# 《루루 인 비트》 대화 한도 대비 개발 인수인계
작성 기준: 2026-11-21 / GitHub: https://github.com/dngus7328/RuruInBeat

> 이 문서는 후속 ChatGPT 대화가 **이전 대화를 직접 보지 못해도** 개발을 재개하도록 만든 인수인계 자료다. 후속 담당자는 첫 작업 전 반드시 GitHub `main`의 최신 파일을 확인하고, 이 문서가 오래되었으면 현재 코드 기준으로 갱신할 것.

## 1. 지금 가장 중요한 사용자 요구
- **노트의 실제 판정 시각은 음원의 의도된 리듬과 정확히 맞아야 한다.** 단순히 가까운 음향 피크(onset)를 찾거나 통계적으로 오차를 줄이는 것은 채보의 정확성을 증명하지 않는다.
- 곡의 실제 구조(인트로·절·후렴·전환), 박자·BPM·오프셋, 드럼/베이스/멜로디의 **어떤 소리에 어느 노트가 대응하는지** 분석한 뒤 채보한다. 무작위 노트 추가, 음원과 무관한 기계적인 등간격 패턴 금지.
- 사용자는 그동안 음악과 맞지 않는 채보와 반복된 '수정 완료' 선언에 상당히 실망했다. 분석과 검증이 끝나지 않은 결과를 **완성·정확하다고 주장하지 말 것**.
- 사용자는 **불필요한 별도 개발 브랜치**를 원하지 않는다. 추후 요청된 검증 가능한 변경은 기본적으로 `main`을 기준으로 처리하고, 불확실한 보정안을 무턱대고 확정·반영하지 말 것. 불가피한 위험이 있다면 이유를 분명히 설명할 것.
- UI나 다른 기능보다 **음악-노트 타이밍을 먼저** 바로잡아야 한다. 사용자가 직접 지시하지 않은 기존 아트/배치 변경 금지.

## 2. 게임과 조작의 최신 확정 사항
- PC 전용 마우스 리듬게임. 과거 4레인 Q/W/O/P 방식은 폐기됨. **4키 채보 전제로 설명하거나 구현하지 말 것.**
- 현재 마우스 모드 코드에서는 좌클릭/우클릭/휠로 노트를 판정하고, 마우스 위치로 피노를 이동시켜 탄막을 회피한다.
- 입력은 노트의 화면상 좌표를 직접 클릭하는 방식이 아니다. 노트의 종류와 **시간**으로 판정한다.
- 루루·피노의 최신 확정 이미지와 화풍은 임의 수정 금지. 과한 네온, 번쩍이는 글로우, 금속성 UI, 모바일식 HUD 지양.
- 스크립트 코드 안내 시 생략 없는 **전체 코드**를 선호한다. Unity 초보이므로 필요한 단계는 간단하고 구체적으로 전달하되 이미 답한 걸 반복해서 묻지 않는다.

## 3. 현재 GitHub 상태 (2026-11-21 확인)
저장소: https://github.com/dngus7328/RuruInBeat
- 기본 브랜치: `main`.
- **PR #49** `Full-Band Hit 노트별 음원 타이밍 교정`: `main`에 병합됨. 병합 커밋 `3fe01811d0aa6e33fcdee56a8b60b6324d5d115b`.
  - 음악 분석 기반 템포맵 51개 구간, Full-Band Hit 노트 헤드 301개와 휠 노트 종료 8개를 수치상 보정함.
  - 실제 게임의 `Assets/MouseMode/ChartSystem/Charts/FullBandHit_MOUSE.asset`와 편집용 `Assets/Editor/FullBandHit_MOUSE.json`이 함께 수정됨.
  - **매우 중요:** 음향 강세 자동 분석 결과를 따라 조정했을 뿐, 각 노트가 어느 악기의 의도된 박자를 표현하는지 전부 사람 귀로 확인한 것은 아님. '노트 타이밍이 전부 정확해졌다'라고 설명하면 안 됨.
  - 6곡 오디오 분석, 정적 차트 검사는 통과했으나, **실제 Unity 플레이와 청음 완주 검증은 별개**임. GitHub Unity EditMode 상태도 필요할 때 재확인.
- **PR #48** `안정화 게이지·실패 규칙 연결 (1차)`: **열린 draft, `main` 미병합**. 안정화 수치/실패 로직 및 런타임 임시 세로 게이지 변경은 PR #48에 존재하지만 `main`에 들어간 것이 아니다. 타이밍 우선 처리 후 다룰 것.
- 사용자 요청 없이는 또 다른 브랜치를 늘리지 말 것.

## 4. 실제 파일·구조
- 핵심 마우스 차트 데이터 모델 / 박자 변환: `Assets/Scripts/MousePlay/MouseModeChartData.cs`의 `MouseModeChartTiming.BeatToSeconds` 및 `SecondsToBeat`.
- 판정·음악 재생: `Assets/Scripts/MousePlay/MouseModeChartPlayer.cs`.
  - `AudioSettings.dspTime`을 기반으로 `PlayScheduled(startDsp)` 사용.
  - `judgeOffsetSeconds`는 현재 `Assets/Scene/Mouse.unity`에서 0으로 설정됨(확인 당시).
  - 채보 노트의 `beat`를 `BeatToSeconds`로 변환하여 판정 시각을 계산.
- 실제 사용 씬: `Assets/Scene/Mouse.unity`.
- 실제 게임용 차트: `Assets/MouseMode/ChartSystem/Charts/*.asset`. 편집용 대응 JSON: `Assets/Editor/*.json`. 수정 시 **반드시 일치시킬 것**.
- 원본 음원: `Assets/Song/`. 대표 6곡:
  1. `BOOTBEAT.wav` / `BOOTBEAT_MOUSE_LV6`
  2. `ArcadeGroove.mp3` / `ArcadeGroove_MOUSE`
  3. `Full-Band Hit.wav` / `FullBandHit_MOUSE`
  4. `MabatakiNoSenritsu.mp3` / `MabatakiNoSenritsu_MOUSE`
  5. `RhythmGameTheme.mp3` / `RhythmGameTheme_MOUSE`
  6. `Pop'n Dash.mp3` / `popn_dash`.
- 분석·재현 도구 (현재 `main`에 PR #49로 포함):
  - `Tools/ChartRework/mouse_six_song_audio_audit.py`
  - `Tools/ChartRework/fullband_tempo_diagnosis.py`
  - `Tools/ChartRework/fullband_continuous_tempo_fit.py`
  - `Tools/ChartRework/fullband_note_timing_audit.py`
  - `Tools/ChartRework/fullband_click_comparison.py`
  - `Tools/ChartRework/fullband_original_note_beats.json`: Full-Band Hit의 수정 전 노트 박자 기록
  - `.github/workflows/chart-data-checks.yml`: 원본 음원 분석·정적 데이터 검사·비교 음원 아티팩트
- 추가 문서: `Docs/MouseTempoMap_Draft_20261118.md`, `Docs/MouseChartAudit_20261121.md`, `Docs/MouseSixSong_PhraseAudit_20261119.md`.

## 5. 후속 작업의 올바른 우선순위
1. **현재 `main` 코드 / 실제 선택 중인 차트 GUID / 실제 AudioClip을 재확인**. 플레이 시 어떤 파일이 로드되는지 확정한다. 이전에 다른 차트 파일(예: `NewMouseChart 2.asset`)이 씬에 직접 연결된 적이 있어, 카탈로그와 씬 직접 실행을 구분해야 한다.
2. **가장 문제 있는 곡인 Full-Band Hit부터** 원본 WAV를 실제로 들어 보고, 인트로~엔딩의 리듬 구성과 반복/변주, 타격해야 할 악기·음악 구절을 확정한다. 강세 검출은 후보 위치 참고용이지 판정 확정용이 아니다.
3. 곡 전체에 대해 각 노트의 대응 음향 사건·목표 판정 초·박자값을 기록한다. 노트 헤드뿐 아니라 **휠 시작/끝**도 검증한다. 타이밍을 특정할 수 없으면 미확정으로 표시한다.
4. 음악적 근거가 확인된 차트 변화만 게임용 `.asset`과 편집용 `.json`에 **동일하게 반영**한다. 오디오 BPM이 잘못된 것인지, 노트 박자값 자체가 잘못된 것인지 분리한다. 근거 없이 모든 노트를 1/4박/1/16박에 강제로 스냅하지 않는다.
5. 원본 음악+판정음 A/B 비교, EditMode/정적 검사, **Unity Play Mode 실제 클릭/휠/탄막 동시 플레이**로 최종 검증한다. 자동 검사 통과만으로 완성 선언하지 않는다.
6. 그 후 다른 다섯 곡의 노트 타이밍도 같은 기준으로 순차 점검한다. 안정화 게이지(PR #48) 등은 그다음.

## 6. 다음 대화 시작 문구
“`Docs/CHAT_HANDOFF_20261121.md`를 읽고 `dngus7328/RuruInBeat`의 최신 `main`과 비교해. 지금은 **노트 타이밍을 실제 음악과 정확히 맞추는 것**이 1순위야. 소리 분석 없이 자동 보정만 반복하지 말고, 노트마다 의도된 리듬을 확인해. PR #48은 미병합, PR #49는 병합 완료. 추가 브랜치를 임의로 만들지 마.”
