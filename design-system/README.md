# ops-toolkit Nocturne

Utilities의 `DESIGN.md` 및 `design-system/tokens.css`, `components.css`를 2026-11-13에 고정 복사했다. Modubit Foundation Nocturne CSS/UI Kit 및 `modubit.tokens.json`에서 유래한다. 원본 LICENSE는 발견하지 못했으며 별도 라이선스를 주장하지 않는다.

## 사용

`index.html`을 브라우저로 열면 OS 라이트·다크 설정을 따르는 정적 컴포넌트 참조를 볼 수 있다. 입력·버튼은 스타일 예시이고 실제 인증, 파일 읽기·수정·업로드는 실행하지 않는다. Utilities의 `preview.js` 시뮬레이션은 포함하지 않았다.

웹 도구는 `tokens.css`와 `components.css`를 자체 배포에 함께 포함한다. 외부 폰트나 원본 저장소 경로를 런타임에 요구하지 않는다. 네이티브 앱은 CSS를 로드하지 않는다.

| 대상 | 적용 파일 |
|---|---|
| LDAP WinForms | `src/LdapAuthTester/NativeTheme.cs` |
| MIP Explorer WPF | `src/MIPLabelCustomCheck/MIPLabelCustomCheck.Explorer/Themes/Nocturne.xaml`, `Themes/NativeTheme.cs` |
| 디자인 계약 | 루트 `DESIGN.md` |

네이티브 OS 고대비·포커스·입력 동작을 우선한다. 드롭다운·체크박스·스크롤바 등 OS 템플릿 영역은 플랫폼 렌더링을 유지한다. WPF/WinForms의 네이티브 크기와 DPI 규칙을 유지하며 웹의 44px 조작 높이를 모든 데스크톱 행에 강제하지 않는다.

## 검증

```powershell
node --test design-system/tests/*.test.cjs
```

대비 검사는 토큰 색상 쌍을 확인한다. 실제 네이티브 컨트롤 렌더링, 스크린리더, 고대비 및 다양한 DPI 검증을 대체하지 않는다. 각 도구의 `REVIEW.md`에 실행 결과와 미확인 항목을 기록한다.
