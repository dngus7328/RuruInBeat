# SongSelect — 이미지 없는 도형 UI (초기 1회 생성)

## 처음 설치할 때
1. GitHub에서 main 최신 내용을 Pull.
2. Unity에서 `Assets/Scene/SongSelectScene.unity` 열기.
3. 씬이 **Main Camera 한 개만 있는 완전히 빈 상태**이면, 에디터가 텍스트·도형·버튼을 단 한 번 생성해 **씬 파일에 저장**한다.
4. 생성이 안 될 경우: `Tools > Lulu In Beat > Song Select > Create Layout Only If Blank` 클릭.
5. Play로 6곡 목록/미리듣기/난이도/PLAY/BACK 작동을 시험한다.

## 배치 보호
- 단 한 번 생성한 뒤에는 카메라 외에 오브젝트가 생기기 때문에 **자동 생성은 영구적으로 중단**.
- 버튼의 좌표, 앵커, 크기, 자식 계층은 런타임에서도 변경되지 않는다.
- **사용자가 Unity에서 옮기거나 크기 조절한 배치는 스크립트 수정/업데이트 후에도 유지**.
- 에디터 메뉴를 다시 눌러도 씬이 비어 있지 않으면 아무것도 변경하지 않는다.
- 변경되는 항목은 텍스트 내용, 버튼 선택 상태/색, 재킷 Sprite, 오디오 미리듣기 상태 뿐.
- 향후 PNG를 넣고 싶다면 Image 컴포넌트에 Sprite를 넣거나 catalog 항목의 `jacket` 필드를 연결. RectTransform은 그대로 둬도 됨.

## 실제 기능
- 기존 6곡, BOOTBEAT 추가 고난도 1개를 채보 에셋에서 그대로 불러옴.
- 리스트 곡 클릭 -> 우측 상세 정보(레벨/BPM/길이) 갱신.
- BOOTBEAT에서만 < > 난이도 전환 활성화. 다른 5곡은 현 상태 하나만 제공.
- 선택 시 짧은 미리듣기. PREVIEW 버튼으로 켜고 끄기.
- START GAME은 해당 `Mouse_*.unity` 씬으로 이동.
- BACK TO TITLE은 기존 `TitleScene.unity`으로 이동.
- 에디터 생성 직후 필요한 씬 경로를 **기존 등록 목록을 보존한 상태로** Build Settings에 추가.

## 한계
- 현재 선택 화면에는 재킷 PNG가 없으므로 단색 플레이스홀더를 사용.
- UI는 1920x1080 기준, PC 화면 스케일 대응으로 설계한 도형 초안임.
- 새 음원 MP3 import / TMP 일본어 글리프·폰트 fallback은 Unity에서 확인 필요.
- 각 난이도의 마우스 채보 싱크·실플레이 검증은 별개.
- Unity 컴파일 및 Play Mode 실행은 이 환경에서 수행하지 못했음.
