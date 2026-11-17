# 남은 이름 바꾸기 — 21일 배포 뒤에

제품 이름은 **CamdiansRX**로 바뀌었고, 약국이 보거나 약국 PC에 남는 것은 전부
2026-11-17에 바꿨다. 이 문서는 **아직 `CamPOS`인 것**의 목록이다.

## 왜 미뤘나

남은 것은 전부 **약국에 보이지 않는다** — 네임스페이스, 프로젝트 폴더, 솔루션
파일 이름. 1,347줄 / 286파일이고, 바꿔도 화면·파일·설치본은 한 글자도 달라지지
않는다. 배포 나흘 전에 그만한 면적을 건드리는 것은 위험만 사고 얻는 것이 없다.

그 대가는 **두 이름이 어긋난 상태로 배포된다**는 것이다. 예외 로그에는
`CamPOS.Application.Sync.SyncService`가 찍히는데 제목 표시줄에는 CamdiansRX가
있다. 알고 있는 상태이지 실수가 아니다.

---

## 남은 목록

| 무엇 | 지금 | 나중에 | 개수 |
|---|---|---|---|
| C# 네임스페이스 | `CamPOS.Domain` `CamPOS.Application` `CamPOS.DataAccess` `CamPOS.Wpf` `CamPOS.Tests` | `CamdiansRX.*` | 822줄 |
| XAML `x:Class`·`clr-namespace` | 같음 | 같음 | 64줄 |
| 형식 참조 (`using`, 전체 이름) | 같음 | 같음 | 461줄 |
| 프로젝트 폴더·`.csproj` | `CamPOS.Wpf/CamPOS.Wpf.csproj` 등 5개 | `CamdiansRX.*` | 5 |
| 솔루션 | `CamPOS.slnx` | `CamdiansRX.slnx` | 1 |
| 저장소 폴더 | `source/repos/PharmaPOS` | 정하기 | 1 |
| 개발 문서 | `README.md`, `docs/manual-*.md`, `docs/optimization-audit.md`, `supabase/*.sql` 머리글 | 본문의 CamPOS | ~40줄 |

## 바꾸지 않는 것 — 영구

| 무엇 | 이유 |
|---|---|
| `PharmaPOS.Application.Licensing` 네임스페이스 | `campos-license-issuer` 저장소와 **바이트까지 같아야** 한다. 한쪽만 바꾸면 이후 모든 diff가 빨개지고 진짜 드리프트가 그 안에 숨는다 |
| `%APPDATA%\PharmaPOS.Issuer` | 발급 도구의 개인 키 폴더. 다른 저장소의 것이고 여기서 건드리지 않는다 |
| Supabase 표·함수·`campos_metrics` | 이름으로 불리는 배포된 코드다. 바꾸면 모든 약국의 동기화가 멈추고, 얻는 것은 아무도 읽지 않을 글자뿐 |
| `AppDataMigration`의 옛 폴더 이름 | 찾으려면 옛 이름을 알아야 한다 |

---

## 할 때의 순서

1. **배포가 안정된 뒤에.** 약국이 2.0을 받고 일주일쯤 지난 다음
2. 폴더·`.csproj`·`.slnx` 이름부터 (`git mv`)
3. 네임스페이스는 IDE의 리팩터링으로 한 번에
4. `PharmaPOS.Application.Licensing`이 **그대로인지** 확인 — 리팩터링이 같이
   바꿔 버리기 쉽다. `license-vectors.json` 테스트가 깨지면 그걸 건드린 것이다
5. `dotnet build` · `dotnet test` · 앱 실행
6. **`deploy.ps1`의 `--packDir` 경로**가 프로젝트 폴더 이름을 쓰고 있으면 같이

> 네임스페이스가 바뀌어도 **약국 PC의 데이터는 영향받지 않는다.** 폴더 이름도
> DB 파일 이름도 이미 CamdiansRX이고, 그것들은 네임스페이스와 무관하다.

---

*작성 2026-11-17 · CamdiansRX v2.1*
