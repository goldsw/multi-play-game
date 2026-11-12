# 클라이언트 메뉴 아키텍처

## 범위

이 기능은 별도 `Assets/ClientUI` 폴더와 `ClientMenu` 씬으로 격리했다. 기존 로비, 이동, 네트워크 스크립트와 프로젝트 설정은 수정하지 않는다. 화면은 Unity uGUI로 런타임 구성하며, 모델·스프라이트·오디오가 아직 없어도 색상 타일과 텍스트로 동작한다.

경기 시뮬레이션, 캐릭터 조작, 실제 방 생성·참가, 네트워크 채팅, 서버 경제 검증은 구현 범위 밖이다. 방과 메시지 UI는 나중에 어댑터를 연결할 수 있도록 이벤트만 제공한다. 아이템/스킬 표의 효과 설명은 도감 표시용 문자열이며 실행하지 않는다.

## 구성

| 경로 | 책임 |
| --- | --- |
| `Assets/ClientUI/Runtime/Data/ClientMenuCatalog.cs` | 캐릭터·꾸미기·아이템·스킬·순위 보상 정의. 스프라이트, 프리팹, 오디오 에셋 슬롯 포함 |
| `Assets/ClientUI/Runtime/Data/ClientProfile.cs` | 재화, 선택 캐릭터, 보유·장착 꾸미기, 음량, 보상 수령 ID |
| `Assets/ClientUI/Runtime/Services/ClientProfileStore.cs` | 프로필 로드/저장, 기본값과 입력 정규화 |
| `Assets/ClientUI/Runtime/Services/ClientEconomyService.cs` | 구매 가능 여부, 장착, 순위 보상 계산 및 중복 수령 방지 |
| `Assets/ClientUI/Runtime/Composition/ClientMenuRoot.cs` | 카탈로그·프로필·경제 서비스·입력 시스템을 조립 |
| `Assets/ClientUI/Runtime/UI/ClientMenuController.cs` | 메뉴 탐색, 상점·캐릭터·설정·로비·메시지·결과 화면과 표시 이벤트 |
| `Assets/ClientUI/Resources/ClientMenuCatalog.asset` | 에셋 없는 기본 카탈로그. Unity Inspector에서 가격과 보상을 조정 |
| `Assets/ClientUI/Scenes/ClientMenu.unity` | 독립 실행 가능한 메뉴 시연 씬 |

## 데이터 흐름

`ClientMenuRoot`가 `ClientMenuCatalog`를 불러오고 `ClientProfileStore`와 `ClientEconomyService`를 생성한다. 화면은 서비스를 호출해 선택·구매·장착·보상 수령을 요청한다. 성공한 프로필 변경은 JSON으로 `Application.persistentDataPath/client-profile.json`에 저장되며 변경 이벤트가 상단 재화 표시를 갱신한다. Unity 기본 Input System UI 모듈로 마우스와 키보드 UI 입력을 받는다.

클라이언트 프로필의 재화는 로컬 데모 값이다. 서비스 출시 때는 서버가 잔액과 구매·보상 결과를 검증하고 저장하도록 교체해야 한다.

## 화면

- 메인 메뉴: 캐릭터 꾸미기, 상점, 방 화면, 보상 미리보기, 아이템/스킬 도감, 메시지, 설정으로 이동
- 캐릭터 선택: 강아지·고양이·토끼·오리·다람쥐 대표 캐릭터 설정 및 장착 현황 표시
- 상점: 스킨·사운드·승리 포즈 분류, 구매·장착·사운드 프리뷰
- 방 화면: 4인 슬롯과 로컬 준비 표시, 방 코드 입력. 실제 네트워크는 호출하지 않음
- 메시지: 입력과 최근 문장 표시. 연결 어댑터가 없으면 전송하지 않고 로컬 미리보기로만 표시
- 결과/보상: 순위를 선택해 지급 흐름 확인. 고유 미리보기 ID로 한 화면 내 중복 청구 차단
- 설정: 전체·음악·효과음 음량을 저장하고 전체 음량을 AudioListener에 적용

## 임시 경제 기본값

| 설정 | 임시값 |
| --- | ---: |
| 시작 재화 | 500잎 |
| 1~4위 보상 | 500 / 300 / 200 / 100잎 |
| 유료 스킨 | 150, 250잎 |
| 유료 사운드 | 100잎 |
| 유료 승리 포즈 | 200, 300잎 |

기본 수치는 플레이 밸런스 확정 전 조정용이다. 카탈로그 에셋에서 변경할 수 있다.

## 실행

Unity에서 `Assets/ClientUI/Scenes/ClientMenu.unity`를 열고 Play를 누른다. 기본 상품과 보상은 `Assets/ClientUI/Resources/ClientMenuCatalog.asset`에서 수정한다. 향후 에셋은 상품의 `icon`, `previewSprite`, `previewAudio`와 캐릭터의 `portrait`, `previewPrefab`에 연결한다. `RoomRequest`와 `MessageSubmitted` 이벤트를 실제 로비/채팅 어댑터가 구독하면 UI와 네트워크 구현을 분리한 채 연결할 수 있다.
