# AGENTS.md — MIPLabelCustomCheck

이 도구는 루트 [AGENTS.md](../../AGENTS.md)의 규칙을 따릅니다.
코드를 수정하거나 기능을 추가하기 전에 현재 폴더의 [INTENT.md](./INTENT.md), [SPEC.md](./SPEC.md), [PLAN.md](./PLAN.md), [REVIEW.md](./REVIEW.md)를 먼저 확인하세요.

---

## 로컬 빌드, 실행 및 테스트 명령어

```powershell
# 1. 도구 전용 솔루션(MIPLabelCustomCheck.sln) 전체 빌드 (경고를 오류로 처리)
dotnet build .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.sln -warnaserror

# 2. 단위 테스트 실행 (코어 파서 + Explorer ViewModel/CLI 테스트)
dotnet test .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.sln --nologo

# 3. WPF Explorer GUI 실행 (인자 없음)
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj

# 4. CLI 모드 실행 (샘플 픽스처 폴더 스캔 및 Dry-Run 검증)
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj -- --path .\src\MIPLabelCustomCheck\Fixtures --details
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj -- --path .\src\MIPLabelCustomCheck\Fixtures --dry-run

# 5. BenchmarkDotNet 성능 측정 실행 (Release 모드)
dotnet run -c Release --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Benchmarks\MIPLabelCustomCheck.Benchmarks.csproj
```

---

## 도구별 특수 주의사항 (Domain Rules)
1. **코어 라이브러리 무의존성(Zero-Dependency) 유지**: `src/MIPLabelCustomCheck/MIPLabelCustomCheck/MIPLabelCustomCheck.csproj`에는 어떠한 외부 NuGet 패키지도 추가하지 마세요.
2. **원본 파일 읽기 전용 보장**: 검사 대상 파일을 열 때는 항상 `FileAccess.Read` 및 `FileShare.ReadWrite | FileShare.Delete`만 사용하며, 원본 파일을 절대 수정하거나 잠그지 마세요.
3. **보안 한계(Security Limits) 준수**: `LimitedReadStream`, `SafeXml`(`DtdProcessing.Prohibit`), `DeflateFramingValidator` 등 악성/손상 파일 방어 로직을 우회하지 마세요.
