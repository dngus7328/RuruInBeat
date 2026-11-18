# 루루 인 비트 — 마우스 전용 6곡 통합 채보 패키지

## 적용 범위

모든 곡은 현재 마우스 입력(L/R/휠) 구조를 사용한다. 예전 4레인 레거시 파일은 변경하지 않는다.

| 음원 | 신설 채보 Asset | 노트 | 탄막 | 난도(임시) |
|---|---|---:|---:|---:|
| BOOTBEAT.wav | BOOTBEAT_MOUSE_LV6 | 164 | 9 | Lv.6 |
| BOOTBEAT.wav | BOOTBEAT_MOUSE_LV11 | 271 | 18 | Lv.11, 추가 |
| Pop'n Dash.mp3 | popn_dash | 200 | 22 | Lv.8 |
| Full-Band Hit.wav | FullBandHit_MOUSE | 396 | 7 | Lv.13 |
| RhythmGameTheme.mp3 | RhythmGameTheme_MOUSE | 229 | 8 | Lv.5 |
| ArcadeGroove.mp3 | ArcadeGroove_MOUSE | 338 | 11 | Lv.9 |
| MabatakiNoSenritsu.mp3 | MabatakiNoSenritsu_MOUSE | 409 | 12 | Lv.14 |

개별 장면: `Assets/Scene/Mouse_<곡ID>.unity`.
채보 원본 JSON: `Assets/Editor/<곡ID>.json`.
게임 내 적용용 에셋: `Assets/MouseMode/ChartSystem/Charts/<곡ID>.asset`.

## 신규 3곡 음원 설치가 아직 필요함
GitHub 연결 도구는 이 작업에서 MP3 바이너리를 업로드하지 못하므로, `Assets/Song/RhythmGameTheme.mp3`, `Assets/Song/ArcadeGroove.mp3`, `Assets/Song/MabatakiNoSenritsu.mp3` 3개는 ZIP 패키지에서 직접 복사해야 한다. 세 MP3의 `.meta` 파일은 GitHub와 ZIP에 같이 포함되어 GUID가 일치한다. 별도의 MP3 파일을 자동 생성하거나 다른 음원으로 대체하지 않았다.

## 설계 및 확인한 사항
- 새 3곡: 업로드 음원의 저·중·고역 온셋에서 타격 후보 추출. 여러 소리가 같은 시간에 날 때 중복 타격을 무작정 쌓지 않도록 합쳤다.
- 새로운 채보의 `tempoChanges`는 자동 박자 추적 결과를 음악 길이 전반에 분배한 **검토용 템포맵**이다. 확정 작곡 BPM/마디표는 아니다.
- 기존 BOOTBEAT/Pop'n Dash는 사용자의 이전 채보 타격 시각을 보존하는 방향으로 배치했고, Full-Band Hit은 과거 818개 타격시각을 새 마우스 채보 약 396개로 정리하였다.
- 밀집 구간 `rain` 탄막은 `gap=7`에 해당하는 넓은 **통과 지점** 안전 틈새를 사용하고, 노트는 그 구간 근처로 이동시켰다. 실제 탄도와 전체 커서 경로의 충돌까지 보장하진 않는다.
- 모든 노트는 900×450 가상 플레이 영역, 노트 본체 거리 137px, 표시 1.75초, 연결 구간 이동 속도 1200px/s 기준으로 정적 검사.
- 실제 게임의 접근 링은 본체보다 커서 서로 겹칠 수 있으며, 노트의 시각적 중요도·우선순위도 반드시 실플레이 확인해야 한다.

## Unity 검증
1. MP3 3개를 원래 `.meta` GUID와 함께 `Assets/Song`에 넣고 Unity에서 컴파일 오류 확인.
2. `Tools > Lulu In Beat > Verify Tempo Map Timing` 실행(기본 BPM 변환 테스트).
3. `Tools > Lulu In Beat > Verify All Mouse Charts` 실행(모든 곡 노트/패턴/오디오 링크 정적 검사).
4. 개별 `Mouse_*.unity` 씬을 열고 Play Mode에서 음악-판정 시간, 휠/좌우클릭, 탄막 경고, 충돌, 실제 가독성 확인.
5. 실제 플레이 결과를 반영해 레벨과 구간별 BPM/노트 위치/탄막 이동을 추가 조정.

## 상태 (주의)
**음향 대역 온셋 기반 제작 및 정적 검증까지 완료.**
이 환경에서는 Unity Editor 컴파일 및 실제 플레이 불가능. 인간 청음에 의한 악기-노트 대응, 임의 곡 템포의 정확한 소스 BPM, 탄막 완전 회피 가능성, 실기 조작감이 확인된 최종 채보라고 주장하지 않는다.
기존 main은 건드리지 않고 작업 브랜치에서 검증을 기다린다.
