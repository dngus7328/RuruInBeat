# 《루루 인 비트》 마우스 모드 이미지 명세서 · 필요 자산 전수표

**기준:** 2026-11-22 / GitHub main / Unity 2022.3 / 실제 PC용 마우스 플레이 구조

> 이 문서는 신규 PNG 제작 지시서와 기존 자산 통합 체크리스트다. 이미 있는 PNG를 곧바로 '사용 중'이라고 주장하지 않는다. GitHub에서 실제 이미지를 렌더링해 육안 검증한 자료가 아니므로 색·투명도·그림 품질은 연결·시각 확인 전에 완료 처리하지 않는다.

## 1. 절대 변경하면 안 되는 확정 사항

1. **4레인 Q/W/O/P 리듬게임은 폐기됐다.** 마우스 이동으로 피노를 움직이고 좌클릭/우클릭/휠로 리듬 입력을 처리하는 PC 게임이다. 구형 레인 버튼·키캡·노트 디자인을 최신 플레이에 억지로 이식하지 않는다.
2. **마우스 노트는 사용자가 지정한 현재 코드 생성 방식 그대로 사용한다.** 원형 노트, 접근 링, L/R/W 구분, 휠 경로/끝점은 MouseModeChartPlayer가 런타임에 처리한다. 새 노트 PNG, 새 접근 링 PNG, 구형 노트 이미지의 재활용을 **제작 요구 목록에서 제외한다**. 노트 판정 방식이나 형태를 임의 재설계하지 않는다.
3. **기존 'Assets/Image/안정화게이지.png', '안정화게이지2.png'는 세로형 4레인용이다. 재사용 불가.** 이 두 이미지를 완성품이나 새 게이지의 자산 후보로 계산하지 않는다. **현재 마우스 모드 화면에 맞는 새 안정화 UI**를 제작한다.
4. 루루/피노의 확정 외형과 그림체를 바꾸지 않는다. 확정 기준명: '루루_확정(20261027-004153).png', '피노_확정(1).png'. 이 두 정확한 원본 파일은 최신 GitHub 파일 목록에서 확인되지 않아, 대체 그림으로 '동일 캐릭터'라고 선언하면 안 된다. GitHub에는 피노의 기존 애니메이션 PNG가 있다.
5. PC UI, 차분한 보라/라벤더 계열과 얇은 선, 무광 2단계 셀 명암을 유지한다. 네온·강한 글로우·과한 광택·메탈 프레임·AI풍 장식·허위 글자·모바일식 카드 구조 금지.
6. 글씨와 숫자(콤보/점수/랭크/진행률/안정화 수치), 버튼 상태, 선택 피드백, 밝기/알파, 노트 수축·타격 효과, 탄막 애니메이션은 **Unity 런타임 구현**. UI PNG에 텍스트/카운터/가짜 조명 효과를 구워 넣지 않는다.

## 2. 실제 GitHub 씬·스크립트 분석

| 게임 부분 | 실제 확인한 구조 | 이미지 작업 판단 |
|---|---|---|
| 타이틀 | TitleScene.unity. 기존 'TitleUI/로고.png', '배경7.png', '버튼.png', '버튼1.png', '슬라이더.png', '슬라이더게이지.png', '확인창.png' 등 GUID 직접 참조 | **원칙적 재사용. 새 타이틀 UI 일괄 제작 금지.** |
| 설정 | TitleScene에 구형 'Lane1KeyRow~Lane4KeyRow' 및 'PinoLeftKeyRow' 등이 남아 있음 | 먼저 씬/코드를 마우스 전용으로 정리. 자산 제작과 별개의 필수 개발 작업. 기존 초기화 버튼 재사용 |
| 곡 선택 | SongSelectScene.unity의 'JacketImage_Replaceable', 'NoJacketArt_Placeholder'. 곡별 SongSelectChartAsset의 'songSelectJacket' 슬롯 존재 | 6개 곡별 재킷이 실제 지정되었는지 확인되지 않음. 최우선 이미지 항목 |
| 실제 플레이 | Mouse.unity: 'GameplayCanvas', 'PlayArea', 'GameDeviceFrame', 'NoteLayer', 'PinoMoveArea', 'BulletLayer', 'HUDLayer', 'TopHUD' | 현재 'Image2/Lulu_UI_Frame_Light_Transparent.png' 등은 씬 직접 참조. 보드 이미지를 다시 만들기 전에 게임 뷰 검수 |
| 마우스 노트 | MouseModeChartPlayer.MakeSprites()가 Texture2D 원형·링·사각형 Sprite를 런타임 생성 | **추가 이미지 0개. 현행 유지.** |
| 탄막·적 | MouseModeBulletDirector가 런타임 탄환/경고 시각화. MouseEnemy01TelegraphUI가 'Resources/MouseEnemies/Enemy01.png' 4×3 스프라이트 원본을 Texture2D로 로드 | 현재 적 원본 재사용. 필요성이 확인되기 전까지 탄환 PNG를 새로 늘리지 않음 |
| 안정화 게이지 | MouseModeBulletDirector.EnsureStabilityDisplay()가 'Stability_Runtime' 임시 UI, 왼쪽에 폭 16·높이 340 크기의 세로 게이지를 런타임 생성 | **신규 마우스 모드 전용 게이지 3개 부품 제작 필수. 구형 4레인 게이지 재사용 금지.** |
| 판정 표시 | 'A9_PERFECT/GREAT/GOOD/MISS/HIT.png' 파일 보유. Mouse 씬 직접 Sprite 참조는 확인되지 않음 | 기존 파일 육안 검수 후 사용/불사용 결정. 노트 본체와 별개의 항목 |
| 결과 | MouseModeResultFlow.BuildFallbackPanel()이 'ResultPanel_Runtime' 임시 직사각형 결과창을 생성 | 임시 패널을 실제 결과 프레임/배경 이미지로 구성 |
| 튜토리얼 | Tutorial.unity / MouseTutorialController. 대화·단계 안내는 코드 생성 패널, 피노는 기존 Pino Image의 Sprite 사용 | 대화/안내 프레임과 루루 초상만 필요한 만큼 추가. 연습 노트·게임판은 기존 Mouse 구조 재사용 |

**기준 해상도:** 4개 활성 씬의 CanvasScaler 기준 1920×1080. 아래 신규 PNG 크기는 원본 제작 권장값(확정된 현재 RectTransform 픽셀 크기가 아님). 실제 배치는 최소 1920×1080, 1600×900, 1366×768 게임 뷰에서 검증한다.

## 3. 반드시 필요한 신규 이미지 — P0

이미지가 없는 게임 기능을 전부 새 이미지로 덮지 않는다. 파일명은 **신규 제작 후 최종 적용할 제안명**이며 아직 저장소에 만들어졌다는 뜻이 아니다.

| ID | 새 파일명 / 납품 단위 | 크기·배경 권장 | 그래픽 지시 및 분리 | Unity 적용 위치 / 완료 조건 |
|---|---|---|---|---|
| P0-01 | Stability_Mouse_Frame.png **1장** | 가로 약 700×84, 알파 투명 | **마우스 모드용 신규 안정화 게이지 외곽 프레임.** 화면의 기기형 무광 UI와 같은 굵기. 4레인 보드에 붙는 구형 세로 프레임 금지 | GameplayCanvas/HUDLayer 쪽의 게임 영역 **밖**에 새로운 안정화 HUD 배치. 노트·피노·탄막 영역을 가리면 불합격 |
| P0-02 | Stability_Mouse_Track.png **1장** | 약 660×44, 알파 투명 또는 어두운 내부 바탕 | **게이지 빈 배경**만 제작. 검은/어두운 무광 내부, 숫자와 막대 없음 | 외곽 프레임 안쪽 Image. 별도 GameObject; fill과 혼합 금지 |
| P0-03 | Stability_Mouse_Fill.png **1장** | 약 640×30, 알파 투명 | 실제 남은 안정화를 표현하는 **속 채움 막대만**. 빛 번짐/광택/색상 변화 굽지 않음 | Unity Image Type=Filled / Horizontal / Left. 수치와 알파는 코드. 최종 방향은 **4레인 세로형이 아닌 마우스 모드 전용 가로형으로 우선 설계**, 화면 배치를 확인하며 위치 조정 |
| P0-04 | Tutorial_Dialogue_Frame.png **1장** | 약 1320×400, 알파 투명, 모서리 분리/9-slice | 루루·피노 대화가 들어갈 무광 프레임. 대사·화자 이름·버튼 이미지에 굽지 않음 | Tutorial / TutorialConversation / DialoguePanel 코드 생성 패널에 Sprite를 적용. 캐릭터와 텍스트 분리 |
| P0-05 | Tutorial_Guide_Frame.png **1장** | 약 620×360, 알파 투명, 9-slice | 단계별 안내 패널. 플레이 장면 오른쪽에 들어가도 눈을 방해하지 않는 선/여백 | Tutorial / TutorialGuide. 현재 '01/06'·제목·설명 TMP를 그대로 사용 |
| P0-06 | Lulu_Portrait_Tutorial.png **1장** | 약 1024×1024 안에서 원본 비율 보존, 알파 투명 | **확정 루루 원본을 사용한 대화용 반신/상반신 초상**. 새 헤어·옷·체형 임의 디자인 금지 | Tutorial 화자별 Portrait Image. 기존 피노 애니메이션 PNG를 루루 초상 대신 쓰지 않음. 확정 이미지가 확보되어야 제작·비교 가능 |
| P0-07 | Guide_MouseMove.png **1장** | 256×256, 알파 투명 | 마우스 본체와 이동 방향을 가리키는 단순 안내용 무문자 픽토그램 | 피노 이동 훈련 단계에만 표시. 80px 출력에서도 식별 |
| P0-08 | Guide_LeftClick.png **1장** | 256×256, 알파 투명 | 좌측 버튼 영역만 간결한 대비로 구별. 굽힌 손/과한 이펙트 금지 | 왼쪽 클릭 안내 TMP 옆, 런타임 색 지정 |
| P0-09 | Guide_RightClick.png **1장** | 256×256, 알파 투명 | P0-08과 동일 크기·각도·선, 오른쪽 영역만 대비 | 오른쪽 클릭 단계 |
| P0-10 | Guide_Wheel.png **1장** | 256×256, 알파 투명 | 휠 중앙만 식별되는 단순 안내. 방향표시는 코드 애니메이션 우선 | 휠 단계. 실제 휠 롱노트의 외형/판정은 건드리지 않음 |
| P0-11~16 | Jacket_BOOTBEAT.png / Jacket_OddbeatShuffle.png / Jacket_FullBandHit.png / Jacket_SHUNSOU.png / Jacket_CottonPulse.png / Jacket_PopnDash.png **6장** | 각 1024×1024, 정사각형, 재킷 특성상 불투명 가능 | 곡마다 다른 **정식 곡 재킷**. 추상 무광 리듬/캐릭터 세계관 기반. AI 가짜 문자 금지. 곡 제목은 Unity TMP에서 별도로 표시 | 각 차트 'MouseModeChartAsset.songSelectJacket'에 1:1로 연결. SongSelect의 JacketImage_Replaceable 및 Placeholder 동작 검증 |
| P0-17 | Result_Backdrop.png **1장** | 1920×1080, 불투명 허용 | 결과화면 후경. 읽기 쉬운 낮은 대비, 배경 단독(점수/랭크/버튼 그림 없음) | MouseModeResultFlow 결과 UI의 가장 뒤. CLEAR/FAILED 양쪽 공용 가능 |
| P0-18 | Result_Frame.png **1장** | 약 1450×900, 알파 투명, 9-slice 권장 | 결과 수치용 PC UI 프레임. 점수/랭크/콤보/판정 통계와 재시도·곡선택을 넣을 영역 확보 | ResultPanel_Runtime의 임시 레이아웃을 정식 결과 화면으로 교체. 레코드 내용은 코드, 버튼은 기존 공통 버튼 재사용 |

**P0 이미지 신규 제작 목표: 18장.** 세부 디자인의 중복이 실제 기존 자산으로 해결되면 그만큼 감축한다. **마우스 노트 PNG = 0장, 기존 4레인 안정화 PNG 재사용 = 0장**은 확정된 기준이다.

### 마우스 전용 안정화 게이지의 구조 (별도 확정 규칙)

- **세로 4레인 보드 부착 게이지를 폐기한 상태에서 출발한다.** 기존 안정을 보여주는 방식만 살리고, 직사각형 비율과 보드 측면 장식은 가져오지 않는다.
- 기본안은 **얇은 가로형, 플레이 범위 바깥의 HUD 공간**이다. 피노 회피 영역과 노트 표시 영역을 가리지 않는 것이 우선이고, 고정 좌표는 실제 Mouse Game View로 검증하기 전에는 확정하지 않는다.
- Unity 구조 예시: GameplayCanvas → StabilityGauge_Mouse → Stability_Frame / Stability_Track / Stability_Fill / Stability_Text. 앞의 3개만 PNG, Text는 TMP.
- 런타임 'Stability_Runtime'과 정식 게이지가 **동시에 보이면 실패**. 실제 안정화 값은 MouseModeBulletDirector.Stability에서 읽고, 판정에 따라 한 곳의 fillAmount만 갱신한다.
- 낮은 수치의 시각 경고는 Animation/Unity Color로 처리한다. 반짝이는 경고 이미지가 필수는 아니다.

## 4. 새 이미지 없이 **재사용·배치**할 항목 — P0

| ID | 자산 | 실제 상태 / 작업 |
|---|---|---|
| R-01 | Assets/Image/피노_idle.png, 피노_Hit.png | 현재 Mouse/Tutorial 직렬화 Sprite GUID에 직접 연결. 4개 프레임씩 썰린 시트. 원본 외형/프레임 ID 유지 |
| R-02 | Assets/Image/피노_Clear.png, 피노_False.png | 파일은 존재하지만 활성 Mouse/Tutorial 씬에 직접 사용되진 않음. 결과 CLEAR/FAILED에서 상황별 올바른 프레임 재사용 검토. 신규 피노 표정 그림 우선 제작 금지 |
| R-03 | Assets/Resources/MouseEnemies/Enemy01.png | 현재 MouseEnemy01TelegraphUI가 Texture2D 형태로 로드해 4×3=12프레임 자름. .meta의 Texture Type이 Sprite가 아닌 것은 **해당 코드에서는 정상**. 임의 Sprite Multiple 변경 금지 |
| R-04 | Assets/Image/A9_PERFECT.png, A9_GREAT.png, A9_GOOD.png, A9_MISS.png, A9_HIT.png | 판정 PNG 5종 존재. 플레이 씬 직접 참조 없음. **실제 그림 품질·투명도 확인 전** 사용할지 결정. 연결 시 기존 판정 TMP와 FAST/LATE가 사라지지 않게 구성 |
| R-05 | Assets/Image/기본_콤보.png, 퍼펙트_콤보.png, A7_score_frame.png | 콤보/점수 프레임 후보. 현재 텍스트 기반 숫자는 Unity에서 유지해야 함. 기존 스프라이트로 필요한 요소만 사용 |
| R-06 | Assets/Image/TitleUI의 로고/현재 배경7/버튼/슬라이더/확인창 등 | TitleScene에 이미 다수 참조됨. 디자인 일괄 교체 없음. 삭제된 4키 설정의 버튼 레이블만 코드 및 씬 수정 |
| R-07 | Assets/Image2/Lulu_UI_Frame_Light_Transparent.png | Mouse/Tutorial의 GameDeviceFrame 직접 참조. **현재 게임기 프레임 사용 중**이며 원본 보드 PNG와 별개. 신규 보드 이미지로 무턱대고 덮어쓰기 금지 |
| R-08 | Assets/Image2/SongIllust/기존 2개 / Assets/Image2/SongJaket/기존 2개 | 현재 사용 여부/실제 그림 내용 확인 후 게임 배경 또는 재킷으로 연결 검토. 6곡 재킷으로 임의로 분배하지 않음 |
| R-09 | Assets/Image/롱노트.png/롱노트2.png/노트.png/노트2.png/판정선.png/게임판.png/피격노트.png | 예전 4레인 채보/보드 계열. **최신 마우스 노트 이미지 후보에서 제외**. 신규 마우스 시각 자료로 자동 전환하지 않음 |
| R-10 | Assets/Image/안정화게이지.png, 안정화게이지2.png | **구형 4레인 UI라 사용 불가. 재사용 대상 제외.** 최신 안정화 게이지는 P0-01~03 신규 제작 |

## 5. 나중에 필요한 이미지 — P1 / P2

| 우선순위 | 항목 / 필요 수량 | 설명 |
|---|---|---|
| P1 | Settings_Scroll_Track.png + Settings_Scroll_Handle.png, **2장** | 이전 UI 분리 명세에서 정리된 **트랙 1개 + 스크롤 핸들 1개**. 길이가 바뀌는 ScrollRect에 연결. 기존 초기화 버튼 PNG 재사용, 새로 만들지 않음. 설정 탭이 실제로 스크롤 필요할 때만 연결 |
| P1 | **곡별 플레이 배경 0~6장** | 'MouseModeChartAsset.backgroundImage'는 대표 6곡 중 PopnDash의 기존 SongIllust 참조를 제외한 다섯 곡에서 비어 있음. 게임 공통 배경이 현재 적합한지 확인한 뒤 필요할 때만 곡별 BG 추가. 재킷과 플레이 배경은 다른 역할 |
| P1 | **탄막 유형별 비주얼 0~3장(선택)** | 현재 탄막은 코드 생성이며 회피 가능성이 더 중요. 특정 탄막 모양이 플레이에 지장을 준다는 실제 사례가 확인된 경우에만 무광 PNG 추가. 색상·접촉 판정 변경 금지 |
| P1 | **일시정지 화면 프레임 0~1장(조건부)** | Mouse 씬에 PausePanel 존재. 기존 패널이 가독성 부족할 때만 새 프레임. ESC 방식 유지, 상시 Pause HUD 버튼 만들지 않음 |
| P1 | **결과 꾸밈 마크·콤보 강조 0~2장(조건부)** | 점수·랭크·클리어 정보는 코드로 표시. 기존 콤보/스코어 PNG를 먼저 비교한 뒤 불가할 때만 추가 |
| P2 | **스토리 컷인/감정별 표정/기타 캐릭터 이미지: 미산정** | 스토리 원고·이벤트 컷 리스트가 확정되기 전에 근거 없는 장수 산정·양산 금지. 우선 P0의 루루 한 장+기존 피노로 프롤로그 구성 |

## 6. 6곡별 이미지 적용표

| 실제 Song ID / 표기명 | 레벨(현재 차트) | 필요 재킷 | PlayArea 배경 상태 |
|---|---|---|---|
| BOOTBEAT_MOUSE_LV6 / BOOTBEAT | 4 | Jacket_BOOTBEAT.png | backgroundImage 비어 있음 |
| ArcadeGroove_MOUSE / Oddbeat Shuffle | 9 | Jacket_OddbeatShuffle.png | backgroundImage 비어 있음 |
| FullBandHit_MOUSE / Full-Band Hit | 13 | Jacket_FullBandHit.png | backgroundImage 비어 있음 |
| MabatakiNoSenritsu_MOUSE / 瞬奏 -SHUNSOU- | 14 | Jacket_SHUNSOU.png | backgroundImage 비어 있음 |
| RhythmGameTheme_MOUSE / Cotton Pulse | 5 | Jacket_CottonPulse.png | backgroundImage 비어 있음 |
| popn_dash / Pop'n Dash | 8 | Jacket_PopnDash.png | 기존 SongIllust 참조됨 |

주의: SongSelectChartCatalog에는 여섯 곡 제목보다 **차트 항목이 더 많을 수 있다**(BOOTBEAT 난이도별 차트 등). 재킷은 무조건 곡(음원) 단위로 재사용하여 같은 곡의 난이도마다 새 그림을 양산하지 않는다.

## 7. Unity 이미지 제작 및 적용 규격

| 요소 | 납품·적용 원칙 |
|---|---|
| 저장 경로(신규용) | Assets/Art/Production/{GameplayHUD,Tutorial,SongJackets,Results,Settings}/. **지금은 제안 폴더이며 자동 생성된 것이 아니다.** 기존 Image/TitleUI 파일의 위치나 GUID를 함부로 바꾸지 않는다 |
| 형식 | PNG (UI/배경/초상), 실제 투명 알파 유지. 바둑판 무늬/흰 배경을 PNG 픽셀에 섞지 않는다 |
| Unity Import | 보통 Texture Type=Sprite(2D and UI), Sprite Mode=Single, Alpha Is Transparency=On, Pivot Center. 기존 스프라이트 시트는 Sprite Mode Multiple의 자른 내부 ID를 유지. Enemy01의 Texture2D 로딩 규칙은 예외 |
| UI Slice | 길게 늘어나는 튜토리얼·결과 프레임, 스크롤 트랙은 모서리 보호용 Border 지정 후 Image.Type=Sliced. 게이지 Fill은 별도 원본으로 늘려서 쓰고 테두리/수치 포함 금지 |
| 기본 픽셀·여백 | 기존 Canvas 1920×1080 기준에서 먼저 배치. 권장 PNG 크기=제작 캔버스, 렌더링 표시 크기와 같지 않음. 실측 전 고정 비율 강요 금지 |
| 색·선 | 확정 루루·피노와 동일한 낮은 광택/선 굵기. 기존 UI 색 팔레트에 맞추되 게임 안의 정보 대비는 충분하게 |
| 효과 | 연습 단계 진행·히트·미스·수치 경고·노트 수축은 C#으로 처리. 별도 발광 상태 PNG 추가 요구 없음 |
| 텍스트 | Paperlogy/TMP 등 실제 프로젝트 폰트 사용, 대사 한글은 Unity에서 렌더링. 이미지 내 문자/콤보 숫자 금지 |

## 8. 제작·연결 순서

1. **지금 보유한 이미지 재고의 실제 그림 내용 확인:** 특히 A9 판정 5종, 피노 Clear/False, 기존 배경 및 프레임. '존재'와 '사용 가능'을 구분한다.
2. **가장 먼저 새 마우스 모드용 안정화 게이지 3분리 PNG 제작/통합.** 현재 왼쪽 런타임 임시 게이지와 중복 없이 안정화 수치·피격·회복을 하나의 UI에 연결한다.
3. **곡 재킷 6곡 연결**. 먼저 이미 있는 SongJaket 파일 2장을 검수하되 정식 재킷으로 무단 지정하지 않는다.
4. **결과창 2부품 통합**. 기존 ResultPanel_Runtime 대신 게임기 그림체에 맞는 결과 패널을 구축하고 Clear/Failed 피노 애니메이션과 이어준다.
5. **튜토리얼 2프레임/화자 초상/4 조작 아이콘 통합**. 현재 코드형 안내를 시각적으로 완성하되 런타임 마우스 노트는 건드리지 않는다.
6. **타이틀/설정의 키보드 4레인 잔재 제거**, 스크롤이 필요하면 P1 스크롤바 2개를 통합한다. 기존 설정 컴포넌트 동작을 유지한다.
7. 배경·추가 탄막·스토리 추가 이미지는 P0 통합 뒤 실제 화면에서 필요하다고 확인되는 것만 작업한다.

## 9. 실제 완료 판단표

- [ ] 구형 4레인 안정화 PNG를 **사용하지 않음**. 새로운 마우스 전용 가로형 게이지가 3개 독립 Sprite로 구현돼 실제 안정화 수치에 연동.
- [ ] NoteLayer의 코드 생성 노트/L/R/W/수축 링/휠 레일을 유지. **새 노트 PNG를 요구하거나 적용하지 않음**.
- [ ] 여섯 곡 전용 재킷을 각 곡 ScriptableObject에 실제 연결하고 선택 시 전환 확인.
- [ ] 결과 화면에서 점수/랭크/판정 통계/최고기록/재시도/돌아가기가 기능과 그래픽 모두 완성.
- [ ] 확정 루루·피노의 비율·의상·머리 실루엣이 새 대화 이미지와 동일.
- [ ] Tutorial 프롤로그→체크포인트→실습→완료→곡 선택 흐름에 최종 이미지 적용. 안내 중 마우스 노트·탄막을 가리지 않음.
- [ ] 타이틀·설정과 새 화면의 선 굵기·명암·색감 통일. 구형 4레인 키 설정이 보이지 않음.
- [ ] 1920×1080, 1600×900, 1366×768 PC Game View에서 잘림/깨짐/겹침 없음.
- [ ] Title→Tutorial(최초)→SongSelect→Mouse(6곡)→Result→Retry/Back 및 Tutorial Replay를 Unity에서 **실제로 플레이 검증**.

이 항목이 통과하기 전에는 **이미지·UI·게임 완성**이라고 표시하지 않는다. 기존 보유 PNG의 육안 품질 검수가 아직 이 문서에서 수행된 것은 아니므로 완료 체크를 채우지 않는다.

## 10. 저장소 전체 이미지 재고 목록

GitHub main의 프로젝트 PNG를 자동 수집했다. Unity TextMesh Pro 패키지 예제 PNG는 제외. **총 85개**. 아래 표시 중 '보유'는 파일 유무만 확인된 상태. '직접 참조'는 활성 씬에서 GUID를 확인한 대표 자산에만 표시했다. 스크립트 동적 로드/이전 미사용 씬까지 전부 감사했다는 의미는 아니다.

| 현재 원본 경로 | 확인한 상태 |
|---|---|
| Assets/Image/06b18569-4828-43a1-a8cc-359aede1963f.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/A4_LaneInput_DJMAXStyle_8f.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/A4_LaneInput_KeyBeam_8f.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/A4_LaneInput_SoftKeyBeam_8f.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/A7_score_frame.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/A9_GOOD.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/A9_GREAT.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/A9_HIT.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/A9_MISS.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/A9_PERFECT.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/LULU_IN_BEAT_note_1x_2x_3x_4x_exact.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/2d655de8-18e3-4ea4-a47c-05c06637006c.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/3bc71f38-1dc0-49ed-85d5-70d4acac940c.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/6d65c6ec-1f31-45ea-a7f4-03a046af6aff.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/7b8fb912-6247-42aa-9d6f-78d0a1183a97.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/814a53bb-3387-43b6-a3cf-99ee978f67b2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/8e378945-6911-46b0-ab3e-9041eb1c597e.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/b26ada09-e925-4a62-8299-b095cc0423c2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/ui1.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/ui2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/ui3.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/ui4.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/ui5.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/다운로드.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/닫기.png | TitleScene 직접 참조 |
| Assets/Image/TitleUI/드롭다운2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/드롭박스1.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/로고.png | TitleScene 직접 참조 |
| Assets/Image/TitleUI/리바인드.png | TitleScene 직접 참조(6), 구형 키 설정 검토 |
| Assets/Image/TitleUI/배경.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/배경2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/배경3.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/배경4.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/배경5.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/배경6.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/배경7.png | TitleScene 직접 참조 |
| Assets/Image/TitleUI/버튼.png | TitleScene 직접 참조(10) |
| Assets/Image/TitleUI/버튼1.png | TitleScene 직접 참조(11) |
| Assets/Image/TitleUI/버튼2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/선택버튼.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/선택버튼2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/슬라이더.png | TitleScene 직접 참조(7) |
| Assets/Image/TitleUI/슬라이더게이지.png | TitleScene 직접 참조(14) |
| Assets/Image/TitleUI/카테고리선택박스.png | TitleScene 직접 참조(5) |
| Assets/Image/TitleUI/크레딧.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/타이틀버튼.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/화살표.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/TitleUI/확인창.png | TitleScene 직접 참조(3) |
| Assets/Image/b2777b12-6d28-4ccb-991b-8184867eb653.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/lulu_score_frame_fixed.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/게임판.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/기본_콤보.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/노트.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/노트2.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/롱노트.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/롱노트2.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/바이러스.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/배경.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image/안정화게이지.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/안정화게이지2.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/판정선.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/퍼펙트_콤보.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/피격노트.png | 구형 자산. 마우스 모드 적용 대상 아님 |
| Assets/Image/피노_Clear.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/피노_False.png | 보유/주요 활성 씬 직접 참조 없음; 검수 후 재사용 후보 |
| Assets/Image/피노_Hit.png | Mouse/Tutorial 직접 참조 |
| Assets/Image/피노_idle.png | Mouse/Tutorial 직접 참조 |
| Assets/Image/피노이동ui.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/HUD.1.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/HUD.2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/HUD.3.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/Lulu_UI_Frame_Light_Transparent.png | Mouse/Tutorial 직접 참조 |
| Assets/Image2/SongIllust/3052416a-4527-481a-ab39-fcaa8b46186a.png | PopnDash 계열 차트의 배경 참조 |
| Assets/Image2/SongIllust/e903736d-b607-4638-a29d-73c53bee9e6b.png | Mouse/Tutorial 직접 참조 |
| Assets/Image2/SongJaket/1f90cbe1-cd1b-4de3-8fe5-33aa78b97fcf.png | 재킷 후보 / 6곡 정식 지정 근거 없음 |
| Assets/Image2/SongJaket/다운로드 (1).png | 재킷 후보 / 6곡 정식 지정 근거 없음 |
| Assets/Image2/hud.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud2.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud3.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud4.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud5.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud6.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud7.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Image2/hud8.png | 보유(직접 연결 여부 및 내용 미확정) |
| Assets/Resources/MouseEnemies/Enemy01.png | 런타임 Texture2D Resources 로드(4×3 프레임) |

---
**명세 확정 근거:** Assets/Scene/{TitleScene,SongSelectScene,Mouse,Tutorial}.unity, Assets/Scripts/MousePlay/{MouseModeChartPlayer,MouseModeResultFlow,MouseTutorialController,MouseModeChartAsset}.cs, Assets/MouseMode/{MouseModeBulletDirector,MouseEnemy01TelegraphUI}.cs, Assets/Resources/SongSelectChartCatalog.asset, Assets/Image/*.png.meta, 프로젝트에 제공된 'UI 부품 분리 작업.txt'의 스크롤바 지시.
