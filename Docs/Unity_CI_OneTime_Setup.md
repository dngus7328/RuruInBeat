# Unity 자동 테스트 최초 설정 (한 번만)

프로젝트 버전: **Unity 2022.3.62f3**. PC용 공용 플레이 씬은 **Assets/Scene/Mouse.unity**.

이 저장소에는 `.github/workflows/unity-editor-tests.yml`과 Unity EditMode 테스트 3개가 이미 추가되어 있습니다. 계정 정보는 저장소 파일이나 이슈/채팅에 넣지 마세요.

## 1. Unity Hub에서 Personal 라이선스 확인

Windows에서 Unity Hub → Preferences(설정) → Licenses(라이선스) → Add를 엽니다. 무료 라이선스 사용자는 **Get a free personal license**를 선택하여 활성화합니다.

그다음 Windows 탐색기 주소창에 `C:\ProgramData\Unity\` 를 입력하여 `Unity_lic.ulf`가 존재하는지 확인합니다. (파일이 없으면 Unity Hub에서 Personal 라이선스 활성화를 다시 확인합니다.)

> Unity Pro 사용자라면 Personal용 파일 대신 본인에게 발급된 유효한 시리얼을 사용합니다. 반드시 본인의 라이선스 정책을 따르세요.

## 2. GitHub 저장소 Secrets 등록

[Repository Actions Secrets](https://github.com/dngus7328/RuruInBeat/settings/secrets/actions)에 접속합니다.

**New repository secret**으로 다음 항목을 만듭니다.

- `UNITY_LICENSE`: Windows `C:\ProgramData\Unity\Unity_lic.ulf` 파일을 메모장으로 열고 **파일 전체 내용**을 붙여 넣습니다. (Personal 라이선스에 해당)
- `UNITY_EMAIL`: Unity Hub에 로그인한 계정 이메일
- `UNITY_PASSWORD`: Unity 계정 비밀번호

Pro 라이선스를 사용하는 경우 `UNITY_SERIAL`도 추가합니다. GitHub Actions에서만 접근 가능한 **Secrets**에 입력하고, 일반 Variables나 소스 코드에 절대 넣지 마세요.

## 3. 테스트 활성화

같은 GitHub 메뉴의 **Variables** 탭으로 이동합니다. **New repository variable**을 누른 뒤:

- Name: `UNITY_CI_ENABLED`
- Value: `true`

로 등록합니다. 이 변수는 비밀 정보가 아닙니다. 이 값을 설정하기 전에는 workflow가 **skipped**로 표시되며 Unity 인증 없이 실패하도록 방치하지 않습니다.

## 4. 최초 수동 실행

[GitHub Actions](https://github.com/dngus7328/RuruInBeat/actions/workflows/unity-editor-tests.yml) → `Run workflow` → `main` → `Run workflow`.

실행 후 `Unity 2022.3 EditMode tests` 작업을 열어 실패한 단계와 로그를 확인하세요. `unity-editmode-results` artifact에는 Test Runner가 출력한 보고서가 저장됩니다.

## 이후

- `main`에 푸시하거나 같은 저장소에서 `main` 대상으로 Pull Request를 만들면 자동으로 EditMode 검사가 실행됩니다.
- 검사 범위: Unity 프로젝트/스크립트 import·compile, 필수 씬 등록, 구형 곡별 플레이 씬 비활성화, 곡 채보 카탈로그의 참조 연결.
- **이 워크플로는 Unity Game View를 촬영하지 않습니다.** 검색창의 실제 커서나 장면 배치는 아직 시각적으로 보증할 수 없습니다. 이를 보려면 화면 렌더링이 가능한 별도의 환경(예: Windows 테스트 러너 + 스크린샷 기능)을 추가해야 합니다.
- `UNITY_CI_ENABLED` 값을 `false`로 바꾸면 추가 커밋 없이 검사를 중단할 수 있습니다.

가이드: [GameCI 활성화](https://game.ci/docs/github/activation/), [GameCI 테스트 러너](https://game.ci/docs/github/test-runner/).
