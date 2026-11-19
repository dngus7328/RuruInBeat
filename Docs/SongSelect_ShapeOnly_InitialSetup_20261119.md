# 곡 선택 화면 — 에디터 .asset 자동 반영 / 배치 보존

## 목표
곡 이름이나 개수를 코드에 적지 않는다. 사용자가 Unity의 마우스 채보 에디터에서 `MouseModeChartAsset` 파일을 만들고 편집하면, 화면이 다음에 열릴 때 자동으로 목록에 나타난다.

## 자동 수집 조건
- 경로: `Assets/MouseMode/ChartSystem/Charts/*.asset`
- 형식: `MouseModeChartAsset`
- 해당 .asset에 **AudioClip**, 비어 있지 않은 노트, **레벨 1~15**가 있어야 함
- 같은 `AudioClip`을 참조하는 여러 .asset은 한 곡의 여러 난이도로 묶음
- 곡 제목은 기본 난이도의 `displayTitle`, BPM/난이도/길이는 각각의 실제 .asset 정보
- 이전 구버전 시험 채보를 포함해 유효한 파일은 모두 목록에 등록되므로, 테스트 채보도 보일 수 있음
- 음원이 없는 채보/레벨 0/비어있는 시험 채보는 표시하지 않음

## 동작
- 에디터가 `SongSelectChartCatalog.asset`을 `Assets/Resources/`에 한 번 생성하고, 채보가 추가/교체/삭제되면 **카탈로그 데이터만 갱신**
- `SongSelectScene` 기존 Image / TMP / Button / RectTransform을 **다시 생성하거나 위치 변경하지 않음**
- 한 화면의 6개는 **보이는 행 슬롯 수**일 뿐, 곡 수 제한이 아님. 마우스를 왼쪽 곡 목록에 놓고 휠을 움직이면 추가 곡으로 이동하며 다음 페이지 표시
- 난이도 전환은 같은 노래 AudioClip을 가진 다른 채보 파일이 있을 때 활성화
- 시작 버튼은 선택한 `MouseModeChartAsset`을 공유 `Mouse.unity` 씬에 전달. 전용 곡별 씬 복제 불필요
- 플레이는 현재 `MouseModeChartPlayer` 판정과 탄막 시스템을 그대로 사용
- 이미지 없는 상태에서는 기존 도형 플레이스홀더 유지. 나중에 Image 컴포넌트 Sprite만 변경 가능

## 기존 씬/유저 배치 보호
- `Assets/Scene/SongSelectScene.unity`을 처음 열었을 때 **Main Camera만 있는 빈 씬**이면 도형 UI를 단 1회 생성·저장
- 씬에 UI가 한 번 생기면 에디터 빌더는 **어떤 좌표/계층/앵커도 수정하지 않음**
- 이미 수동 배치한 UI가 있다면 자동 생성도 건너뜀
- 실행 중 UI 코드에서도 위치/크기/계층을 만지지 않고 텍스트/선택 색/이미지만 갱신
- 이전 버전의 6곡 고정 UI로 생성된 씬에서도 기존 버튼/패널을 재사용
- Build Settings는 기존 씬을 삭제/재정렬하지 않고, `TitleScene`을 포함한 기존 목록을 유지하며 `SongSelectScene`과 공유 `Mouse`가 없을 경우에만 추가

## 새 채보 넣는 방법
1. 채보 에디터에서 새 `MouseModeChartAsset` 생성.
2. 기존 곡의 새 난이도라면 기존 파일과 동일한 **AudioClip**을 설정.
3. 제목/레벨(1~15)/노트/BPM을 채우고 저장.
4. 곡 선택 씬을 플레이하면 목록과 난이도에 자동 반영됨.
5. 자동 갱신이 안 되면 메뉴 `Tools > Lulu In Beat > Song Select > Refresh Chart Files`.

## 현 상태
- BOOTBEAT Lv.6/Lv.11
- Pop'n Dash Lv.1(이전 시험 채보)/Lv.8
- Cotton Pulse Lv.5
- Oddbeat Shuffle Lv.9
- Full-Band Hit Lv.13
- 瞬奏 -SHUNSOU- Lv.14
- BOOTBEAT_EASY.asset은 원래 레벨이 지정되지 않았기 때문에 자동 목록에서 제외
- **한 곡에 15개 난이도가 있다는 뜻은 아님**. 필요한 난이도의 실제 채보를 각각 제작해야 한다.

Unity Play Mode에서 화면 가독성과 선택한 채보가 실제로 실행되는지는 아직 확인하지 못했다. 본 수정은 씬을 다시 생성하지 않는 데이터·로직 변경이다.
