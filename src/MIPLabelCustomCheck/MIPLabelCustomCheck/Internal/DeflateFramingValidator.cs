using System.Buffers.Binary;

namespace MIPLabelCustomCheck.Internal;

internal static class DeflateFramingValidator
{
    private const int MaximumCodeLengthBits = 15;
    private const int MaximumCodeLengthAlphabetBits = 7;
    private const uint AdlerModulus = 65521;

    private static readonly int[] CodeLengthOrder =
        [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    private static readonly int[] LengthBases =
        [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31,
            35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];

    private static readonly int[] LengthExtraBits =
        [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2,
            3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];

    private static readonly int[] DistanceBases =
        [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129,
            193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097,
            6145, 8193, 12289, 16385, 24577];

    private static readonly int[] DistanceExtraBits =
        [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6,
            6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];

    private static readonly HuffmanTree FixedLiteralLengthTree =
        CreateFixedLiteralLengthTree();

    private static readonly HuffmanTree FixedDistanceTree = CreateFixedDistanceTree();

    internal static void Validate(
        ReadOnlySpan<byte> zlibData,
        ReadOnlySpan<byte> decodedData,
        int maximumDecodedBytes)
    {
        if (maximumDecodedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDecodedBytes));
        }

        if (decodedData.Length > maximumDecodedBytes || zlibData.Length < 6)
        {
            throw Invalid("The zlib stream is truncated or exceeds the output limit.");
        }

        ValidateHeader(zlibData[0], zlibData[1]);

        var reader = new BitReader(zlibData[2..]);
        var outputLength = 0;
        var finalBlock = false;

        while (!finalBlock)
        {
            finalBlock = reader.ReadBits(1) != 0;
            switch (reader.ReadBits(2))
            {
                case 0:
                    ReadStoredBlock(ref reader, ref outputLength, maximumDecodedBytes);
                    break;
                case 1:
                    ReadCompressedBlock(
                        ref reader,
                        FixedLiteralLengthTree,
                        FixedDistanceTree,
                        ref outputLength,
                        maximumDecodedBytes);
                    break;
                case 2:
                    ReadDynamicBlock(
                        ref reader,
                        ref outputLength,
                        maximumDecodedBytes);
                    break;
                default:
                    throw Invalid("The DEFLATE block type is reserved.");
            }
        }

        reader.AlignToByte();
        var trailerOffset = checked(2 + reader.AlignedBytePosition);
        if (zlibData.Length - trailerOffset != sizeof(uint))
        {
            throw Invalid("The zlib stream must end with exactly one Adler-32 trailer.");
        }

        if (outputLength != decodedData.Length)
        {
            throw Invalid("The DEFLATE output length does not match the decoded data.");
        }

        var expectedAdler = BinaryPrimitives.ReadUInt32BigEndian(zlibData[trailerOffset..]);
        if (ComputeAdler32(decodedData) != expectedAdler)
        {
            throw Invalid("The zlib Adler-32 checksum is invalid.");
        }
    }

    private static void ValidateHeader(byte cmf, byte flg)
    {
        if ((cmf & 0x0F) != 8 ||
            (cmf >> 4) > 7 ||
            ((cmf << 8) + flg) % 31 != 0 ||
            (flg & 0x20) != 0)
        {
            throw Invalid("The RFC 1950 zlib header is invalid.");
        }
    }

    private static void ReadStoredBlock(
        ref BitReader reader,
        ref int outputLength,
        int maximumDecodedBytes)
    {
        reader.AlignToByte();
        var length = reader.ReadBits(16);
        var complement = reader.ReadBits(16);
        if ((length ^ complement) != 0xFFFF)
        {
            throw Invalid("The stored DEFLATE block length is invalid.");
        }

        AddOutput(ref outputLength, length, maximumDecodedBytes);
        reader.SkipAlignedBytes(length);
    }

    private static void ReadDynamicBlock(
        ref BitReader reader,
        ref int outputLength,
        int maximumDecodedBytes)
    {
        var literalLengthCount = reader.ReadBits(5) + 257;
        if (literalLengthCount > 286)
        {
            throw Invalid("The dynamic literal/length alphabet is too large.");
        }

        var distanceCount = reader.ReadBits(5) + 1;
        var codeLengthCount = reader.ReadBits(4) + 4;
        Span<byte> codeLengthLengths = stackalloc byte[19];
        for (var index = 0; index < codeLengthCount; index++)
        {
            codeLengthLengths[CodeLengthOrder[index]] = checked((byte)reader.ReadBits(3));
        }

        var codeLengthTree = HuffmanTree.Create(
            codeLengthLengths,
            MaximumCodeLengthAlphabetBits,
            "code-length");
        var totalCodeCount = literalLengthCount + distanceCount;
        Span<byte> combinedLengths = stackalloc byte[318];
        var lengthIndex = 0;

        while (lengthIndex < totalCodeCount)
        {
            var symbol = codeLengthTree.Decode(ref reader);
            switch (symbol)
            {
                case <= 15:
                    combinedLengths[lengthIndex++] = checked((byte)symbol);
                    break;
                case 16:
                    if (lengthIndex == 0)
                    {
                        throw Invalid("A code-length repeat has no previous value.");
                    }

                    RepeatLength(
                        combinedLengths,
                        ref lengthIndex,
                        totalCodeCount,
                        combinedLengths[lengthIndex - 1],
                        reader.ReadBits(2) + 3);
                    break;
                case 17:
                    RepeatLength(
                        combinedLengths,
                        ref lengthIndex,
                        totalCodeCount,
                        0,
                        reader.ReadBits(3) + 3);
                    break;
                case 18:
                    RepeatLength(
                        combinedLengths,
                        ref lengthIndex,
                        totalCodeCount,
                        0,
                        reader.ReadBits(7) + 11);
                    break;
                default:
                    throw Invalid("The dynamic code-length symbol is invalid.");
            }
        }

        var literalLengthTree = HuffmanTree.Create(
            combinedLengths[..literalLengthCount],
            MaximumCodeLengthBits,
            "literal/length",
            allowIncompleteSingleton: true);
        if (combinedLengths[256] == 0)
        {
            throw Invalid("The dynamic literal/length tree has no end-of-block code.");
        }

        var distanceTree = HuffmanTree.Create(
            combinedLengths.Slice(literalLengthCount, distanceCount),
            MaximumCodeLengthBits,
            "distance",
            allowIncompleteSingleton: true,
            allowEmpty: true);

        ReadCompressedBlock(
            ref reader,
            literalLengthTree,
            distanceTree,
            ref outputLength,
            maximumDecodedBytes);
    }

    private static void RepeatLength(
        Span<byte> lengths,
        ref int index,
        int totalCount,
        byte value,
        int repeatCount)
    {
        if (repeatCount > totalCount - index)
        {
            throw Invalid("A dynamic code-length repeat exceeds the alphabet.");
        }

        lengths.Slice(index, repeatCount).Fill(value);
        index += repeatCount;
    }

    private static void ReadCompressedBlock(
        ref BitReader reader,
        HuffmanTree literalLengthTree,
        HuffmanTree distanceTree,
        ref int outputLength,
        int maximumDecodedBytes)
    {
        while (true)
        {
            var symbol = literalLengthTree.Decode(ref reader);
            if (symbol < 256)
            {
                AddOutput(ref outputLength, 1, maximumDecodedBytes);
                continue;
            }

            if (symbol == 256)
            {
                return;
            }

            var lengthIndex = symbol - 257;
            if ((uint)lengthIndex >= LengthBases.Length)
            {
                throw Invalid("The DEFLATE length symbol is reserved.");
            }

            var length = LengthBases[lengthIndex] +
                reader.ReadBits(LengthExtraBits[lengthIndex]);
            var distanceSymbol = distanceTree.Decode(ref reader);
            if ((uint)distanceSymbol >= DistanceBases.Length)
            {
                throw Invalid("The DEFLATE distance symbol is reserved.");
            }

            var distance = DistanceBases[distanceSymbol] +
                reader.ReadBits(DistanceExtraBits[distanceSymbol]);
            if (distance > outputLength)
            {
                throw Invalid("The DEFLATE distance exceeds the produced output.");
            }

            AddOutput(ref outputLength, length, maximumDecodedBytes);
        }
    }

    private static void AddOutput(
        ref int outputLength,
        int additionalLength,
        int maximumDecodedBytes)
    {
        if (additionalLength < 0 || outputLength > maximumDecodedBytes - additionalLength)
        {
            throw Invalid("The DEFLATE output exceeds the configured limit.");
        }

        outputLength = checked(outputLength + additionalLength);
    }

    private static HuffmanTree CreateFixedLiteralLengthTree()
    {
        Span<byte> lengths = stackalloc byte[288];
        lengths[..144].Fill(8);
        lengths[144..256].Fill(9);
        lengths[256..280].Fill(7);
        lengths[280..].Fill(8);
        return HuffmanTree.Create(lengths, MaximumCodeLengthBits, "fixed literal/length");
    }

    private static HuffmanTree CreateFixedDistanceTree()
    {
        Span<byte> lengths = stackalloc byte[32];
        lengths.Fill(5);
        return HuffmanTree.Create(lengths, MaximumCodeLengthBits, "fixed distance");
    }

    private static uint ComputeAdler32(ReadOnlySpan<byte> data)
    {
        uint first = 1;
        uint second = 0;

        foreach (var value in data)
        {
            first += value;
            if (first >= AdlerModulus)
            {
                first -= AdlerModulus;
            }

            second += first;
            if (second >= AdlerModulus)
            {
                second -= AdlerModulus;
            }
        }

        return (second << 16) | first;
    }

    private static int ReverseBits(int value, int bitCount)
    {
        var reversed = 0;
        for (var index = 0; index < bitCount; index++)
        {
            reversed = (reversed << 1) | (value & 1);
            value >>= 1;
        }

        return reversed;
    }

    private static InvalidDataException Invalid(string message) => new(message);

    private sealed class HuffmanTree
    {
        private readonly int[] _lookup;
        private readonly int _maximumBits;

        private HuffmanTree(int[] lookup, int maximumBits)
        {
            _lookup = lookup;
            _maximumBits = maximumBits;
        }

        internal static HuffmanTree Create(
            ReadOnlySpan<byte> lengths,
            int maximumAllowedBits,
            string description,
            bool allowIncompleteSingleton = false,
            bool allowEmpty = false)
        {
            Span<int> counts = stackalloc int[MaximumCodeLengthBits + 1];
            var maximumBits = 0;
            var symbolCount = 0;

            foreach (var length in lengths)
            {
                if (length > maximumAllowedBits)
                {
                    throw Invalid($"The {description} Huffman code is too long.");
                }

                if (length != 0)
                {
                    counts[length]++;
                    maximumBits = Math.Max(maximumBits, length);
                    symbolCount++;
                }
            }

            if (maximumBits == 0)
            {
                if (allowEmpty)
                {
                    return new HuffmanTree([], 0);
                }

                throw Invalid($"The {description} Huffman tree is empty.");
            }

            var remainingCodes = 1;
            for (var bits = 1; bits <= maximumAllowedBits; bits++)
            {
                remainingCodes = (remainingCodes << 1) - counts[bits];
                if (remainingCodes < 0)
                {
                    throw Invalid($"The {description} Huffman tree is oversubscribed.");
                }
            }

            if (remainingCodes != 0 &&
                !(allowIncompleteSingleton && symbolCount == 1 && maximumBits == 1))
            {
                throw Invalid($"The {description} Huffman tree is incomplete.");
            }

            Span<int> nextCodes = stackalloc int[MaximumCodeLengthBits + 1];
            var nextCode = 0;
            for (var bits = 1; bits <= maximumBits; bits++)
            {
                nextCode = (nextCode + counts[bits - 1]) << 1;
                nextCodes[bits] = nextCode;
            }

            var lookup = new int[1 << maximumBits];
            for (var symbol = 0; symbol < lengths.Length; symbol++)
            {
                var length = lengths[symbol];
                if (length == 0)
                {
                    continue;
                }

                var reversedCode = ReverseBits(nextCodes[length]++, length);
                var suffixCount = 1 << (maximumBits - length);
                var packed = (length << 16) | (symbol + 1);
                for (var suffix = 0; suffix < suffixCount; suffix++)
                {
                    lookup[reversedCode | (suffix << length)] = packed;
                }
            }

            return new HuffmanTree(lookup, maximumBits);
        }

        internal int Decode(ref BitReader reader)
        {
            if (_maximumBits == 0)
            {
                throw Invalid("A DEFLATE length requires a distance code.");
            }

            var packed = _lookup[reader.PeekBits(_maximumBits)];
            if (packed == 0)
            {
                throw Invalid("The DEFLATE Huffman code is invalid.");
            }

            reader.SkipBits(packed >> 16);
            return (packed & 0xFFFF) - 1;
        }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _bitPosition;

        internal BitReader(ReadOnlySpan<byte> data) => _data = data;

        internal int AlignedBytePosition
        {
            get
            {
                if ((_bitPosition & 7) != 0)
                {
                    throw new InvalidOperationException("The bit reader is not byte-aligned.");
                }

                return _bitPosition >> 3;
            }
        }

        internal int ReadBits(int bitCount)
        {
            var value = PeekBits(bitCount);
            _bitPosition = checked(_bitPosition + bitCount);
            return value;
        }

        internal int PeekBits(int bitCount)
        {
            if (bitCount is < 0 or > 16 ||
                _bitPosition > checked(_data.Length * 8) - bitCount)
            {
                throw Invalid("The DEFLATE bit stream is truncated.");
            }

            var value = 0;
            for (var index = 0; index < bitCount; index++)
            {
                var position = _bitPosition + index;
                value |= ((_data[position >> 3] >> (position & 7)) & 1) << index;
            }

            return value;
        }

        internal void SkipBits(int bitCount)
        {
            _ = ReadBits(bitCount);
        }

        internal void AlignToByte()
        {
            _bitPosition = checked((_bitPosition + 7) & ~7);
            if (_bitPosition > checked(_data.Length * 8))
            {
                throw Invalid("The DEFLATE bit stream is truncated.");
            }
        }

        internal void SkipAlignedBytes(int byteCount)
        {
            if ((_bitPosition & 7) != 0 ||
                byteCount < 0 ||
                byteCount > _data.Length - (_bitPosition >> 3))
            {
                throw Invalid("The stored DEFLATE block is truncated.");
            }

            _bitPosition = checked(_bitPosition + byteCount * 8);
        }
    }
}
