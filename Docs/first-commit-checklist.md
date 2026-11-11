# 첫 커밋 검토 — 4인 Relay 접속 틀

2026-11-11 기준 접속 틀을 구현하고 Windows 개발 빌드로 검증함.

## 실행

1. Unity에서 [MultiplayerLobby 씬](../Assets/Scenes/MultiplayerLobby.unity)을 열고 Play 실행함.
2. 시작하면 자기 Relay 룸을 만들고 왼쪽 위에 룸 코드를 표시함.
3. 참가자는 방 찾기에서 상대 룸 코드를 입력함. 화면 오른쪽부터 0번 슬롯을 채움. 게스트의 방 나가기는 자기 룸 복귀로 이어짐.
4. Unity 에디터와 Development Build에서는 방장 혼자 시작할 수 있음. 일반 빌드는 4명 접속이 필요함. 호스트가 참가자에게 서로 다른 경기 스폰 지점을 무작위로 배정함. 각 소유자의 스폰 확인 뒤 Playing으로 전환함.
5. WASD 또는 게임패드 왼쪽 스틱으로 자기 큐브를 이동함. 상대 화면에는 소유자 위치가 보간되어 표시됨.
6. 경기에서 방을 나가면 네트워크 종료 완료 뒤 새 자기 룸으로 돌아옴.

빌드의 첫 씬으로 등록됨. 씬·프리팹 등록은 Unity 메뉴 Tools → Mushroom Survival → Create Multiplayer Lobby에서 확인할 수 있음.

## 구조

- **Data** — `SessionData`는 화면에 전달할 읽기 전용 상태, `SessionStatus`는 접속 단계, `LobbySettings`는 공통 인원 설정임.
- **Managers** — `SessionManager`가 접속·시작·종료 순서를 조율하고, `LobbyPresenter`가 화면 이벤트와 매니저를 연결함.
- **Networking 기능** — Relay 연결, 인원 제한·시작 후 입장 차단, 로비 상태 복제, 큐브 스폰, 소유자의 스폰 확인, 소유자 권한 Transform을 각각 분리함.
- **UI 기능** — `LobbyView`는 상태 표시와 버튼 이벤트를 담당함.
- **패턴** — `ISessionConnector` 전략으로 실제 Relay와 개발용 로컬 연결을 교체함. 이벤트 기반 Observer와 생성 지점의 의존성 주입으로 연결함.
- **프로토타입 화면** — 초기 기능 검증을 위해 IMGUI 로비를 사용함. 큐브와 경기장은 URP Lit 재질을 사용함.

## 검증 기록

- Unity 6000.3.15f1에서 NGO 2.14.0, Multiplayer Services 2.4.1 패키지 해결과 C# 컴파일 성공함.
- Windows 개발 빌드 성공함.
- Prefabs·Resources·Scripts 구조로 이동 후 새 씬 경로로 Unity 컴파일·Windows 개발 빌드를 다시 검증함.
- 동일 PC에서 실행 파일 네 개와 별도 익명 인증 프로필을 사용해 실제 Relay 방에 호스트 1명·게스트 3명이 접속함.
- 네 프로세스 모두 소유한 큐브와 총 네 큐브의 스폰을 확인했고 Playing 상태에 도달함.
- 최종 네 프로세스 로그에서 smoke 실패·예외·LogError가 각각 0건임.
- 실제 Cloud 연결 정보는 정상 확인함. 작업 중 미연결로 안내했던 것은 잘못된 판정이었음.
- 별도 PC와 지연·패킷 손실 환경에서의 검증은 다음 통합 검증 항목임.

개발 빌드에만 포함되는 테스트 드라이버는 `--smoke-relay-host`, `--smoke-relay-client CODE` 인자로 실행 가능함. 각 프로세스가 임시 인증 프로필을 사용해 같은 PC에서도 별도 참가자로 동작함. 로컬 전송 검증에는 `--smoke-host`, `--smoke-client` 인자를 사용함. 방장 단독 검증은 호스트 인자에 `--smoke-solo`를 추가함.

첫 게스트에 `--smoke-room-switch`를 추가하면 자기 룸 → 상대 룸 → 자기 룸 → 상대 룸을 순서대로 검증함. 퇴장한 슬롯을 재사용하면 나머지 게스트를 입장시켜 4인 스폰을 확인함.

4인 테스트의 모든 프로세스에 `--smoke-move`를 추가하면 게스트 입력의 위치 전파와 호스트 보정을 검증함. 테스트 드라이버가 1번 게스트를 움직이고, 호스트 큐브와의 겹침을 재현해 게스트만 안전 위치로 복귀하는지 확인함.

검증 로그와 개발 빌드는 워크스페이스의 `.verify/`에 있음. 최종 빌드 로그는 `build-final.log`, 접속 로그는 `relay-final-host.log`와 `relay-final-client-1.log`부터 `relay-final-client-3.log`임.

## 이번 커밋 범위

- `Assets/Scripts/`의 신규 코드·메타, `Assets/Prefabs/`의 프리팹·등록 자산, `Assets/Resources/`의 재질·리소스 폴더, `Assets/Scenes/MultiplayerLobby.unity`와 대응하는 메타.
- `Packages/manifest.json`, `Packages/packages-lock.json`의 네트워크 패키지 변경.
- `ProjectSettings/EditorBuildSettings.asset`의 첫 씬 등록.
- 이 체크리스트와 앞서 합의한 아키텍처·A/B 지침서.

기존 스테이징 내역에 다른 초기 프로젝트 파일도 있으므로, 위 경로를 기준으로 변경 범위를 검토함.

다음 커밋 단위는 클라이언트 권한 Rigidbody 이동·위치 동기화·벽 충돌·플레이어끼리 길 막기의 연결 검증임.
