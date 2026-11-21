# 마우스 모드 채보·가독성 품질 점검 — 2026-11-21

## 어떤 문제가 있었나

기존 `900×450` 화면 가정을 실제 Unity 게임 화면과 혼동했다. 실제 `Assets/Scene/Mouse.unity`는 1920×1080 CanvasScaler를 사용하고, NoteLayer가 부모 영역에 맞춰 늘어난다. 이 씬의 레퍼런스 크기에서 계산한 NoteLayer는 약 **1792×859 UI 단위**이다. 실제 PC 창 크기와 CanvasScaler의 동작에 따라 달라질 수 있다.

또한 사용자의 마우스 좌·우 클릭은 **노트 위치를 직접 클릭하는 방식이 아니다.** 따라서 단순한 노트 간 커서 이동 속도로 채보를 실패 처리해서는 안 된다. 노트 좌표는 리듬 가독성에 사용하며, 피노의 마우스 회피와 판정은 별도의 시스템이다.

## 5곡의 플레이용 채보 직접 수정

`Assets/MouseMode/ChartSystem/Charts/`의 게임용 `.asset`와 `Assets/Editor/`의 대응 채보 `.json`를 함께 변경했다. 노트의 **beat / kind / endBeat / wheel path / attacks / tempoChanges**는 수정하지 않았다.

| 채보 | 기존 본체 겹침 후보(쌍) | 수정 후 | 위치만 재배치 |
| --- | ---: | ---: | ---: |
| ArcadeGroove_MOUSE (Oddbeat Shuffle) | 183 | 0 | 106 |
| FullBandHit_MOUSE (Full-Band Hit) | 235 | 0 | 184 |
| MabatakiNoSenritsu_MOUSE (瞬奏 -SHUNSOU-) | 303 | 0 | 193 |
| RhythmGameTheme_MOUSE (Cotton Pulse) | 158 | 0 | 113 |
| popn_dash (Pop'n Dash) | 101 | 0 | 73 |
| **합계** | **980** | **0** | **669** |

이 수치는 **1500×720 크기의 보수적 UI 모델**, 최소 중심 간격 150 단위, 판정 전 2.2초 등장, GOOD 판정 후 0.13초까지의 동시 표시 범위 및 이동 중 휠 머리 위치를 반영한 자동 기하 검사 결과다. 이미지 렌더링/손 조작/음악적 재미의 실측이 아니다. 기존 위치에 최대한 가깝게 이동하되 위험한 겹침을 없앴고, 곡의 박자와 입력 난이도를 알고리즘으로 변경하지 않았다.

`BOOTBEAT_MOUSE_LV6`는 같은 조건에서 본체 겹침 후보가 0이어서 배치를 그대로 유지했다. **숨긴 실험용 채보 및 선택되지 않는 이전 난이도 채보는 이번 재배치 대상이 아니다.** 기존 문제 후보가 남아 있을 수 있다.

## Unity 내 검사

메뉴 **Tools > Ruru In Beat > Audit All Mouse Charts**를 클릭하면 다음을 얻는다.

- `Reports/MouseChartAuditSummary.csv` — 차트·화면 크기별 전체 개수
- `Reports/MouseChartAuditDetails.csv` — 겹침/빠른 연속 배치/음원 범위/탄막 이벤트 문제의 노트 ID·곡 시간
- 기준 화면은 `Mouse.unity`의 CanvasScaler·Anchor·SizeDelta로 계산한 **레퍼런스 NoteLayer 크기**와 이 크기의 84%인 좁은 PC 시나리오다. 과거 900×450/750×375의 고정 가정은 사용하지 않는다.
- 인게임 `MouseModeChartPlayer`도 실제 `noteLayer.rect`와 휠 머리 이동을 기준으로 경고한다. 채보를 자동으로 움직이거나 노트 크기를 바꾸지 않는다.

휠 레일/END 마커는 `WheelGuides` 전용 UI 레이어로 옮겨, 현재 쳐야 하는 노트의 본체와 판정 테두리를 가리지 않도록 했다. 기존 노트 이미지·판정 시간·휠 모션은 유지한다.

## GitHub에서 반복 검사

`.github/scripts/chart_static_audit.py`는 매번 `Mouse.unity`의 노트 크기·접근 시간을 읽고 **1500×720 및 1792×859** 두 모델에서 선택 가능한 `MOUSE` 채보의 본체 겹침을 검사한다. 하나라도 발생하면 GitHub Actions의 `Mouse chart data checks`가 실패하도록 변경했다. 음악 파일 GUID·카탈로그 등록·공격 패턴 ID 검사는 계속 수행한다.

## 남은 검수 — 완료 선언 불가

1. **Unity Play Mode**: 각 노래의 소리와 클릭 타이밍 일치, 휠 시작·중간·끝 판정, 실제 노트 가독성, 창 크기 변경, 제목→곡 선택→게임→결과 흐름 확인
2. **탄막 플레이**: 피노 가만히 있기/좌우 이동/위아래 이동/고속 이동 시 피격·회피 가능성, 다중 패턴 중 회피 공간, 판정과 회피를 동시에 할 수 있는지 검수
3. **성능과 안정성**: 긴 곡 완주, 재시작, 포커스 이탈, 일시정지 복귀, 랙 및 FPS 확인
4. **음악성**: 오프셋과 실제 음원의 악센트가 맞는지 사람이 들어보고 플레이하여 판단. 기하학적 겹침 0이 재미를 증명하지 않는다.
5. **빌드 검사**: 현재 GitHub의 Unity CI는 별도 `UNITY_CI_ENABLED` 설정이 필요한 조건부 작업이다. 정적 Python 검사가 통과하더라도 Unity 컴파일·실행 성공으로 해석하지 않는다.

채보 원본은 각 Git 커밋의 이전 버전으로 복구할 수 있다. 사용자가 정한 디자인 및 접근 원 설정을 임의로 줄이지 않는다.
