# Enemy01 탄막 사전 예고 — 설치 완료 상태

## 원본 스프라이트
- `Assets/Resources/MouseEnemies/Enemy01.png`
- 사용자 업로드 `적1.png`의 **원본 바이너리를 그대로** GitHub에 저장했다 (2048×1536, 4열×3행, 12프레임, 투명 배경).
- `Enemy01.png.meta`는 Read/Write 사용, Texture2D Default 임포트이며 이미지 자르기는 런타임에서 이루어진다. 수동 Sprite Editor 설정 불필요.

## 애니메이션과 예고
- 피노 대기 애니메이션과 같은 6FPS 핑퐁.
- 기존 `MouseModeBulletDirector`에서 실제 경고/발사 시점을 계산해 적을 **먼저** 보여준다.
- 일반탄·고급탄·Rain·Laser·Burst에 적용하며 실제 공격/노트 데이터는 바꾸지 않는다.
- 씬마다 직접 배치할 필요 없음. BulletLayer가 런타임에 `Enemy01_AnimatedPreAttack` 오브젝트를 생성한다.

## 적용 방법
1. GitHub Desktop에서 main 최신 변경을 Pull.
2. Unity에서 기존 Mouse 또는 Mouse_*.unity 씬을 Play.
3. 계층의 `BulletLayer/Enemy01_AnimatedPreAttack`에서 스프라이트 재생과 첫 탄막보다 앞선 등장을 확인.

Unity 에디터 직접 컴파일/실기 플레이는 수행되지 않았다. 실제 게임 화면에서 프레임/노트 가림/선행 시간 확인이 필요하다.
