# 루루 인 비트 스토리 편집 가이드

Unity 2022.3에서 스크립트 수정 없이 스토리를 편집하는 방법입니다.

## 기존 튜토리얼 대사
`Tools > Lulu In Beat > 스토리 > 1. 튜토리얼 대사 편집`을 선택합니다.
Inspector에 `Assets/Resources/TutorialStory.asset`이 열립니다.

- Opening: 도입 대사, Closing: 종료 대사. 자유롭게 추가, 삭제, 순서 변경.
- Speaker: 화자 이름, Text: 대사, Expression: 표정 번호(-1은 자동/기본).
- Left/Right Portrait: 직접 사용할 Sprite, Hide Left/Right: 해당 캐릭터 숨기기.
- Background: 대사별 배경. Opening Background는 도입 기본 배경입니다.
- Incomplete Closing: 실습 미달일 때 첫 종료 대사를 대신하는 문장. 비우면 일반 종료 대사 사용.
- Chapter Names/Guides/Success/Lead: 여섯 실습 단계의 안내 및 성공 대사. 단계 순서와 채보 타이밍은 유지해야 합니다.
- 튜토리얼의 Sound 필드는 사용하지 않습니다. 독립 스토리에서만 대사별 효과음을 재생합니다.

## 새 스토리 만들기
`Tools > Lulu In Beat > 스토리 > 2. 새 스토리 만들기`를 누르고 원하는 경로에 에셋을 저장합니다.
스토리 제목, 기본 배경(Sprite), 배경 음악(AudioClip), 글자 표시 속도, 대사 목록을 편집합니다.
표정 번호는 루루 0 생각/1 기본/2 불만/3 격려, 피노 0 걱정/1 기본/2 자신감/3 소개/4 질문입니다.
임의의 캐릭터는 직접 Sprite를 지정합니다. 필요 없는 캐릭터는 Hide로 숨깁니다.
Story ID는 저장 파일의 감상 기록에 사용되므로 중복되지 않게 유지합니다.

## 게임 흐름에 연결하기
`Tools > Lulu In Beat > 스토리 > 3. 스토리 연결 설정`을 선택합니다.
Routes에 항목을 추가하고 Story에 만든 에셋을 드래그합니다.

- BeforeTutorial: 최초 시작 시 튜토리얼 전에 재생.
- AfterTutorial: 튜토리얼 종료 후 곡 선택을 누르면 재생.
- AfterSongClear: 곡을 클리어하고 결과 화면에서 곡 선택을 누르면 재생.
  Chart에 해당 곡의 MouseModeChartAsset을 연결하면 그 곡에만 적용됩니다.
- Once Per Save: 끝까지 감상한 스토리는 저장 데이터에 기록되어 자동 재생에서 제외됩니다.
- 여러 항목은 등록 순서대로 재생합니다. 비어 있는 스토리는 건너뜁니다.
- 초기 Routes는 비어 있으므로 기존 게임 흐름이 유지됩니다.

## 스토리 씬 UI 수정 및 미리보기
`Tools > Lulu In Beat > 스토리 > 4. 스토리 씬 열기 및 UI 편집 준비`를 선택합니다.
첫 실행 때만 편집 가능한 UI Hierarchy를 만들고 씬을 열어 줍니다. Ctrl+S로 저장하세요.
StoryController의 Preview Story에 에셋을 넣고 Play를 누르면 직접 미리보기할 수 있습니다.
직접 실행했을 때 종료할 씬은 Direct Exit Scene에서 설정합니다. 게임 도중 자동 연결된 경우에는 원래 진행 경로를 따릅니다.
StoryCanvas 아래 Background, LeftPortrait, RightPortrait, DialoguePanel 등의 RectTransform 및 UI 스타일을 수정할 수 있습니다.
대사/표정/배경 자체는 스토리 데이터가 실행 중 적용하므로 데이터 에셋에서 수정하세요.
Canvas와 컨트롤러의 UI 참조 필드는 삭제하지 마세요.

새 배경이나 캐릭터 이미지 파일은 Unity에서 Sprite (2D and UI)로 설정한 뒤 스토리 데이터 슬롯에 연결합니다.
새 스토리 에셋과 변경한 씬은 GitHub Desktop에서 함께 커밋해야 다른 PC에도 적용됩니다.

## 검증
기존 튜토리얼 대사와 실습 안내를 에셋으로 옮겼으며 기본 루트는 비활성 상태입니다.
EditMode 테스트는 `RuruStoryEditModeTests`입니다. Unity Test Runner에서 실행하세요.
