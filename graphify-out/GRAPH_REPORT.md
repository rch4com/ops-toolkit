# Graph Report - ops-toolkit  (2026-11-13)

## Corpus Check
- 245 files · ~160,938 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 12 file(s) not represented in the graph (top: (none) 3, .css 2, .props 1)

## Summary
- 2886 nodes · 7540 edges · 161 communities (104 shown, 57 thin omitted)
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 1148 edges (avg confidence: 0.85)
- Token cost: unavailable (host agent usage not exposed; not zero)

## Community Hubs (Navigation)
- Agent Handoff Manager Script
- PDF Label Reader & Tests
- Compound File Stream Limits
- HTML Plan Interactive Runtime
- MIP Label Checker Core API
- MIP Format Reader Infrastructure
- MIP Results And Benchmarks
- LDAP And MIP Specifications
- OLE Property Set Decoding
- Explorer Main ViewModel & Commands
- PDF Object & Trailer Resolver
- File Signature Detection
- SafeXml & XMP Label Reader
- MIP Explorer Main Window XAML
- PDF Value Model Types
- PNG Zlib & XMP Packet Tests
- Compound File Binary Fixture Builder
- Compound File Stream Limits
- OpenXML Relationship & Override Tests
- LdapAuthTester WinForms UI Controls
- PNG Chunk Validation Tests
- PSJobScheduler Core Engine
- LDAP Auth Result Models
- PDF Lexer & Indirect Objects
- Compound File Reader Validation Tests
- Explorer Scan Session & Progress
- .NET 10 Solution & Project Structure
- LdapAuthTester CLI Options
- HTML Plan Packager Script
- Explorer Timed Check State Models
- Unslop Story Frame Capture Script
- PDF Cross-Reference & Flate Decoder
- MSG Compound Directory Fixture
- LDAP Typed Settings Storage
- MIP Explorer ViewModels & Scanning
- Compound File Sector & DIFAT Reader
- JPEG APP1 XMP Reader & Fixtures
- PNG Label Reader & Decompression
- MIP Explorer CLI Options
- Explorer App Startup & TimedChecker
- EML & MSG Email Label Readers
- Explorer Folder Tree ViewModel
- Compound File Stream Tracking Tests
- LabelInfo XML Record Reader
- Label Property Parser & Detail Builder
- SafeXml & XMP Label Reader
- Active Directory Protocol Checker
- LdapAuthTester Target & Request Models
- Cancellable JSON Profile Storage
- LdapAuthTester Application & Namespaces
- Utilities Integration Governance
- Operational Utility Catalog
- OpenXML Main Part & Duplicate Tests
- LdapAuthTester CLI Runner & Entrypoint
- OpenXML Archive Part Index
- OpenXML Main Part & Duplicate Tests
- Compound File V3 Tree Fixtures
- MIP Explorer CLI Scan Item Model
- OpenXML Legacy Tenant Suppression Tests
- Shared Services Dependencies
- Label Property Parser & Detail Builder
- Windows CI Build Pipeline
- Explorer Labelable File Extensions
- LDAP CLI Report & Environment Models
- Compound Directory Entry Model
- Local System Diagnostics
- EML & MSG Email Label Readers
- CSV JSON Report Export
- Nocturne Design System
- Checker Stream Failure Recovery Tests
- PNG IDAT Skip & ReadGuard Tests
- PNG Post-Detection Stream Exception Tests
- Shared Service Contracts
- AD Credential CLI Contracts
- Compound File Sector & DIFAT Reader
- LabelCheckResult Equality & Tests
- TIFF IFD & XMP Label Reader
- Checker Non-Seekable Stream Tests
- PNG Sparse Length Seek Guard Tests
- PNG Caller Stream Exception Tests
- ops-toolkit AI-Native SDLC Governance
- Dynamic Deflate & Adler32 Test Fixtures
- PowerShell & LDAP Tool SDLC Docs
- PNG Non-Seekable Stream Tests
- OpenXML Archive Part Index
- Palette Contrast Tests
- PDF Stream Object Resolution
- UTF-8 Log Tail
- MIP Explorer CLI Scan Report Model
- Unreadable Stream Error Handling Tests
- Native Theme Dependencies
- WPF OS Theme Mapping
- LDAP CLI Report & Environment Models
- JPEG Marker & Segment Reader
- Preset Profile Model
- Unslop Browser Snapshot Script
- OpenXML Content Type & Metadata Evidence
- MIP Explorer WPF Application Entry
- CheckState & Elapsed WPF Converters
- JSON Store Resource Lifecycle
- LdapAuthTester CLI Runner & Entrypoint
- Shared Library Development Gates
- OpenXML Relationship & Override Tests
- OpenXML Classification Target Escape Tests
- OpenXML Archive Part Index
- AD Diagnostic Safety Gates
- PSJobScheduler Cron Expression Evaluator
- Authentication Failure Categories
- Explorer Timed Check State Models
- Recent History Model
- OpenXML Unsafe Target Validation Tests
- HTML Plan Git & File Helpers
- Unslop Story Dashboard Progression Visuals
- WinForms OS Theme Mapping
- Explorer Hybrid Startup Tests
- OpenXML Content Type & Metadata Evidence
- MIP Explorer Main Window XAML
- PowerShell Credential Bind Flow
- MIP Label Check Exception Model
- Unslop UI Redesign Skill & References
- WPF Nocturne Resource Styles
- LdapAuthTester WinForms UI Controls
- OpenXML Relationship & Override Tests
- Caveman & html-plan Agent Skills
- HTML Plan OpenAI Skill Metadata
- Beacon Engraving Hero Visual Reference
- Meadow 3D Editorial Footer Reference
- Unslop Ember Dashboard After Screenshot
- Unslop Ember Landing Hero Screenshot
- PSJobScheduler Status Query Script
- Mermaid Preview GitHub Sync
- MIP Label PNG Test Fixture
- Graphify MCP Server Configuration
- MIP Product Features Benchmarks
- Tool Blueprint Template & Copilot Rules
- MIPLabelCustomCheck SDLC & Architecture Docs
- Test Directories & Migration Plan
- Korean Commit Message Convention
- OpsCommon PowerShell Module Docs
- MIPLabelCustomCheck SDLC & Architecture Docs
- LDAP Diagnostic Tool Workflow

## God Nodes (most connected - your core abstractions)
1. `OpenXmlFixture` - 109 edges
2. `OpenXmlLabelReaderTests` - 77 edges
3. `MipLabelChecker` - 73 edges
4. `PngFixture` - 66 edges
5. `PngLabelReaderTests` - 63 edges
6. `MainViewModel` - 58 edges
7. `CompoundFileFixture` - 55 edges
8. `CompoundFileReader` - 50 edges
9. `MainForm` - 48 edges
10. `AdAuthTestResult` - 47 edges

## Surprising Connections (you probably didn't know these)
- `Agent Handoff Manager Skill` --semantically_similar_to--> `Operational Safety & Idempotency Principle`  [INFERRED] [semantically similar]
  .agents/skills/agent-handoff-manager/SKILL.md → AGENTS.md
- `Shared storage and LDAP opt-in integration contract` --references--> `OpsToolkit.Shared library`  [EXTRACTED]
  SPEC.md → src/OpsToolkit.Shared/README.md
- `WhatIf plan-only execution` --conceptually_related_to--> `Atomic corruption-preserving cancellable JSON stores`  [INFERRED]
  scripts/ad/Test-AdCredential/SPEC.md → src/OpsToolkit.Shared/SPEC.md
- `DryRunChecker` --inherits--> `AdAuthChecker`  [EXTRACTED]
  tests/dotnet/LdapAuthTester.Tests/AdAuthCheckerTests.cs → src/LdapAuthTester/Services/AdAuthChecker.cs
- `TestableAdAuthChecker` --inherits--> `AdAuthChecker`  [EXTRACTED]
  tests/dotnet/LdapAuthTester.Tests/AdAuthCheckerTests.cs → src/LdapAuthTester/Services/AdAuthChecker.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **AD diagnostics, reusable services and native design integration** — docs_specs_utilities_integration_plan_contracts, scripts_ad_test_adcredential_readme_test_adcredential, src_opstoolkit_shared_readme_opstoolkit_shared, design_nocturne [EXTRACTED 1.00]
- **html-plan Interactive Claim-Exhibit Planning System** — agents_skills_html_plan_skill_interactive_plan, agents_skills_html_plan_references_blocks_reference, agents_skills_html_plan_examples_scheduled_send_example [EXTRACTED 1.00]
- **MIPLabelCustomCheck 6-Document SDLC Set** — src_miplabelcustomcheck_intent_mip_sensitivity_label_detection, src_miplabelcustomcheck_spec_architecture_spec, src_miplabelcustomcheck_plan_migration_plan, src_miplabelcustomcheck_agents_zero_dependency_rule [EXTRACTED 1.00]
- **ops-toolkit AI-Native SDLC Governance Document Pipeline** — agents_ops_toolkit_governance, plan_implementation_roadmap, claude_claude_code_bridge, gemini_gemini_antigravity_bridge [EXTRACTED 1.00]
- **Three supported AD authentication mechanisms** — src_ldapauthtester_spec_auth_test_method, src_ldapauthtester_spec_directoryentry, src_ldapauthtester_spec_principalcontext, src_ldapauthtester_spec_ldapconnection [EXTRACTED 1.00]
- **Read-only MIP metadata diagnosis output and parser safety** — src_miplabelcustomcheck_readme_core_library, src_miplabelcustomcheck_spec_labelstatus, src_miplabelcustomcheck_spec_protectiontype, src_miplabelcustomcheck_spec_security_limits [EXTRACTED 1.00]
- **Unslop UI Audit and 9-Pass Redesign Skill Suite** — agents_skills_unslop_skill_ui_redesign, agents_skills_unslop_references_signs_audit_catalog, agents_skills_unslop_references_tokens_design_scale, agents_skills_unslop_references_visuals_guide, agents_skills_unslop_references_effects_visual_treatments, agents_skills_unslop_references_refs_examples [EXTRACTED 1.00]
- **Tools Implementing the 6-Document Standard Blueprint** — docs_templates_tool_blueprint_template_six_doc_standard, scripts_iis_remove_expirediiscertificate_readme_tool_overview, scripts_scheduler_psjobscheduler_readme_overview [INFERRED 0.95]

## Communities (161 total, 57 thin omitted)

### Community 0 - "Agent Handoff Manager Script"
Cohesion: 0.07
Nodes (42): atomic_json(), _bad_rel_path(), bucket(), _bullets(), _day(), depth_gaps(), doctor(), ensure_repo() (+34 more)

### Community 1 - "PDF Label Reader & Tests"
Cohesion: 0.11
Nodes (14): Details, LabelDetailViewModel, ActionIdText, ContentBitsText, Id, IdText, MethodText, NameText (+6 more)

### Community 2 - "Compound File Stream Limits"
Cohesion: 0.10
Nodes (5): CompoundFileLabelReaderTests, OlePropertySetBuilder, OlePropertySetReaderTests, Prop, FakeCompoundFileView

### Community 3 - "HTML Plan Interactive Runtime"
Cohesion: 0.07
Nodes (43): applyIfs(), arrowDefs(), askPick(), boot(), buildResponse(), buildToc(), closePop(), closeSheet() (+35 more)

### Community 4 - "MIP Label Checker Core API"
Cohesion: 0.08
Nodes (10): LabelCheckOptions, Default, IncludeDetails, WithDetails, MipLabelChecker, EmailLabelReaderTests, LabelDetailTests, MipLabelCheckerTests (+2 more)

### Community 5 - "MIP Format Reader Infrastructure"
Cohesion: 0.07
Nodes (11): MIPLabelCustomCheck.Tests.TestData, MIPLabelCustomCheck.Internal.Pdf, MIPLabelCustomCheck.Internal, MIPLabelCustomCheck.Internal.CompoundFile, MIPLabelCustomCheck.Tests, OlePropertySetStatus, NotPresent, Success (+3 more)

### Community 6 - "MIP Results And Benchmarks"
Cohesion: 0.05
Nodes (15): MIPLabelCustomCheck.Benchmarks, LabelCheckerBenchmarks, LargeSparsePngFile, Signature, SparseFileNativeMethods, LabelCheckResult, Details, IsLabeled (+7 more)

### Community 7 - "LDAP And MIP Specifications"
Cohesion: 0.05
Nodes (41): LdapAuthTester AGENTS.md, LdapAuthTester INTENT.md, art.CNR.LdapAuthTest legacy .NET Framework tool, AdAuthChecker service, AdAuthModels requests/results/environment models, CliOptions / CliRunner, LdapAuthTester PLAN.md, InspectLdapsCertificateAsync and LdapsCertificateInfo (+33 more)

### Community 8 - "OLE Property Set Decoding"
Cohesion: 0.10
Nodes (10): OlePropertySetReader, ValueDecodeOutcome, CodePageUnsupported, Skip, Success, Crc32, BitReader, AlignedBytePosition (+2 more)

### Community 9 - "Explorer Main ViewModel & Commands"
Cohesion: 0.06
Nodes (26): MainViewModel, DefaultStatusFilters, Drives, ExportCsvCommand, ExtensionCount, Files, FilteredFiles, IncludeDetailsByDefault (+18 more)

### Community 10 - "PDF Object & Trailer Resolver"
Cohesion: 0.12
Nodes (9): LabelPropertyParseResult, PdfXrefEntry, PdfObjectResolver, LatestTrailer, PdfDictionary, PdfReference, PdfValue, PdfLabelReader (+1 more)

### Community 11 - "File Signature Detection"
Cohesion: 0.05
Nodes (29): FileFormat, CompoundFile, Eml, GenericPFile, Jpeg, Pdf, Png, Tiff (+21 more)

### Community 12 - "SafeXml & XMP Label Reader"
Cohesion: 0.07
Nodes (16): LimitedReadStream, CanRead, CanSeek, CanWrite, Length, Position, SafeXml, LimitedReadStreamTests (+8 more)

### Community 13 - "MIP Explorer Main Window XAML"
Cohesion: 0.05
Nodes (42): ActionIdText, Children, ContentBitsText, DataContext.OpenFileCommand, DataContext.RemeasureCommand, DataContext.RemeasureWithDetails, Details, DisplayName (+34 more)

### Community 14 - "PDF Value Model Types"
Cohesion: 0.12
Nodes (7): PdfArray, PdfBoolean, PdfName, PdfNull, PdfNumber, PdfString, PdfLexerTests

### Community 16 - "Compound File Binary Fixture Builder"
Cohesion: 0.10
Nodes (18): CompoundFileFixture, Bytes, DifatSectorId, DirectorySectorCount, FatSectorCount, FatSectorId, FirstDirectorySectorId, LastDirectorySectorId (+10 more)

### Community 17 - "Compound File Stream Limits"
Cohesion: 0.09
Nodes (8): CompoundFileLabelReader, CompoundFileReaderView, ICompoundFileView, LegacyLabels, None, CompoundFileReaderView, MsgLabelReader, NamedPropertyMapping

### Community 18 - "OpenXML Relationship & Override Tests"
Cohesion: 0.09
Nodes (4): Inputs, OpenXmlFixture, MainDocumentContentType, MainDocumentPath

### Community 20 - "PNG Chunk Validation Tests"
Cohesion: 0.10
Nodes (3): Chunk, PngFixture, PngFixtureData

### Community 21 - "PSJobScheduler Core Engine"
Cohesion: 0.12
Nodes (30): Clear-PendingStateFields(), Convert-TodaysLogsToUtf8(), ConvertTo-LocalDateTimeOrNull(), ConvertTo-NormalizedDowToken(), Expand-RangeList(), Get-ActiveLockCount(), Get-CompletionPath(), Get-JobKey() (+22 more)

### Community 22 - "LDAP Auth Result Models"
Cohesion: 0.09
Nodes (23): AdAuthTestResult, CertificateInfo, CertificateValidationBypassed, DiagnosticDetail, ElapsedMilliseconds, Endpoint, FailureCategory, IsCredentialFailure (+15 more)

### Community 23 - "PDF Lexer & Indirect Objects"
Cohesion: 0.20
Nodes (3): PdfIndirectObject, PdfLexer, Position

### Community 25 - "Explorer Scan Session & Progress"
Cohesion: 0.14
Nodes (8): ScanProgress, ScanSession, ScanSummary, RecordingProgress, CollectingProgress, Reports, IgnoringProgress, ScanSessionTests

### Community 26 - ".NET 10 Solution & Project Structure"
Cohesion: 0.09
Nodes (29): BenchmarkDotNet (0.15.8), System.DirectoryServices.AccountManagement (10.0.0), System.DirectoryServices.Protocols (10.0.0), net10.0-windows, Microsoft.NET.Sdk, LdapAuthTester, MIPLabelCustomCheck.Benchmarks, MIPLabelCustomCheck.Explorer (+21 more)

### Community 27 - "LdapAuthTester CLI Options"
Cohesion: 0.07
Nodes (28): CliOptions, Domain, EnvOnly, IncludeUserInfo, IsDryRun, LaunchGui, LdapPort, LdapsPort (+20 more)

### Community 28 - "HTML Plan Packager Script"
Cohesion: 0.06
Nodes (24): argv, baseDir, blockSrc(), errors, FENCE, here, html, ids (+16 more)

### Community 29 - "Explorer Timed Check State Models"
Cohesion: 0.09
Nodes (18): TimedCheckResult, IsSuccess, FileEntryViewModel, Elapsed, Error, FilePath, HasDetails, LabelText (+10 more)

### Community 30 - "Unslop Story Frame Capture Script"
Cohesion: 0.06
Nodes (22): args, chrome, file, frame, frames, [frameX, frameY, frameWidth], html, [input, out] (+14 more)

### Community 31 - "PDF Cross-Reference & Flate Decoder"
Cohesion: 0.14
Nodes (8): PdfCrossReferenceReader, StartXrefKeyword, PdfCrossReferenceTable, PdfXrefEntryType, Compressed, Free, InUse, XrefSection

### Community 32 - "MSG Compound Directory Fixture"
Cohesion: 0.13
Nodes (11): DirectoryEntry, Child, FullPath, Id, Name, Right, Size, StartSector (+3 more)

### Community 33 - "LDAP Typed Settings Storage"
Cohesion: 0.15
Nodes (9): LdapServerSettings, Domain, LdapPort, LdapsPort, TestMethod, TestTarget, TimeoutSeconds, LdapSettingsStore (+1 more)

### Community 34 - "MIP Explorer ViewModels & Scanning"
Cohesion: 0.14
Nodes (5): MIPLabelCustomCheck.Explorer.ViewModels, MIPLabelCustomCheck.Explorer.Tests, MIPLabelCustomCheck.Explorer.Scanning, FileEntryViewModelTests, FixturePaths

### Community 36 - "JPEG APP1 XMP Reader & Fixtures"
Cohesion: 0.18
Nodes (3): JpegFixture, JpegTiffLabelReaderTests, TiffFixture

### Community 37 - "PNG Label Reader & Decompression"
Cohesion: 0.23
Nodes (6): ChunkHeader, PngLabelReader, PngSignature, XmpKeyword, PngStructureException, XmpPacket

### Community 38 - "MIP Explorer CLI Options"
Cohesion: 0.11
Nodes (15): CliOptions, AllFiles, IsDryRun, LaunchGui, OutputCsvPath, OutputJson, OutputJsonPath, Recursive (+7 more)

### Community 39 - "Explorer App Startup & TimedChecker"
Cohesion: 0.27
Nodes (3): TimedChecker, TimedCheckerTests, TimedCheckerWarmupTests

### Community 40 - "EML & MSG Email Label Readers"
Cohesion: 0.19
Nodes (4): OlePropertySetReadResult, EmlLabelReader, LabelReadResult, XmpLabelReader

### Community 41 - "Explorer Folder Tree ViewModel"
Cohesion: 0.15
Nodes (8): FolderTreeNodeViewModel, Children, DisplayName, IsAccessible, IsExpanded, IsSelected, Path, FolderTreeNavigationTests

### Community 42 - "Compound File Stream Tracking Tests"
Cohesion: 0.11
Nodes (7): TrackingStream, CanRead, CanSeek, CanWrite, IsDisposed, Length, Position

### Community 43 - "LabelInfo XML Record Reader"
Cohesion: 0.20
Nodes (8): LabelInfoReadResult, LabelInfoRecord, ActionId, ContentBits, Method, Name, SetDate, LabelInfoXmlReaderTests

### Community 44 - "Label Property Parser & Detail Builder"
Cohesion: 0.10
Nodes (16): MIPLabelCustomCheck, LabelDetail, ActionId, ContentBits, Id, Method, Name, SetDate (+8 more)

### Community 47 - "LdapAuthTester Target & Request Models"
Cohesion: 0.11
Nodes (17): AdAuthTestRequest, Domain, IncludeUserInfo, IsDryRun, LdapPort, LdapsPort, Password, SkipCertificateValidation (+9 more)

### Community 49 - "LdapAuthTester Application & Namespaces"
Cohesion: 0.19
Nodes (5): LdapAuthTester, LdapAuthTester.Cli, LdapAuthTester.Models, LdapAuthTester.Tests, LdapAuthTester.Services

### Community 50 - "Utilities Integration Governance"
Cohesion: 0.12
Nodes (4): Utilities integration implementation plan, Project purpose and operational intent, Quality gates and Utilities review, Technical architecture and interface contracts

### Community 51 - "Operational Utility Catalog"
Cohesion: 0.14
Nodes (12): LdapAuthTester completed utility, MIPLabelCustomCheck completed utility, PSJobScheduler completed utility, Publish-SingleFileTools, Remove-ExpiredIISCertificate completed utility, .NET 10, ops-toolkit repository and catalog, OpsCommon planned PowerShell module (+4 more)

### Community 53 - "LdapAuthTester CLI Runner & Entrypoint"
Cohesion: 0.18
Nodes (4): CliRunner, Program, CliOptionsAndRunnerTests, StubProfileAndCertChecker

### Community 54 - "OpenXML Archive Part Index"
Cohesion: 0.19
Nodes (5): ContentTypeEvidence, Invalid, OpenXmlLabelReader, RelationshipEvidence, Invalid

### Community 55 - "OpenXML Main Part & Duplicate Tests"
Cohesion: 0.20
Nodes (4): OpenXmlLabelReaderTests, UnsafeClassificationTargets, UnsafeMainOverridePartNames, UnsafeOfficeDocumentTargets

### Community 56 - "Compound File V3 Tree Fixtures"
Cohesion: 0.21
Nodes (6): DirectorySpec, Child, Left, Name, Right, Type

### Community 58 - "MIP Explorer CLI Scan Item Model"
Cohesion: 0.13
Nodes (13): CliFileScanItem, Details, ElapsedMilliseconds, ErrorMessage, FilePath, FileSizeBytes, IsDryRun, LabelIds (+5 more)

### Community 60 - "Shared Services Dependencies"
Cohesion: 0.17
Nodes (3): OpsToolkit.Shared.Models, OpsToolkit.Tests, OpsToolkit.Shared.Services

### Community 61 - "Label Property Parser & Detail Builder"
Cohesion: 0.15
Nodes (6): LabelInfoXmlReader, DetailBuilder, LabelMethod, Privileged, Standard, Unspecified

### Community 62 - "Windows CI Build Pipeline"
Cohesion: 0.12
Nodes (15): runs-on, steps, jobs, build-and-test, name, on, pull_request, push (+7 more)

### Community 63 - "Explorer Labelable File Extensions"
Cohesion: 0.18
Nodes (3): LabelableExtensions, Default, LabelableExtensionsTests

### Community 64 - "LDAP CLI Report & Environment Models"
Cohesion: 0.13
Nodes (13): AdAuthCliReport, Environment, IsDryRun, OverallSuccess, Results, Timestamp, AdEnvironmentInfo, CurrentLogonUser (+5 more)

### Community 65 - "Compound Directory Entry Model"
Cohesion: 0.13
Nodes (14): CompoundDirectoryEntry, ChildId, Id, LeftSiblingId, Name, RightSiblingId, StartSector, StreamSize (+6 more)

### Community 66 - "Local System Diagnostics"
Cohesion: 0.17
Nodes (11): SystemDiagnosticsInfo, AvailableMemoryBytes, CurrentProcessMemoryBytes, DotNetRuntimeVersion, MachineName, OsArchitecture, OsVersion, ProcessorCount (+3 more)

### Community 68 - "EML & MSG Email Label Readers"
Cohesion: 0.13
Nodes (11): PFileProtectionReader, LabelStatus, InvalidFile, Labeled, ProtectedLabelUnknown, Unlabeled, Unsupported, ProtectionType (+3 more)

### Community 69 - "CSV JSON Report Export"
Cohesion: 0.18
Nodes (11): ReportItem, ErrorMessage, FileName, FilePath, FileSizeBytes, Id, ProcessedAt, ProgressPercent (+3 more)

### Community 70 - "Nocturne Design System"
Cohesion: 0.22
Nodes (3): Nocturne design contract, Nocturne static component reference, Nocturne design kit guide

### Community 71 - "Checker Stream Failure Recovery Tests"
Cohesion: 0.16
Nodes (6): ThrowingAfterDetectionStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 72 - "PNG IDAT Skip & ReadGuard Tests"
Cohesion: 0.16
Nodes (7): ReadGuardStream, CanRead, CanSeek, CanWrite, Length, Position, ReadGuardRange

### Community 73 - "PNG Post-Detection Stream Exception Tests"
Cohesion: 0.16
Nodes (6): ThrowingAfterDetectionReadStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 74 - "Shared Service Contracts"
Cohesion: 0.27
Nodes (3): OpsToolkit.Shared migrated .NET services, OpsToolkit.Shared user guide, OpsToolkit.Shared specification

### Community 75 - "AD Credential CLI Contracts"
Cohesion: 0.19
Nodes (3): Test-AdCredential integration, Test-AdCredential user guide, Test-AdCredential specification

### Community 78 - "TIFF IFD & XMP Label Reader"
Cohesion: 0.31
Nodes (4): TiffLabelReader, TiffStructureException, InvalidXmpPacketException, XmpPacketLabelReader

### Community 79 - "Checker Non-Seekable Stream Tests"
Cohesion: 0.15
Nodes (6): NonSeekableStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 80 - "PNG Sparse Length Seek Guard Tests"
Cohesion: 0.17
Nodes (7): SparseLengthStream, CanRead, CanSeek, CanWrite, LargeSeekAttempted, Length, Position

### Community 81 - "PNG Caller Stream Exception Tests"
Cohesion: 0.15
Nodes (6): ThrowingInvalidDataReadStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 82 - "ops-toolkit AI-Native SDLC Governance"
Cohesion: 0.20
Nodes (6): AGENTS.md — AI Coding Agent Guidelines for ops-toolkit, Agent Handoff Manager Skill, 2-Tier .NET Solution Architecture (Tool .sln + Root OpsToolkit.slnx), CLAUDE.md — Claude Code Instructions Bridge, GEMINI.md — Gemini CLI & Antigravity Instructions Bridge, Implementation roadmap

### Community 83 - "Dynamic Deflate & Adler32 Test Fixtures"
Cohesion: 0.33
Nodes (3): DeflateBitWriter, DynamicDeflateFixture, HuffmanCode

### Community 84 - "PowerShell & LDAP Tool SDLC Docs"
Cohesion: 0.20
Nodes (8): Remove-ExpiredIISCertificate Implementation Plan, Remove-ExpiredIISCertificate Tool Overview, Remove-ExpiredIISCertificate Quality & Safety Review, Remove-ExpiredIISCertificate Specification & Status Codes, PSJobScheduler Implementation Plan, PSJobScheduler Cron-Safe Job Scheduler Overview, PSJobScheduler Quality & Safety Review, PSJobScheduler Architecture & State Specification

### Community 85 - "PNG Non-Seekable Stream Tests"
Cohesion: 0.17
Nodes (6): NonSeekableReadStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 87 - "Palette Contrast Tests"
Cohesion: 0.20
Nodes (7): assert, contrast(), css, fs, luminance(), path, { test }

### Community 89 - "UTF-8 Log Tail"
Cohesion: 0.29
Nodes (3): LogTailService, SharedServicesTests, Store

### Community 90 - "MIP Explorer CLI Scan Report Model"
Cohesion: 0.18
Nodes (11): CliScanReport, ErrorCount, IsDryRun, Items, LabeledCount, ProtectedUnknownCount, TargetPath, Timestamp (+3 more)

### Community 91 - "Unreadable Stream Error Handling Tests"
Cohesion: 0.18
Nodes (6): UnreadableStream, CanRead, CanSeek, CanWrite, Length, Position

### Community 95 - "LDAP CLI Report & Environment Models"
Cohesion: 0.20
Nodes (10): LdapsCertificateInfo, ChainStatusDetails, DaysUntilExpiration, IsExpired, Issuer, NotAfter, NotBefore, SslPolicyErrors (+2 more)

### Community 96 - "JPEG Marker & Segment Reader"
Cohesion: 0.58
Nodes (3): JpegLabelReader, XmpIdentifier, JpegStructureException

### Community 97 - "Preset Profile Model"
Cohesion: 0.20
Nodes (8): PresetProfile, CreatedAt, Description, Id, IsDefault, Name, SettingsJson, UpdatedAt

### Community 98 - "Unslop Browser Snapshot Script"
Cohesion: 0.22
Nodes (3): args, chrome, positionals

### Community 100 - "MIP Explorer WPF Application Entry"
Cohesion: 0.25
Nodes (3): MIPLabelCustomCheck.Explorer.Cli, MIPLabelCustomCheck.Explorer, Program

### Community 103 - "LdapAuthTester CLI Runner & Entrypoint"
Cohesion: 0.22
Nodes (9): AdUserProfileInfo, Department, DisplayName, DistinguishedName, Email, MemberOfGroups, SamAccountName, Title (+1 more)

### Community 104 - "Shared Library Development Gates"
Cohesion: 0.22
Nodes (4): OpsToolkit.Shared agent instructions, OpsToolkit.Shared intent, OpsToolkit.Shared implementation plan, OpsToolkit.Shared review

### Community 108 - "AD Diagnostic Safety Gates"
Cohesion: 0.25
Nodes (4): Test-AdCredential agent instructions, Test-AdCredential intent, Test-AdCredential implementation plan, Test-AdCredential review

### Community 109 - "PSJobScheduler Cron Expression Evaluator"
Cohesion: 0.39
Nodes (6): ConvertTo-NormalizedDowToken(), Expand-RangeList(), Get-NextCronTime(), New-CronMatcher(), Test-CronMatch(), Test-SetContains()

### Community 110 - "Authentication Failure Categories"
Cohesion: 0.25
Nodes (8): AuthFailureCategory, Credential, NetworkOrServer, None, Skipped, Timeout, Tls, Unknown

### Community 111 - "Explorer Timed Check State Models"
Cohesion: 0.25
Nodes (7): CheckState, Canceled, Completed, Failed, Pending, Running, Skipped

### Community 112 - "Recent History Model"
Cohesion: 0.25
Nodes (8): RecentHistoryItem, ActionType, DisplayName, Id, IsSuccess, Note, Path, Timestamp

### Community 114 - "HTML Plan Git & File Helpers"
Cohesion: 0.33
Nodes (7): err(), findFile(), git(), gitShow(), inside(), real(), stamp()

### Community 120 - "PowerShell Credential Bind Flow"
Cohesion: 0.52
Nodes (5): Get-AdFailureKind(), Invoke-AdCredentialBind(), Invoke-AdCredentialCheck(), Resolve-AdEndpoint(), Test-AdTcpConnection()

### Community 122 - "MIP Label Check Exception Model"
Cohesion: 0.33
Nodes (5): MipLabelCheckErrorReason, FileLocked, MipLabelCheckException, FilePath, Reason

### Community 123 - "Unslop UI Redesign Skill & References"
Cohesion: 0.40
Nodes (6): Unslop Code Effects Reference (Glyphs, Halftone, Dithering, Duotone), Unslop Finished Screen References Catalog, Unslop Signs of Slop Audit Catalog, Unslop Design Tokens Reference (Spacing, Neutral Ramps, Type, Radius, Motion), Unslop Hero & Moment Visuals Guide, Unslop Skill — 9-Pass UI Redesign & Craft Workflow

### Community 124 - "WPF Nocturne Resource Styles"
Cohesion: 0.33
Nodes (5): State, IsSelected, Outline, ResourceDictionary, Border

### Community 125 - "LdapAuthTester WinForms UI Controls"
Cohesion: 0.40
Nodes (3): ComboBoxItem, Display, Value

### Community 126 - "OpenXML Relationship & Override Tests"
Cohesion: 0.40
Nodes (4): OpenXmlDocumentKind, Excel, PowerPoint, Word

### Community 127 - "Caveman & html-plan Agent Skills"
Cohesion: 0.50
Nodes (5): Caveman Skill README & Sibling Modes (caveman, ultracave, megacave), Caveman Terse Voice Skill Specification, Scheduled Send in PostBox — Example html-plan Document, html-plan Custom Element Blocks Reference (doc-plan, doc-claim, doc-mock, doc-machine, doc-calls, doc-schema, doc-code, doc-ask), html-plan Interactive Claim-Tree Implementation Plan Skill

### Community 128 - "HTML Plan OpenAI Skill Metadata"
Cohesion: 0.40
Nodes (4): interface, default_prompt, display_name, short_description

### Community 129 - "Beacon Engraving Hero Visual Reference"
Cohesion: 0.50
Nodes (3): Monochromatic Cobalt Blue Woodcut/Engraving Lighthouse Illustration, High-Contrast Editorial Serif Headline Typography, Beacon Incident Monitoring Hero Screen (Engraving Style)

### Community 130 - "Meadow 3D Editorial Footer Reference"
Cohesion: 0.50
Nodes (4): Quiet Place for a Loud Internet Brand Positioning, Oversized Wordmark Behind 3D Rolling Grass Foreground, Meadow 3D Footer & Hero Reference Screen, Subsurface-Scattered 3D Geometric Crystalline Forms in Grass

### Community 131 - "Unslop Ember Dashboard After Screenshot"
Cohesion: 0.50
Nodes (4): KPI Summary Cards with 3D Card Visual and Tabular Metrics, Multi-Plan Revenue Spline Chart with Mirrored Baseline, Unslop Dashboard After Reference Screenshot (Ember Overview), Dark Sidebar Navigation with Search and Tiered Plans

### Community 132 - "Unslop Ember Landing Hero Screenshot"
Cohesion: 0.50
Nodes (4): ASCII Dithered Paper Airplane Hero Illustration with Status Callouts, Direct Product Copywriting (Invoices that chase themselves), Ember Financial Overview Dashboard Preview, Ember Landing Page After Unslop Screenshot

### Community 135 - "MIP Label PNG Test Fixture"
Cohesion: 0.67
Nodes (3): Hand-Drawn Checkmark / V-Shaped Test Graphic, MIP Label Sample PNG Fixture Image, Microsoft Information Protection (MIP) Sensitivity Label Test Fixture Concept

### Community 139 - "MIP Product Features Benchmarks"
Cohesion: 0.67
Nodes (3): MIP Label.pdf Test Fixture, LabelCheckerBenchmarks Suite, LargeSparsePngFile 512 MiB Benchmark Generator

## Knowledge Gaps
- **519 isolated node(s):** `graphify-mcp`, `OpsToolkit.Shared`, `LaunchGui`, `ShowHelp`, `ShowVersion` (+514 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 868 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **57 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FileEntryViewModel` connect `Explorer Timed Check State Models` to `PDF Label Reader & Tests`, `MIP Explorer ViewModels & Scanning`, `MIP Results And Benchmarks`, `EML & MSG Email Label Readers`, `Explorer Main ViewModel & Commands`, `Label Property Parser & Detail Builder`, `Explorer Timed Check State Models`, `PDF Stream Object Resolution`, `Explorer Scan Session & Progress`?**
  _High betweenness centrality (0.047) - this node is a cross-community bridge._
- **Are the 60 inferred relationships involving `OpenXmlFixture` (e.g. with `OpenXmlLabelReaderTests.Read_accepts_case_equivalent_main_part_entry()` and `OpenXmlLabelReaderTests.Read_accepts_later_segment_colon_main_target()`) actually correct?**
  _`OpenXmlFixture` has 60 INFERRED edges - model-reasoned connections that need verification._
- **What connects `graphify-mcp`, `OpsToolkit.Shared`, `LaunchGui` to the rest of the system?**
  _519 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Agent Handoff Manager Script` be split into smaller, more focused modules?**
  _Cohesion score 0.07071455016660497 - nodes in this community are weakly interconnected._
- **Why does `MainViewModel` connect `Explorer Main ViewModel & Commands` to `MIP Explorer ViewModels & Scanning`, `Explorer App Startup & TimedChecker`, `EML & MSG Email Label Readers`, `Explorer Folder Tree ViewModel`, `LdapAuthTester WinForms UI Controls`, `Explorer Hybrid Startup Tests`, `Explorer Scan Session & Progress`, `MIP Explorer CLI Scan Item Model`, `Explorer Timed Check State Models`, `Explorer Labelable File Extensions`?**
  _High betweenness centrality (0.038) - this node is a cross-community bridge._
- **Are the 64 inferred relationships involving `MipLabelChecker` (e.g. with `App.OnStartup()` and `Program.Main()`) actually correct?**
  _`MipLabelChecker` has 64 INFERRED edges - model-reasoned connections that need verification._
- **Should `PDF Label Reader & Tests` be split into smaller, more focused modules?**
  _Cohesion score 0.11032863849765258 - nodes in this community are weakly interconnected._
## Extraction integrity and scope limits

- Raw extraction diagnostics: 215 dangling endpoint edges, 0 missing endpoint fields, 7 self loops, 297 same-endpoint edge collapses. The builder resolves/filters references and uses an undirected graph; these warnings mean some raw relationships are not represented independently.
- Detector excluded design-system/tokens.css under its sensitive-file filter. Fixtures/MIP Label.docx and Fixtures/MIP Label.xlsx could not be converted because the optional Office dependency is unavailable. These files were not re-extracted.
- AST extraction reported 14 code files with no symbols. Semantic host agents fully read all 31 dispatched changed documents; 45 unchanged semantic files reused cached results.
- A first export was refused by the semantic-loss guard. Missing still-valid catalog/specification concepts were reconciled from current documents, then the supported source-aware merge succeeded without force. Removed CheckStateToBrushConverter.cs was pruned.
- Archived handoffs are local bookkeeping; this graph generation does not install a watch process or commit hook.
