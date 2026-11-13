# Agent instructions

Read root AGENTS.md and local INTENT/SPEC/PLAN/REVIEW before editing. Keep net10.0 BCL-only; no WinUI, CommunityToolkit, or UI bindings. Use explicit paths and propagate cancellation. Never swallow JSON errors or silently replace corrupt stores. Caller owns secret filtering and dry-run policy. Run focused project build, test, and dotnet format --verify-no-changes. Do not probe production systems in tests.
