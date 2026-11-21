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
- **2026-11-21 변경:** 기존 '마우스 위치 무시, 종류+시간만 판정'을 폐기했다. 이제 **마우스 포인터가 실제 노트 원 안에 있고, 해당 버튼과 박자에 일치해야** 클릭 노트가 판정된다. 허공 클릭은 콤보 초기화 + PERFECT 1개 분량 감점, 노트 위에서 너무 이르게 클릭한 경우 노트 MISS 소모. 자세한 구현은 `Assets/Scripts/MousePlay/MouseModeChartPlayer.cs`의 `IsPointerOnNote`, `TryConsumeEarlyTap`.
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

## 7. 2026-11-21 추가 진행 — Full-Band Hit 실제 WAV·노트 구간 분석

**반드시 최신 코드와 함께 읽을 새 문서:** `Docs/FullBandHit_OriginalAudio_Rhythm_Audit_20261121.md`.

- 사용자 지시에 따라 **별도 브랜치 없이 `main`에 직접 수정**.
- 원본 `Full-Band Hit.wav` 129.56초 전체를 4개 주파수 대역(45–220 / 220–950 / 950–2600 / 2600–5400Hz)으로 실제 신호 분석했다. **악기 스템 분리는 아님**.
- `Tools/ChartRework/fullband_music_structure_audit.py`를 추가하고 GitHub Actions `chart-data-checks.yml`에 실행 및 16박 구간별·노트별 CSV/JSON 아티팩트 출력을 연결했다.
- `FullBandHit_MOUSE`의 탭 노트 수 **394 → 389**. 기존 노트 ID **390,391,394,395,396 삭제**(뒤의 둘은 실제 음량이 일반적인 음량의 1% 미만). 기존 ID **5,7,43,161,387,389,392,393**의 판정 beat를 WAV에서 강한 공격 후보 시각에 맞춰 변경했다. 편집용 JSON과 실제 .asset 모두 변경.
- 최신 원음원 검사 수치: **389개 노트 전부 4대역 검출 공격 중 적어도 하나에서 35ms 이내**. 다만 이 결과만으로 '곡과 100% 완벽히 일치' 또는 '악기별 의도가 반영된 완성 채보'라고 말하면 안 된다. 일부 타격은 약할 수 있고, 원래는 계속 유지되는 소리의 일부일 수 있다. **청음 검증 필수**.
- 매우 중요: `Assets/Scene/Mouse.unity`를 Unity Editor에서 **직접 실행하면 Inspector가 `NewMouseChart 2.asset` GUID(`c7236f68a65f2ce4a9f022221f5f66ee`)를 가리킨다**. Full-Band Hit를 검증하려면 **SongSelectScene에서 Full-Band Hit를 고르고 Mouse 씬으로 넘어가야 한다**. `MouseModeSongSelection.CurrentChart`가 선택 곡으로 대체하는 구조. 무턱대고 Mouse 씬 Inspector를 덮어쓰지 말 것.
- 본 작업으로 실제 WAV의 onset 근접성은 개선되었지만 **곡의 구조·드럼/베이스/멜로디의 의도·노트 패턴 전체를 수작업 청음으로 검증한 것은 아님**. 다음 단계는 패턴/음악적 표현의 정확성 검수, 실제 게임 Play Mode 오프셋 검증이다.

## 8. 2026-11-21 후속 진행 — Full-Band Hit 곡 전체 실제 음향 기반 새 채보 (최신 상태)

**이 8항이 위 7항의 389개 노트 상태보다 최신이다.**

- `main`의 `Assets/Editor/FullBandHit_MOUSE.json`와 `Assets/MouseMode/ChartSystem/Charts/FullBandHit_MOUSE.asset`를 실제로 교체했다. **470개 노트 = 왼쪽 클릭 230 + 오른쪽 클릭 229 + 기존 휠 11개**. 탄막 공격 이벤트 15개와 기존 휠 레일·종료·타임라인은 유지했다.
- `Tools/ChartRework/fullband_music_driven_rechart.py`는 원본 `Full-Band Hit.wav` 129.56초를 4개 대역으로 분석하고 음향 타격 복합 이벤트를 묶어 4박 마디별 3~8개 음향 근거 후보를 선택했다. 원래 일정한 1박마다 1노트씩 놓이던 패턴을 대체한다. `fullband_export_candidate.py`가 새 JSON과 Unity YAML을 동기화한다.
- 2026-11-21 원본 WAV 후보 분석에서는 1269개 이벤트 복합체 / 1038개 1/4박 격자 근접 이벤트를 추출해 후보 470개를 만들었다. 일반 탭은 원본 WAV의 검출 타격 시각으로 변환해 `BeatToSeconds`와 2ms 이내 일치하도록 검사했다. **이 검사는 청음이나 음악적 의도에 대한 확정 증명이 아니다.**
- 런타임 접근 원은 최소 1500×720 해상도에서 겹치지 않도록 24개 위치 후보로 분산했다. 게임용 임시 후보로 `.github/scripts/chart_static_audit.py`를 실행했을 때 **Full-Band Hit의 겹침·음원 링크·재임포트 데이터 검사에 오류 없음**.
- 수정 전후 비교를 위해 `fullband_click_comparison.py`는 수정 전 394개 노트를 보존한 불변 기준과 현재 곡 기반 470개 노트를 렌더링하도록 수정했다.
- 본 작업은 **main 직접 반영**. 새 브랜치 없음.
- 반드시 사용자에게 솔직하게 고지할 점: 음향 트랜지언트 분석은 사람이 원본을 끝까지 듣고 악기·프레이즈를 검수한 것과 다르다. **정확한 음악적 완성 보장, 실제 Unity Play Mode 손으로 완주 테스트는 하지 못했다.** 따라서 470개가 음악적으로 의도에 완전히 들어맞는지에 대해 허위로 100% 확언하지 말 것.
- Unity에서 직접 `Mouse` 씬 Play 누르면 임시 다른 채보가 나온다. **SongSelectScene에서 Full-Band Hit 선택**해야 최신 버전이 게임에 로드된다.

## 9. 2026-11-21 레벨 14 SHUNSOU BPM 표기/박자 단위 2배
- 실제 최고 레벨 `MabatakiNoSenritsu_MOUSE` (`瞬奏 -SHUNSOU-`) `level:14`.
- 사용자 요청에 따라 `main`에서 BPM **128.7968 → 257.5936**, 템포 변경 마커 18개 각각의 BPM 및 beat를 모두 2배, 노트 397개 각각의 시작/endBeat, 탄막 16개 beat를 모두 2배 했다.
- **실제 음악 재생 속도, 노트 판정 초 단위 시각, 클릭 밀도, 난이도는 변하지 않는다.** BPM 2배가 '빠른 채보' 또는 '음원과 더 정확히 맞음'을 뜻하지 않는다. 14레벨 실질 난도 조정은 음원 구절별 물리 패턴 재설계가 필요하다.
- 경로: `Assets/Editor/MabatakiNoSenritsu_MOUSE.json`, `Assets/MouseMode/ChartSystem/Charts/MabatakiNoSenritsu_MOUSE.asset`.
- 변환 전후 노트 헤드/휠 종료/탄막/마커 총 **432개 시각** 수학적으로 0ms 편차. GitHub 정적 데이터 검사 및 WAV 음원 타이밍 검사는 성공. 음원 연동 근접도: 평균이 아닌 **중앙값 4.9ms / 90백분위수 15.3ms**(강한 음향 onset 후보 거리). 음악적으로 타격 의도가 모두 검증됐다는 뜻은 아님.

## 10. 2026-11-21 무작위 클릭 판정 악용 수정
- 사용자 지적: 마우스만 아무렇게나 연타해도 노트가 판정되어 플레이 의미가 없음.
- `MouseModeChartPlayer.cs`의 `Judge`가 종류·시간만 검사하던 문제를 해결. **마우스 포인터가 실제 노트 몸체 원 안에 있을 때만** 시간·버튼 판정을 시도하도록 변경. 신형 Input System 콜백에서 화면 좌표를 큐에 동봉해 이벤트와 함께 처리, 구형 Input에서도 마우스 화면 좌표 사용. RectTransform + Canvas 카메라 기반으로 좌표계 대응.
- 원 밖 클릭은 기존의 `RegisterEmptyClick`에 들어가며, `Mouse.unity`에서 `emptyClickPenalty: 0.35 → 1`로 강화. MISS-equivalent 1개 분량 감점·콤보 초기화.
- 노트 위에서 판정보다 **0.13~0.26초 일찍** 클릭하면 해당 노트를 MISS로 확정해 연타가 나중에 성공 판정을 가져갈 수 없게 함. 정확하게 타이밍에 맞춘 고속 연타는 시간 잠금 없이 허용.
- 테스트: `Assets/Tests/Editor/MouseMashPreventionEditModeTests.cs`(원 내부/외부, 조기 탭 조건).
- 이 기능은 마우스로 클릭하는 노트(휠 시작 포함) 대상이다. 휠 지속·끝 판정의 공간 위치까지 별도로 검사하는 것은 아직 하지 않았다.

## 11. 2026-11-22 코드만으로 게임 규칙 완성 (이미지 필요 없음)

사용자가 지시한 것은 새 이미지 제작이 아니라 **기존 Mouse 씬의 작동하는 마우스 클릭·피노 이동을 유지한 채 이미지 없이 가능한 미완성 기능을 직접 구현**하는 작업이었다. 수정은 `main`에 직접 적용했다.

- **안정화와 실패 연결:** `MouseModeBulletDirector`가 `Mouse.unity`의 `enableStabilityFailure:1`을 사용한다. 원본 판정 노트마다 PERFECT +1, GREAT 0, GOOD -1, MISS -4, 피노 피격은 씬 설정의 -15, 안정화 0이면 `StabilityDepleted`를 통해 결과 실패. 실패 이벤트에서 컬렉션 순회 중 StopChart를 호출하지 않도록 다음 Director.Update에서 처리. 기존 `failurePanel` 아트가 없으면 결과 창은 기존 코드 생성 결과 UI. `stabilityFill` 연결이 비어 있으면 Canvas 옆에 **이미지 파일 없는 세로 막대** 생성. 클릭 실패용 가짜 MISS(`Judged`)와 실제 채보 노트(`ChartNoteJudged`)를 구분해 안정화 중복 감점 방지.
- **마우스 연타와 휠 판정:** 엉뚱한 위치의 클릭, 잘못된 L/R, 노트 직전 무리한 연타를 MISS/빈 클릭 감점 처리. `PinoMouseCursorUI`는 플레이 중 OS 커서를 화면 중앙에 고정하고 상대 마우스 이동으로 피노를 움직인다. 따라서 **커서가 Locked일 때 노트 위치 검사 강제는 실제 사용 불가능한 버그**이므로 `IsPointerOnNote`는 Locked 모드에서 위치 요구를 건너뛰고 기존 버튼+시간 판정을 유지한다. 커서 자유 모드일 때만 정확한 원 안 클릭이 요구된다. 휠 중간·끝 입력도 동일한 포인터 검사 함수를 사용한다. **중요: 피노와 노트 입력을 혼동해 조작 방식을 임의 변경하지 말 것**.
- **음량 및 판정 보정:** `AudioSettingsManager`가 기존 `SongManager` 외 실제 `MouseModeChartPlayer`의 곡 AudioSource를 찾아 마스터×음악 볼륨 적용. `NoteSoundManager`는 런타임 생성되는 타격음 소스이므로 재생 때마다 마스터×타격음 또는 마스터×효과음 저장값을 적용. `MouseModeChartPlayer`는 `SaveManager.Data.judgeOffset`을 미리 저장된 Inspector 오프셋에 합산하고 실제 입력/자동 MISS에 똑같이 적용. 기존 타이틀 `TimingSliderValue`, 저장 클램프, 씬 설정 모두 ±150ms로 확장. **노트·음원 시작 시간은 변경하지 않음**.
- **결과/기록:** `MouseModeChartPlayer.MaxCombo`, `AccuracyPercent` 추가. `MouseModeResultFlow`는 마지막 판정에 안정화가 0이면 CLEAR 대신 FAILED, 실패와 클리어 정보로 최대 콤보·피노 HITS·빈 클릭·ACC·TIME·BEST 표시. `SaveManager.SongBestRecord`로 클리어 곡의 최고 점수/최대 콤보를 `save.json`에 저장. 곡별 최초 클리어와 새 기록을 결과에서 구분한다.
- **탄막 경고 안전성:** `MouseModeBulletDirector.EnsureTelegraphLead`로 기본탄 런치 전 최소 .55초, 레이저·폭발 시작 전 최소 .65초 시각 경고 보장. 원래 탄막·노트·음원 타격 시각과 피노 이동은 변경하지 않음.
- **효과 설정:** `SaveData.effectDisplay`가 선택적인 성공 팝/링 이펙트 출력에 적용되도록 변경. 텍스트 판정/사운드/핵심 피격은 유지.
- **테스트:** `MouseRuntimeRulesEditModeTests`가 안정화, 보정 범위, 탄막 경고, 저장 데이터 직렬화, 잘못된 입력 처리 존재 등을 검사한다. `MouseMashPreventionEditModeTests` 유지. GitHub Actions EditMode 테스트를 통해 스크립트 컴파일도 확인한다.
- **미완 항목은 완료로 허위 보고 금지:** 실제 사용자가 Unity Play Mode에서 끝까지 플레이하는 물리/감각 테스트, 음악적 의미에 따른 전곡 채보 수작업 교정, 복잡한 탄막의 실제 이동 경로 검증, 교체할 최종 결과/게이지 UI 아트, 피노의 별도 클리어/실패 프레임은 **아직 검증/제작되지 않았다**. 사용자에게 불필요하게 다시 처음부터 만들라고 하지 말 것.

## 12. 2026-11-22 중간 강제 종료·탄막 의미·음악 시작 전 노트 등장 수정

- **중간 종료 원인:** 이전에 활성화한 `enableStabilityFailure=1`이 안정화 수치 0을 달성하자 `MouseModeBulletDirector.TryDepleteStability`를 호출하고 곡을 강제로 중단. 당시 각 MISS -4, 피노 피격 -15라 짧은 시간에 죽을 수 있었음. `stopImmediatelyAtZero:0`으로 변경하여 **노래는 끝까지 진행**하고, 결산 시 `ShouldFailAtSongEnd`(안정화 0 여부)를 통해 실패를 판정함. 각 GOOD -0, MISS -1, 피격 -12, 무적시간 1.35초로 조정. PERFECT +1. 안정화 0도 플레이 중 회복 가능.
- **탄막의 긍정적 역할:** 탄환을 피노가 아슬아슬하게 스쳤을 때 `GRAZE` 판정으로 안정화 +2, 런타임 텍스트 표시, 결과창 GRAZE 횟수 표시. 각 탄환별 최대 1회, 전역 0.35초 쿨타임. 일반/고급 탄 모두 스윕 궤적 기준 안전한 접촉 바깥에서만 발생. `CalculateBattleScore`는 피격 -10,000, GRAZE +1,000(최대 +25,000)을 적용하며, 불완전 리듬 판정으로 1,000,000 SS 위조는 불가능. 음악·노트 판정 시각은 바꾸지 않음.
- **노래 전 채보 등장 원인:** `MouseModeChartPlayer`에서 GO 시작을 첫 노트의 접근 시간에 맞춰 음원 시작보다 약 2초 앞당기는 `openingLeadInSeconds`을 사용했고, 음원보다 먼저 노트가 등장했다. 현재 **음악과 GO는 동일한 DSP 시각**, 음악 시작 전 화면에 노트·탄막 생성 불가. 음원·노트의 기존 판정 타이밍은 유지한다.
- **한계:** 실제 곡의 첫 노트는 BOOTBEAT 0.697s, ArcadeGroove 0.460s, RhythmGameTheme 0.190s, FullBandHit 0.143s, SHUNSOU 0.350s 등 **음악 시작 직후** 존재. 따라서 음악보다 먼저 노트를 보여주지 않으면서 첫 노트 모두에 2.2초 접근 시간을 줄 수는 없음. 조기 채보를 숨기는 방향을 적용했지만, 첫 노트의 접근 시간이 짧아지는 데 대한 플레이 체감 테스트는 필요. 초반 채보를 임의로 재배치하거나 음악을 미리 재생하지 말 것.
- **검사:** `MouseRuntimeRulesEditModeTests`에서 장르 기준 GRAZE 판정과 보상, 곡 시작 전 노트 표시 불가, SS 규칙 등을 테스트. 실제 사용자가 Unity에서 완주한 테스트는 아님.
