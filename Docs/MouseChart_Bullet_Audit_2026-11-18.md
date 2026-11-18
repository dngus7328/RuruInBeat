# 루루 인 비트 — 마우스 채보/탄막 문제 조사 (2026-11-18)

## 확인한 대상
- GitHub `main`의 `Assets/Scene/Mouse.unity`, `Assets/Scripts/MousePlay/MouseModeChartPlayer.cs`, `Assets/MouseMode/MouseModeBulletDirector.cs`
- 별도로 전달했던 `PopnDash_119_REBUILT.json` (BPM 119, 노트 200개, 탄막 이벤트 19개). **해당 JSON과 Pop'n Dash 음원은 아직 GitHub `main`에 없음.**
- 주의: 파일 내용과 정적 시뮬레이션 결과이지 Unity 실행 플레이 결과가 아님.

## 발견한 결함과 근거

1. **실제 Unity 크기를 반영하지 않은 노트 배치.** `Mouse.unity`는 `noteSize=100`, `noteVisualScale=1.393`, `approachTravelSeconds=1.4`, `approachStartScale=3`로 설정. `MouseModeChartPlayer.GetVisualNoteSize()`와 `ToUI()` 로직으로 가상 NoteLayer 900×450을 계산하면 노트 지름은 126px. Pop'n Dash 최신 JSON에는 1.4초 내 동시 표시 노트 본체 사이가 126px 미만인 쌍이 **43개**. 같은 좌표의 30·33번은 23.928초 / 25.200초에 등장해 서로 가려질 수 있음. 이는 **화면 크기를 가정한 정적 검증 결과**임.
2. **접근 원이 과도하게 큼.** 3배 설정과 접근 원 지름 계수 1.04를 곱하면 900×450 가상 화면에서 최대 약 393px. 노트 본체가 떨어져 있어도 접근 원이 다른 노트를 덮음.
3. **화면 밖 시작 탄환을 너무 빨리 삭제.** `UpdateAdvancedShots`는 최초 생성된 탄환이 화면 밖이면 곧바로 파괴할 수 있었음. 화면 밖 → 화면 안 → 반대쪽 밖을 가로지르는 패턴이 불가능해지는 결함.
4. **탄막 설정이 Inspector 값을 무시.** 기존 `Awake()`가 `fallDurationBeats`, `warningLeadBeats`, `exitDurationBeats`, 피격 관련 수치를 강제 덮어씀. 저장한 값대로 실제로 플레이되지 않을 수 있음.
5. **고급 탄막의 다수가 정해진 좌표에서 시작.** 기존 `adv_spiral`, `adv_ring`, `adv_stream` 등은 `LocalPoint` 고정 발사 좌표를 사용함. 게임 속 발사체 위치와 반드시 일치하지 않음.
6. **재생 차트와 에디터 선택 차트가 다를 수 있음.** GitHub `Mouse.unity`의 `MouseModeChartPlayer.chartAsset`은 `NewMouseChart.asset`(BOOTBEAT)을 참조함. Pop'n Dash 음원/채보가 GitHub에 없는 상태에서는 Pop'n Dash용 Unity 실플레이를 검증했다고 말할 수 없음.
7. **판정 싱크는 채보와 별도 검증 필요.** 현재 `MouseModeChartPlayer`는 `AudioSource.PlayScheduled(startDsp)`와 같은 `startDsp` 기준으로 노트를 판정하도록 작성됨. 코드만으로는 동기화가 틀렸다고 단정할 수 없음. 입력 오프셋과 실제 음원의 첫 박/악기음을 별도로 검증해야 함.

## 이 브랜치의 수정
- 탄환이 실제 화면 영역에 들어오기 전에는 오프스크린이라는 이유로 즉시 삭제하지 않도록 수정. 일단 진입한 탄환은 **완전히 화면을 나갈 때** 제거.
- 신규 선택형 고급 패턴 `adv_edgecross`: 화면 밖 → 화면 안 → 반대편 화면 밖의 횡단탄. 기존 채보에 무단 삽입하지 않음.
- Inspector의 탄막 시간/피격 관련 설정을 `Awake()`에서 무단 덮어쓰지 않도록 수정. `Mouse.unity` 값은 기존의 실제 실행 효과를 유지하는 값으로 옮김.
- Mouse 씬 접근 원 시작 배율을 3에서 1.55로 축소하여 과도한 가림 완화. **노트 JSON 좌표의 겹침까지 자동 해결한 것은 아님.**
- 시작 시 노트 가독성 검사를 실제 노트 크기 및 접근 원 크기에 맞게 강화.

## 미해결 / 확인이 필요한 항목
- Pop'n Dash 채보의 실제 노트 **43쌍** (900×450 가상 크기) 재배치와 음악별 의미 있는 손 움직임 구성은 별도 **채보 파일 변경**이 필요함. 자동으로 박자/좌표를 흔들어 땜질하지 않음.
- Pop'n Dash 음원 및 최신 JSON이 GitHub에 올라오지 않아 실제 Unity 재생, 정확한 음악 싱크, 곡 전체의 회피 가능성은 검증되지 않음.
- 고정 좌표를 사용하는 기존 고급 패턴은 새로 도입한 횡단 패턴과 별개임. 일괄 수정하면 기존 채보에 영향을 주므로 후속 재설계 대상으로 구분.
- Unity 에디터 컴파일/실행 검증을 수행하지 못했으므로 이 변경은 **병합 전 검증용 초안 PR**임.
