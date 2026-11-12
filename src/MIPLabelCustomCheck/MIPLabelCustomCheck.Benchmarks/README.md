# MIPLabelCustomCheck.Benchmarks

BenchmarkDotNet benchmarks for `MipLabelChecker`, covering every real fixture format
(`.docx`/`.xlsx`/`.pptx`, `.doc`/`.xls`/`.ppt`, `.pdf`, `.png`), a synthetic large-file case, and a
caller-owned parallel check.

## What's benchmarked

`LabelCheckerBenchmarks` shares a single `MipLabelChecker` instance across all benchmark methods
(mirroring real usage — the checker is stateless and meant to be reused):

- **Single-file benchmarks** (`*Single`) — one `Check(string)` call per fixture:
  `CheckDocxSingle`, `CheckXlsxSingle`, `CheckPptxSingle`, `CheckDocSingle`, `CheckXlsSingle`,
  `CheckPptSingle`, `CheckPdfSingle`, `CheckPngSingle`. Each resolves the real
  `MIP Label.<ext>` fixture from the repository root in `[GlobalSetup]`.
- **`CheckLargeUnlabeledSingle`** — a single `Check(string)` call against a synthetic 512 MiB
  unlabeled PNG created in `[GlobalSetup]` (see below) and deleted in `[GlobalCleanup]`. Exists
  to catch a reader whose time or, especially, managed allocations scale with file size instead
  of with metadata size.
- **`CheckAllFixturesParallel`** — the caller-owned parallel pattern documented in the project
  README (`Parallel.For` over all 8 real fixtures sharing one checker instance).

`[MemoryDiagnoser]` is enabled at the class level, so every run reports allocated bytes per
operation alongside timing.

### The synthetic 512 MiB file

`LargeSparsePngFile.Create` builds a well-formed PNG whose only real structure is an 8-byte
signature and 13-byte `IHDR` chunk at the very beginning and an `IEND` chunk at the very end. In
between sits one large ancillary chunk (`juNK`, safe-to-ignore per the PNG spec) whose declared
payload length accounts for the rest of the 512 MiB — but those payload bytes are never written
to disk. On Windows the file is first marked sparse via `FSCTL_SET_SPARSE` (best-effort; a
volume without sparse-file support just means the file occupies its full nominal size on disk,
which does not affect the benchmark's correctness, only disk usage). `PngLabelReader` skips
non-`iTXt`, non-`IEND` chunks with a single `Seek`, and does not validate the CRC of skipped
chunks, so the unwritten region is never read back either.

## Running

Full suite:

```powershell
dotnet run -c Release --project benchmarks/MIPLabelCustomCheck.Benchmarks
```

Quick verification (single-file benchmarks only, minimal warmup/iterations — used to produce the
baseline below):

```powershell
dotnet run -c Release --project benchmarks/MIPLabelCustomCheck.Benchmarks -- --filter "*Single*" --warmupCount 1 --iterationCount 3
```

BenchmarkDotNet writes detailed reports (CSV, GitHub-flavored Markdown, HTML) under
`BenchmarkDotNet.Artifacts/results/`, which is gitignored.

## Recorded baseline

Captured with the quick-verification command above.

**Environment:**

- BenchmarkDotNet v0.15.8
- Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
- AMD Ryzen 7 5800X 3.80GHz, 1 CPU, 16 logical and 8 physical cores
- .NET SDK 10.0.301
- Host / Job runtime: .NET 8.0.29 (8.0.29, 8.0.2926.32403), X64 RyuJIT x86-64-v3, GC = Concurrent
  Workstation
- Job: `IterationCount=3, WarmupCount=1` (explicit override; BenchmarkDotNet's pilot stage still
  ran to pick an invocation count per iteration, as it does regardless of `--iterationCount`)

**Results:**

| Method | Mean | StdDev | Allocated |
| --- | ---: | ---: | ---: |
| `CheckDocxSingle` | 258.45 us | 2.644 us | 104.69 KB |
| `CheckXlsxSingle` | 244.26 us | 8.146 us | 103.39 KB |
| `CheckPptxSingle` | 285.17 us | 135.187 us | 170.70 KB |
| `CheckDocSingle` | 56.04 us | 1.835 us | 18.01 KB |
| `CheckXlsSingle` | 59.81 us | 9.414 us | 16.84 KB |
| `CheckPptSingle` | 64.03 us | 3.365 us | 20.65 KB |
| `CheckPdfSingle` | 107.00 us | 28.277 us | 126.76 KB |
| `CheckPngSingle` | 76.05 us | 4.802 us | 28.77 KB |
| `CheckLargeUnlabeledSingle` | 45.43 us | 3.925 us | **4.63 KB** |

(`StdDev` is wide on several rows because only 3 iterations with 1 warmup were run for a quick
verification pass, not a statistically tuned baseline; treat these as order-of-magnitude numbers,
not precise regression thresholds. Re-run the full suite — default job, no `--iterationCount`
override — for a tighter baseline before using these numbers to gate a change.)

**Key finding:** `CheckLargeUnlabeledSingle` — the 512 MiB synthetic file — allocated only
**4.63 KB**, on par with or less than the real fixtures (16–171 KB) and nowhere near a
size-proportional (≈512 MiB) managed allocation. Every reader on the hot path (format detection,
then `PngLabelReader`) processes this file by seeking past its single large unread chunk rather
than buffering it, exactly as intended. All 9 single-file benchmarks completed in the same run
(~41 seconds total workload time) with no errors, confirming the benchmark project, the sparse
large-file generator, and every supported-format reader all work end-to-end in Release
configuration.
