# 설정창 스크롤바 리소스

`Assets/Image/TitleUI/Scrollbar_Track.png` 및 `Scrollbar_Handle.png`는 별도 투명 PNG 스프라이트입니다. 설정용 기존 버튼/슬라이더/토글은 그대로 보존합니다.

- Track: 32×320, 세로 고정 배경. Unity `Image`에 연결합니다.
- Handle: 32×96, 세로 이동 손잡이. Unity `Scrollbar > Sliding Area > Handle`의 `Image`에 연결합니다.
- `Scrollbar.direction = BottomToTop` 또는 `TopToBottom`으로 사용하고, 실제 ScrollRect는 현재 UI 스크롤 방향에 맞춰 연결합니다.
- Track와 Handle은 글자와 아이콘 없이 단정한 평면 색상으로 분리했으며 텍스처에는 글로우/광택/선택 상태가 없습니다.
- Unity의 선택·호버·드래그 반응은 Scrollbar `Transition`이나 스크립트에서 적용합니다.
- 스프라이트의 `.meta` 파일도 포함합니다.

**주의:** 리소스 추가만 수행했습니다. 기존 씬의 Scrollbar를 자동 생성하거나 연결하지 않았으며 Unity 에디터 실기기 실행 검증은 하지 않았습니다.
