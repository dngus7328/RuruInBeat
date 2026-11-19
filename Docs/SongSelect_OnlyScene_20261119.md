# 곡 선택 단일 씬 (2026-11-19)

이 프로젝트의 유일한 곡 선택 씬은 **Assets/Scene/SongSelectScene.unity** 입니다.

이 씬은 이전 6행 목록이 아닌 **2열 3행 카트리지 진열대 + 우측 세로형 패키지 미리보기**로 재배치됐습니다.

## 유지된 기능
- 자동 곡 카탈로그, 각 곡당 대표 채보 하나
- 카트리지 첫 클릭: 선택 애니메이션과 곡 정보 갱신
- 같은 카트리지 재클릭: 삽입 애니메이션 후 게임 시작
- 미리듣기, 게임 시작 버튼, 타이틀 복귀
- 한국어 기본 및 영어·일본어 번역 문자열

## 정리된 중복 파일
- 예전 UI 내용이 들어 있던 원본 `SongSelectScene.unity`의 콘텐츠를 새 UI로 교체했습니다.
- 새 UI의 GUID `f0b6d7382c6e4f79b623ac784af49e90`를 정식 `SongSelectScene.unity.meta`로 이전했습니다.
- 기존 `SongSelectArcadeScene.unity`와 메타는 삭제합니다.
- 예전 레이아웃 재생성 스크립트도 삭제하고 Build Settings와 TitleScene을 단일 씬으로 설정했습니다.

## 검증
씬의 참조 fileID 누락 여부 및 Build Settings 씬 GUID를 확인했습니다.
Unity Editor에서 실제 렌더링 및 Play Mode 실행 검증은 하지 못했습니다.
