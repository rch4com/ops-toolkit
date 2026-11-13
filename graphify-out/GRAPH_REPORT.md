# Graph Report - ops-toolkit  (2026-11-13)

## Corpus Check
- 201 files · ~142,875 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 11 file(s) not represented in the graph (top: (none) 3, .css 1, .props 1)

## Summary
- 2466 nodes · 6603 edges · 135 communities (86 shown, 49 thin omitted)
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 1021 edges (avg confidence: 0.85)
- Token cost: 142,576 input · 18,338 output

## Community Hubs (Navigation)
- Compound File Stream Limits
- Agent Handoff Manager Script
- HTML Plan Interactive Runtime
- OLE Property Set Decoding
- PDF Label Reader & Tests
- MIP Explorer Main Window XAML
- MIP Label Checker Core API
- PDF Object & Trailer Resolver
- Explorer Main ViewModel & Commands
- OpenXML Relationship & Override Tests
- PDF Value Model Types
- Compound File Binary Fixture Builder
- SafeXml & XMP Label Reader
- Compound File Label Reader
- Explorer Timed Check State Models
- PDF Lexer & Indirect Objects
- LdapAuthTester WinForms UI Controls
- Compound File Reader Validation Tests
- MIP Label Checker Benchmarks
- PSJobScheduler Core Engine
- PNG Zlib & XMP Packet Tests
- Compound File Sector & DIFAT Reader
- PDF Cross-Reference & Flate Decoder
- .NET 10 Solution & Project Structure
- Explorer Scan Session & Progress
- HTML Plan Packager Script
- EML & MSG Email Label Readers
- Unslop Story Frame Capture Script
- PNG Chunk Validation Tests
- MSG Compound Directory Fixture
- LDAP Auth Result Models
- LabelInfo XML Record Reader
- OpenXML Archive Part Index
- Explorer Labelable File Extensions
- Explorer App Startup & TimedChecker
- PNG Label Reader & Decompression
- LdapAuthTester CLI Options
- LdapAuthTester Application & Namespaces
- MIP Explorer ViewModels & Scanning
- LdapAuthTester Target & Request Models
- Label Property Parser & Detail Builder
- OpenXML Content Type & Metadata Evidence
- PDF & Image Format Readers
- MIP Internal Guards & PFile Reader
- MIP Explorer CLI Options
- LabelCheckResult Equality & Tests
- PowerShell & LDAP Tool SDLC Docs
- JPEG APP1 XMP Reader & Fixtures
- Compound File Stream Tracking Tests
- Chunked Read Stream Format Tests
- OpenXML Custom Property Label Tests
- PNG IDAT Skip & ReadGuard Tests
- Compound File V3 Tree Fixtures
- OpenXML Legacy Tenant Suppression Tests
- Compound Directory Entry Model
- PNG Sparse Length Seek Guard Tests
- ops-toolkit AI-Native SDLC Governance
- Active Directory Protocol Checker
- Explorer Folder Tree ViewModel
- LdapAuthTester CLI Runner & Entrypoint
- MIPLabelCustomCheck SDLC & Architecture Docs
- Label Property Parser Unit Tests
- Checker Stream Failure Recovery Tests
- PNG Post-Detection Stream Exception Tests
- Dynamic Deflate & Adler32 Test Fixtures
- LDAP CLI Report & Environment Models
- PNG Non-Seekable Stream Tests
- PNG Caller Stream Exception Tests
- OpenXML Unsafe Target Validation Tests
- MIP Explorer CLI Scan Item Model
- File Format Magic Signature Detector
- Checker Non-Seekable Stream Tests
- BenchmarkDotNet Native Sparse File Setup
- MIP Explorer CLI Scan Report Model
- Explorer Label Detail ViewModel
- Supported File Format Enumeration
- Unreadable Stream Error Handling Tests
- Unslop Browser Snapshot Script
- MIP Explorer WPF Application Entry
- JPEG Marker & Segment Reader
- Email Label Reader & Options Tests
- OpenXML Classification Target Escape Tests
- MIP Explorer WPF Value Converters
- Compound File & Binary Format Readers
- CheckState & Elapsed WPF Converters
- OLE Property Set Read Result Model
- Explorer Folder Tree Navigation Tests
- Magic Byte Signatures & Buffer Filling
- TIFF IFD & XMP Label Reader
- TIFF Malformed IFD & Tag Tests
- PNG Dynamic Block & Exception Tests
- PSJobScheduler Cron Expression Evaluator
- HTML Plan Git & File Helpers
- Unslop Story Dashboard Progression Visuals
- MIP Explorer CLI Runner & JSON Serialization
- MIP Label Check Exception Model
- Unslop UI Redesign Skill & References
- OpenXML Relationship XML Inspection
- XMP Packet Label Reader & Exceptions
- LabelStatus Result Enumeration
- Compound File Partial Stream Read Tests
- Caveman & html-plan Agent Skills
- HTML Plan OpenAI Skill Metadata
- Beacon Engraving Hero Visual Reference
- Meadow 3D Editorial Footer Reference
- Unslop Ember Dashboard After Screenshot
- Unslop Ember Landing Hero Screenshot
- LdapAuthTester Project & DirectoryServices
- ops-toolkit Root README Tool Catalog
- PSJobScheduler Status Query Script
- MIP Label PNG Test Fixture
- WindowsOnlyFact xUnit Test Attribute
- Graphify MCP Server Configuration
- PDF Stream Value & Lexer Test
- Test Directories & Migration Plan
- Tool Blueprint Template & Copilot Rules
- Korean Commit Message Convention
- OpsCommon PowerShell Module Docs

## God Nodes (most connected - your core abstractions)
1. `OpenXmlFixture` - 109 edges
2. `OpenXmlLabelReaderTests` - 77 edges
3. `MipLabelChecker` - 70 edges
4. `PngFixture` - 66 edges
5. `PngLabelReaderTests` - 63 edges
6. `CompoundFileFixture` - 55 edges
7. `CompoundFileReader` - 50 edges
8. `PdfLexer` - 47 edges
9. `Window` - 46 edges
10. `MainViewModel` - 42 edges

## Surprising Connections (you probably didn't know these)
- `Agent Handoff Manager Skill` --semantically_similar_to--> `Operational Safety & Idempotency Principle`  [INFERRED] [semantically similar]
  .agents/skills/agent-handoff-manager/SKILL.md → AGENTS.md
- `PSJobScheduler Architecture & State Specification` --semantically_similar_to--> `LdapAuthTester Intent & Account Lockout Guard`  [INFERRED] [semantically similar]
  scripts/scheduler/PSJobScheduler/SPEC.md → src/LdapAuthTester/INTENT.md
- `TestableAdAuthChecker` --inherits--> `AdAuthChecker`  [EXTRACTED]
  tests/dotnet/LdapAuthTester.Tests/AdAuthCheckerTests.cs → src/LdapAuthTester/Services/AdAuthChecker.cs
- `FakeCompoundFileView` --implements--> `ICompoundFileView`  [EXTRACTED]
  tests/dotnet/MIPLabelCustomCheck.Tests/TestData/FakeCompoundFileView.cs → src/MIPLabelCustomCheck/MIPLabelCustomCheck/Internal/CompoundFileLabelReader.cs
- `SecurityLimitTests` --references--> `LabelStatus`  [EXTRACTED]
  tests/dotnet/MIPLabelCustomCheck.Tests/SecurityLimitTests.cs → src/MIPLabelCustomCheck/MIPLabelCustomCheck/LabelCheckResult.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **ops-toolkit AI-Native SDLC Governance Document Pipeline** — agents_ops_toolkit_governance, intent_project_purpose, spec_technical_architecture, plan_implementation_roadmap, review_quality_gate_checklist, claude_claude_code_bridge, gemini_gemini_antigravity_bridge [EXTRACTED 1.00]
- **Unslop UI Audit and 9-Pass Redesign Skill Suite** — agents_skills_unslop_skill_ui_redesign, agents_skills_unslop_references_signs_audit_catalog, agents_skills_unslop_references_tokens_design_scale, agents_skills_unslop_references_visuals_guide, agents_skills_unslop_references_effects_visual_treatments, agents_skills_unslop_references_refs_examples [EXTRACTED 1.00]
- **html-plan Interactive Claim-Exhibit Planning System** — agents_skills_html_plan_skill_interactive_plan, agents_skills_html_plan_references_blocks_reference, agents_skills_html_plan_examples_scheduled_send_example [EXTRACTED 1.00]
- **Tools Implementing the 6-Document Standard Blueprint** — docs_templates_tool_blueprint_template_six_doc_standard, scripts_iis_remove_expirediiscertificate_readme_tool_overview, scripts_scheduler_psjobscheduler_readme_overview, src_ldapauthtester_readme_overview [INFERRED 0.95]
- **Operational Safety & Failure Circuit Guard Mechanisms** — scripts_iis_remove_expirediiscertificate_intent_binding_protection, scripts_scheduler_psjobscheduler_spec_architecture, src_ldapauthtester_intent_lockout_prevention [INFERRED 0.85]
- **MIPLabelCustomCheck 6-Document SDLC Set** — src_miplabelcustomcheck_readme_miplabelcustomcheck, src_miplabelcustomcheck_intent_mip_sensitivity_label_detection, src_miplabelcustomcheck_spec_architecture_spec, src_miplabelcustomcheck_plan_migration_plan, src_miplabelcustomcheck_review_quality_security_review, src_miplabelcustomcheck_agents_zero_dependency_rule [EXTRACTED 1.00]

## Communities (135 total, 49 thin omitted)

### Community 0 - "Compound File Stream Limits"
Cohesion: 0.07
Nodes (12): LimitedReadStream, CanRead, CanSeek, CanWrite, Length, Position, CompoundFileLabelReaderTests, LimitedReadStreamTests (+4 more)

### Community 1 - "Agent Handoff Manager Script"
Cohesion: 0.07
Nodes (42): atomic_json(), _bad_rel_path(), bucket(), _bullets(), _day(), depth_gaps(), doctor(), ensure_repo() (+34 more)

### Community 2 - "HTML Plan Interactive Runtime"
Cohesion: 0.07
Nodes (43): applyIfs(), arrowDefs(), askPick(), boot(), buildResponse(), buildToc(), closePop(), closeSheet() (+35 more)

### Community 3 - "OLE Property Set Decoding"
Cohesion: 0.08
Nodes (10): OlePropertySetReader, ValueDecodeOutcome, CodePageUnsupported, Skip, Success, Crc32, BitReader, AlignedBytePosition (+2 more)

### Community 4 - "PDF Label Reader & Tests"
Cohesion: 0.14
Nodes (5): PdfLabelReaderTests, Inputs, PdfFixture, PdfWriter, Position

### Community 5 - "MIP Explorer Main Window XAML"
Cohesion: 0.04
Nodes (43): ActionIdText, Children, ContentBitsText, Elapsed, StateBrush, DataContext.OpenFileCommand, DataContext.RemeasureCommand, DataContext.RemeasureWithDetails (+35 more)

### Community 6 - "MIP Label Checker Core API"
Cohesion: 0.11
Nodes (5): MipLabelChecker, LabelDetailTests, MipLabelCheckerTests, SecurityLimitTests, TruncatedMagicInputs

### Community 7 - "PDF Object & Trailer Resolver"
Cohesion: 0.12
Nodes (8): LabelPropertyParseResult, PdfObjectResolver, LatestTrailer, PdfDictionary, PdfReference, PdfValue, PdfLabelReader, PdfCrossReferenceReaderTests

### Community 8 - "Explorer Main ViewModel & Commands"
Cohesion: 0.07
Nodes (20): MainViewModel, Drives, ExtensionCount, Files, IncludeDetailsByDefault, IsPathValid, IsScanning, NavigateCommand (+12 more)

### Community 9 - "OpenXML Relationship & Override Tests"
Cohesion: 0.07
Nodes (8): MainContentTypeOverride, OpenXmlDocumentKind, Excel, PowerPoint, Word, OpenXmlFixture, MainDocumentContentType, MainDocumentPath

### Community 10 - "PDF Value Model Types"
Cohesion: 0.11
Nodes (8): PdfArray, PdfBoolean, PdfName, PdfNull, PdfNumber, PdfString, PdfUnsupportedFeatureException, PdfLexerTests

### Community 11 - "Compound File Binary Fixture Builder"
Cohesion: 0.10
Nodes (18): CompoundFileFixture, Bytes, DifatSectorId, DirectorySectorCount, FatSectorCount, FatSectorId, FirstDirectorySectorId, LastDirectorySectorId (+10 more)

### Community 12 - "SafeXml & XMP Label Reader"
Cohesion: 0.09
Nodes (11): SafeXml, XmpLabelReader, NonSeekableReadStream, CanRead, CanSeek, CanWrite, IsDisposed, Length (+3 more)

### Community 13 - "Compound File Label Reader"
Cohesion: 0.09
Nodes (8): CompoundFileLabelReader, CompoundFileReaderView, ICompoundFileView, LegacyLabels, None, CompoundFileReaderView, MsgLabelReader, NamedPropertyMapping

### Community 14 - "Explorer Timed Check State Models"
Cohesion: 0.07
Nodes (25): TimedCheckResult, IsSuccess, CheckState, Canceled, Completed, Failed, Pending, Running (+17 more)

### Community 15 - "PDF Lexer & Indirect Objects"
Cohesion: 0.18
Nodes (3): PdfIndirectObject, PdfLexer, Position

### Community 16 - "LdapAuthTester WinForms UI Controls"
Cohesion: 0.08
Nodes (4): MainForm, ComboBoxItem, Display, Value

### Community 18 - "MIP Label Checker Benchmarks"
Cohesion: 0.09
Nodes (12): LabelCheckerBenchmarks, LargeSparsePngFile, Signature, LabelCheckResult, Details, IsLabeled, LabelId, LabelIds (+4 more)

### Community 19 - "PSJobScheduler Core Engine"
Cohesion: 0.12
Nodes (30): Clear-PendingStateFields(), Convert-TodaysLogsToUtf8(), ConvertTo-LocalDateTimeOrNull(), ConvertTo-NormalizedDowToken(), Expand-RangeList(), Get-ActiveLockCount(), Get-CompletionPath(), Get-JobKey() (+22 more)

### Community 22 - "PDF Cross-Reference & Flate Decoder"
Cohesion: 0.14
Nodes (9): PdfCrossReferenceReader, StartXrefKeyword, PdfCrossReferenceTable, PdfXrefEntry, PdfXrefEntryType, Compressed, Free, InUse (+1 more)

### Community 23 - ".NET 10 Solution & Project Structure"
Cohesion: 0.07
Nodes (25): BenchmarkDotNet (0.15.8), MIPLabelCustomCheck.Benchmarks, net10.0, Microsoft.NET.Sdk, net10.0-windows, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+17 more)

### Community 24 - "Explorer Scan Session & Progress"
Cohesion: 0.16
Nodes (8): ScanProgress, ScanSession, ScanSummary, CliRunnerTests, CollectingProgress, Reports, IgnoringProgress, ScanSessionTests

### Community 25 - "HTML Plan Packager Script"
Cohesion: 0.06
Nodes (24): argv, baseDir, blockSrc(), errors, FENCE, here, html, ids (+16 more)

### Community 26 - "EML & MSG Email Label Readers"
Cohesion: 0.11
Nodes (13): EmlLabelReader, LabelReadResult, ProtectionType, GenericPFile, Native, None, LabelSource, EmailHeader (+5 more)

### Community 27 - "Unslop Story Frame Capture Script"
Cohesion: 0.06
Nodes (22): args, chrome, file, frame, frames, [frameX, frameY, frameWidth], html, [input, out] (+14 more)

### Community 28 - "PNG Chunk Validation Tests"
Cohesion: 0.12
Nodes (3): Chunk, PngFixture, PngFixtureData

### Community 29 - "MSG Compound Directory Fixture"
Cohesion: 0.13
Nodes (11): DirectoryEntry, Child, FullPath, Id, Name, Right, Size, StartSector (+3 more)

### Community 30 - "LDAP Auth Result Models"
Cohesion: 0.11
Nodes (19): AdAuthTestResult, DiagnosticDetail, ElapsedMilliseconds, Endpoint, IsCredentialFailure, IsDryRun, IsNetworkOrServerFailure, IsSkipped (+11 more)

### Community 31 - "LabelInfo XML Record Reader"
Cohesion: 0.14
Nodes (9): LabelInfoReadResult, LabelInfoRecord, ActionId, ContentBits, Method, Name, SetDate, LabelInfoXmlReader (+1 more)

### Community 32 - "OpenXML Archive Part Index"
Cohesion: 0.14
Nodes (5): ArchivePartIndex, HasEquivalentDuplicates, LegacyLabel, LegacyLabelReadResult, OpenXmlLabelReader

### Community 33 - "Explorer Labelable File Extensions"
Cohesion: 0.14
Nodes (4): LabelableExtensions, Default, ExtensionsText, LabelableExtensionsTests

### Community 34 - "Explorer App Startup & TimedChecker"
Cohesion: 0.23
Nodes (3): TimedChecker, TimedCheckerTests, TimedCheckerWarmupTests

### Community 35 - "PNG Label Reader & Decompression"
Cohesion: 0.23
Nodes (6): ChunkHeader, PngLabelReader, PngSignature, XmpKeyword, PngStructureException, XmpPacket

### Community 36 - "LdapAuthTester CLI Options"
Cohesion: 0.09
Nodes (22): CliOptions, Domain, EnvOnly, IsDryRun, LaunchGui, LdapPort, LdapsPort, Method (+14 more)

### Community 38 - "LdapAuthTester Application & Namespaces"
Cohesion: 0.12
Nodes (6): LdapAuthTester, LdapAuthTester.Cli, LdapAuthTester.Models, LdapAuthTester.Tests, LdapAuthTester.Services, Program

### Community 39 - "MIP Explorer ViewModels & Scanning"
Cohesion: 0.17
Nodes (4): MIPLabelCustomCheck.Explorer.ViewModels, MIPLabelCustomCheck.Explorer.Tests, MIPLabelCustomCheck.Explorer.Scanning, FixturePaths

### Community 40 - "LdapAuthTester Target & Request Models"
Cohesion: 0.09
Nodes (18): AdAuthTestRequest, Domain, IsDryRun, LdapPort, LdapsPort, Password, SkipSecondOnCredentialFailure, TestMethod (+10 more)

### Community 41 - "Label Property Parser & Detail Builder"
Cohesion: 0.11
Nodes (14): MIPLabelCustomCheck, DetailBuilder, LabelDetail, ActionId, ContentBits, Id, Method, Name (+6 more)

### Community 42 - "OpenXML Content Type & Metadata Evidence"
Cohesion: 0.20
Nodes (4): ContentTypeEvidence, Invalid, MetadataDocument, StructuralInspection

### Community 44 - "MIP Internal Guards & PFile Reader"
Cohesion: 0.15
Nodes (5): MIPLabelCustomCheck.Internal, MIPLabelCustomCheck.Tests, InputGuard, PFileProtectionReader, FixturePaths

### Community 45 - "MIP Explorer CLI Options"
Cohesion: 0.13
Nodes (12): CliOptions, AllFiles, IsDryRun, LaunchGui, OutputJson, Recursive, ShowHelp, ShowVersion (+4 more)

### Community 47 - "PowerShell & LDAP Tool SDLC Docs"
Cohesion: 0.12
Nodes (13): Remove-ExpiredIISCertificate Implementation Plan, Remove-ExpiredIISCertificate Tool Overview, Remove-ExpiredIISCertificate Quality & Safety Review, Remove-ExpiredIISCertificate Specification & Status Codes, PSJobScheduler Implementation Plan, PSJobScheduler Cron-Safe Job Scheduler Overview, PSJobScheduler Quality & Safety Review, PSJobScheduler Architecture & State Specification (+5 more)

### Community 49 - "Compound File Stream Tracking Tests"
Cohesion: 0.15
Nodes (7): TrackingStream, CanRead, CanSeek, CanWrite, IsDisposed, Length, Position

### Community 50 - "Chunked Read Stream Format Tests"
Cohesion: 0.13
Nodes (9): ChunkedReadStream, CanRead, CanSeek, CanWrite, LargestReadRequest, LargestReadResult, Length, Position (+1 more)

### Community 51 - "OpenXML Custom Property Label Tests"
Cohesion: 0.25
Nodes (5): OpenXmlLabelReaderTests, UnsafeClassificationTargets, UnsafeMainOverridePartNames, UnsafeOfficeDocumentTargets, CustomPropertyValue

### Community 52 - "PNG IDAT Skip & ReadGuard Tests"
Cohesion: 0.14
Nodes (7): ReadGuardStream, CanRead, CanSeek, CanWrite, Length, Position, ReadGuardRange

### Community 53 - "Compound File V3 Tree Fixtures"
Cohesion: 0.21
Nodes (6): DirectorySpec, Child, Left, Name, Right, Type

### Community 55 - "Compound Directory Entry Model"
Cohesion: 0.13
Nodes (14): CompoundDirectoryEntry, ChildId, Id, LeftSiblingId, Name, RightSiblingId, StartSector, StreamSize (+6 more)

### Community 56 - "PNG Sparse Length Seek Guard Tests"
Cohesion: 0.15
Nodes (7): SparseLengthStream, CanRead, CanSeek, CanWrite, LargeSeekAttempted, Length, Position

### Community 57 - "ops-toolkit AI-Native SDLC Governance"
Cohesion: 0.26
Nodes (11): AGENTS.md — AI Coding Agent Guidelines for ops-toolkit, Agent Handoff Manager Skill, 2-Tier .NET Solution Architecture (Tool .sln + Root OpsToolkit.slnx), CLAUDE.md — Claude Code Instructions Bridge, GEMINI.md — Gemini CLI & Antigravity Instructions Bridge, INTENT.md — Project Purpose & Operational Intent, PLAN.md — Implementation Plan & Roadmap, REVIEW.md — Code Review & Quality Gate Checklist (+3 more)

### Community 59 - "Explorer Folder Tree ViewModel"
Cohesion: 0.22
Nodes (7): FolderTreeNodeViewModel, Children, DisplayName, IsAccessible, IsExpanded, IsSelected, Path

### Community 61 - "MIPLabelCustomCheck SDLC & Architecture Docs"
Cohesion: 0.14
Nodes (11): MIP Label.pdf Test Fixture, Zero-Dependency MIP Sensitivity Label Detection, MIPLabelCustomCheck.Explorer WPF GUI + CLI Hybrid, LabelCheckerBenchmarks Suite, LargeSparsePngFile 512 MiB Benchmark Generator, MipLabelChecker Core API, MIPLabelCustomCheck Overview & Usage, MIPLabelCustomCheck Quality & Security Review (+3 more)

### Community 63 - "Checker Stream Failure Recovery Tests"
Cohesion: 0.16
Nodes (6): ThrowingAfterDetectionStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 64 - "PNG Post-Detection Stream Exception Tests"
Cohesion: 0.16
Nodes (6): ThrowingAfterDetectionReadStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 65 - "Dynamic Deflate & Adler32 Test Fixtures"
Cohesion: 0.29
Nodes (3): DeflateBitWriter, DynamicDeflateFixture, HuffmanCode

### Community 66 - "LDAP CLI Report & Environment Models"
Cohesion: 0.15
Nodes (13): AdAuthCliReport, Environment, IsDryRun, OverallSuccess, Results, Timestamp, AdEnvironmentInfo, CurrentLogonUser (+5 more)

### Community 67 - "PNG Non-Seekable Stream Tests"
Cohesion: 0.17
Nodes (6): NonSeekableReadStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 68 - "PNG Caller Stream Exception Tests"
Cohesion: 0.17
Nodes (6): ThrowingInvalidDataReadStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 70 - "MIP Explorer CLI Scan Item Model"
Cohesion: 0.17
Nodes (12): CliFileScanItem, Details, ElapsedMilliseconds, ErrorMessage, FilePath, FileSizeBytes, IsDryRun, LabelIds (+4 more)

### Community 71 - "File Format Magic Signature Detector"
Cohesion: 0.24
Nodes (3): FileFormatDetectorTests, PolyglotSignatures, Signatures

### Community 72 - "Checker Non-Seekable Stream Tests"
Cohesion: 0.17
Nodes (6): NonSeekableStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 75 - "MIP Explorer CLI Scan Report Model"
Cohesion: 0.18
Nodes (11): CliScanReport, ErrorCount, IsDryRun, Items, LabeledCount, ProtectedUnknownCount, TargetPath, Timestamp (+3 more)

### Community 76 - "Explorer Label Detail ViewModel"
Cohesion: 0.20
Nodes (10): Details, LabelDetailViewModel, ActionIdText, ContentBitsText, Id, IdText, MethodText, NameText (+2 more)

### Community 77 - "Supported File Format Enumeration"
Cohesion: 0.18
Nodes (10): FileFormat, CompoundFile, Eml, GenericPFile, Jpeg, Pdf, Png, Tiff (+2 more)

### Community 78 - "Unreadable Stream Error Handling Tests"
Cohesion: 0.18
Nodes (6): UnreadableStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 79 - "Unslop Browser Snapshot Script"
Cohesion: 0.20
Nodes (3): args, chrome, positionals

### Community 80 - "MIP Explorer WPF Application Entry"
Cohesion: 0.22
Nodes (4): MIPLabelCustomCheck.Explorer, Application, App, Program

### Community 81 - "JPEG Marker & Segment Reader"
Cohesion: 0.58
Nodes (3): JpegLabelReader, XmpIdentifier, JpegStructureException

### Community 82 - "Email Label Reader & Options Tests"
Cohesion: 0.24
Nodes (5): LabelCheckOptions, Default, IncludeDetails, WithDetails, EmailLabelReaderTests

### Community 87 - "OLE Property Set Read Result Model"
Cohesion: 0.28
Nodes (4): OlePropertySetReadResult, OlePropertySetStatus, NotPresent, Success

### Community 89 - "Magic Byte Signatures & Buffer Filling"
Cohesion: 0.22
Nodes (7): FileFormatDetector, BigEndianTiffSignature, CompoundFileSignature, JpegSignature, LittleEndianTiffSignature, PngSignature, ZipSignature

### Community 93 - "PSJobScheduler Cron Expression Evaluator"
Cohesion: 0.39
Nodes (6): ConvertTo-NormalizedDowToken(), Expand-RangeList(), Get-NextCronTime(), New-CronMatcher(), Test-CronMatch(), Test-SetContains()

### Community 94 - "HTML Plan Git & File Helpers"
Cohesion: 0.33
Nodes (7): err(), findFile(), git(), gitShow(), inside(), real(), stamp()

### Community 99 - "MIP Label Check Exception Model"
Cohesion: 0.33
Nodes (5): MipLabelCheckErrorReason, FileLocked, MipLabelCheckException, FilePath, Reason

### Community 100 - "Unslop UI Redesign Skill & References"
Cohesion: 0.40
Nodes (6): Unslop Code Effects Reference (Glyphs, Halftone, Dithering, Duotone), Unslop Finished Screen References Catalog, Unslop Signs of Slop Audit Catalog, Unslop Design Tokens Reference (Spacing, Neutral Ramps, Type, Radius, Motion), Unslop Hero & Moment Visuals Guide, Unslop Skill — 9-Pass UI Redesign & Craft Workflow

### Community 103 - "LabelStatus Result Enumeration"
Cohesion: 0.33
Nodes (6): LabelStatus, InvalidFile, Labeled, ProtectedLabelUnknown, Unlabeled, Unsupported

### Community 105 - "Caveman & html-plan Agent Skills"
Cohesion: 0.50
Nodes (5): Caveman Skill README & Sibling Modes (caveman, ultracave, megacave), Caveman Terse Voice Skill Specification, Scheduled Send in PostBox — Example html-plan Document, html-plan Custom Element Blocks Reference (doc-plan, doc-claim, doc-mock, doc-machine, doc-calls, doc-schema, doc-code, doc-ask), html-plan Interactive Claim-Tree Implementation Plan Skill

### Community 106 - "HTML Plan OpenAI Skill Metadata"
Cohesion: 0.40
Nodes (4): interface, default_prompt, display_name, short_description

### Community 107 - "Beacon Engraving Hero Visual Reference"
Cohesion: 0.50
Nodes (3): Monochromatic Cobalt Blue Woodcut/Engraving Lighthouse Illustration, High-Contrast Editorial Serif Headline Typography, Beacon Incident Monitoring Hero Screen (Engraving Style)

### Community 108 - "Meadow 3D Editorial Footer Reference"
Cohesion: 0.50
Nodes (4): Quiet Place for a Loud Internet Brand Positioning, Oversized Wordmark Behind 3D Rolling Grass Foreground, Meadow 3D Footer & Hero Reference Screen, Subsurface-Scattered 3D Geometric Crystalline Forms in Grass

### Community 109 - "Unslop Ember Dashboard After Screenshot"
Cohesion: 0.50
Nodes (4): KPI Summary Cards with 3D Card Visual and Tabular Metrics, Multi-Plan Revenue Spline Chart with Mirrored Baseline, Unslop Dashboard After Reference Screenshot (Ember Overview), Dark Sidebar Navigation with Search and Tiered Plans

### Community 110 - "Unslop Ember Landing Hero Screenshot"
Cohesion: 0.50
Nodes (4): ASCII Dithered Paper Airplane Hero Illustration with Status Callouts, Direct Product Copywriting (Invoices that chase themselves), Ember Financial Overview Dashboard Preview, Ember Landing Page After Unslop Screenshot

### Community 111 - "LdapAuthTester Project & DirectoryServices"
Cohesion: 0.40
Nodes (4): System.DirectoryServices.AccountManagement (10.0.0), System.DirectoryServices.Protocols (10.0.0), net10.0-windows, Microsoft.NET.Sdk

### Community 112 - "ops-toolkit Root README Tool Catalog"
Cohesion: 0.40
Nodes (5): LdapAuthTester Tool Catalog Entry, MIPLabelCustomCheck Tool Catalog Entry, README.md — ops-toolkit Repository Overview & Tool Catalog, PSJobScheduler Tool Catalog Entry, Remove-ExpiredIISCertificate Tool Catalog Entry

### Community 114 - "MIP Label PNG Test Fixture"
Cohesion: 0.67
Nodes (3): Hand-Drawn Checkmark / V-Shaped Test Graphic, MIP Label Sample PNG Fixture Image, Microsoft Information Protection (MIP) Sensitivity Label Test Fixture Concept

### Community 120 - "Test Directories & Migration Plan"
Cohesion: 0.67
Nodes (3): MIPLabelCustomCheck .NET 10 Migration & Hybrid Plan, .NET 10 xUnit Tests Directory, PowerShell Pester Tests Directory

## Knowledge Gaps
- **446 isolated node(s):** `graphify-mcp`, `LaunchGui`, `ShowHelp`, `ShowVersion`, `EnvOnly` (+441 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 724 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **49 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainViewModel` connect `Explorer Main ViewModel & Commands` to `Explorer Labelable File Extensions`, `Explorer App Startup & TimedChecker`, `MIP Explorer ViewModels & Scanning`, `Explorer Timed Check State Models`, `Explorer Scan Session & Progress`, `Explorer Folder Tree ViewModel`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **Are the 60 inferred relationships involving `OpenXmlFixture` (e.g. with `OpenXmlLabelReaderTests.Read_accepts_case_equivalent_main_part_entry()` and `OpenXmlLabelReaderTests.Read_accepts_later_segment_colon_main_target()`) actually correct?**
  _`OpenXmlFixture` has 60 INFERRED edges - model-reasoned connections that need verification._
- **What connects `graphify-mcp`, `LaunchGui`, `ShowHelp` to the rest of the system?**
  _446 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Compound File Stream Limits` be split into smaller, more focused modules?**
  _Cohesion score 0.07152875175315568 - nodes in this community are weakly interconnected._
- **Why does `PdfLexer` connect `PDF Lexer & Indirect Objects` to `PDF Object & Trailer Resolver`, `PDF Value Model Types`, `PDF & Image Format Readers`, `PDF Cross-Reference & Flate Decoder`, `PDF Stream Value & Lexer Test`?**
  _High betweenness centrality (0.029) - this node is a cross-community bridge._
- **Are the 61 inferred relationships involving `MipLabelChecker` (e.g. with `App.OnStartup()` and `Program.Main()`) actually correct?**
  _`MipLabelChecker` has 61 INFERRED edges - model-reasoned connections that need verification._
- **Should `Agent Handoff Manager Script` be split into smaller, more focused modules?**
  _Cohesion score 0.07071455016660497 - nodes in this community are weakly interconnected._