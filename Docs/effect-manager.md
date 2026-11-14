# 플레이어 상태 효과

`CubePlayer`마다 `EffectManager`가 하나 있음. 호스트의 스킬·아이템·경기 규칙에서 대상 플레이어에게 같은 `EffectDefinition` 에셋을 전달할 수 있음.

## 구성

- `EffectDefinition` 에셋에 지속시간과 재적용 정책을 설정함. Unity의 Create → Mushroom Survival → Effects → Movement Effect에서 스턴·속도 변경·강제 이동 에셋을 생성할 수 있음.
- `EffectManager`가 호스트 NGO 틱마다 만료된 효과를 제거하고 대기 요청을 FIFO 순서로 적용함. 각 효과의 종료 시각을 따로 기록하고, 활성 목록이 바뀌면 `ActiveEffectsChanged`를 호출함. 디스폰 시 큐와 활성 목록을 비움.
- `MovementEffectSync`가 활성 이동 효과를 합성하고 소유자에게 상태를 동기화함. `RigidbodyMovement`가 이를 물리 틱마다 읽어 벽·플레이어 충돌 제한을 거쳐 이동함.

## 중첩 규칙

- 에셋 참조를 효과의 식별자로 사용함. `Refresh`는 같은 에셋의 기존 적용을 지우고 지속시간을 다시 시작함. `Independent`는 같은 에셋도 개별 종료 시각을 가짐. 서로 다른 에셋은 동시에 유지됨.
- 스턴이 하나라도 활성 상태면 수평 이동을 정지함. 정지 중에도 효과는 원래 시간에 만료됨.
- 강제 이동은 가장 최근에 적용된 활성 효과의 방향과 에셋에 설정된 속도(m/s)를 사용함. 방향은 요청 시 월드 X/Z로 전달함. 입력보다 강제 이동이 우선하고, 스턴이 수평 이동을 멈춤.
- 일반 이동 속도는 가장 강한 감속 배율과 가장 강한 가속 배율을 곱함. 감속 0.5와 가속 1.2가 겹치면 0.6배임.

## 스킬·아이템 연결

```csharp
// 호스트가 스킬이나 아이템의 적중을 확정하면 호출함.
targetEffects.TryEnqueue(new EffectRequest(stunEffect));
```

바나나 적중 처리는 스턴 에셋을 요청함. 물웅덩이의 타일 변환은 맵 시스템이, 사과의 투사체 이동과 제거는 투사체 코드가 담당함. [아이템 시스템 기획](https://app.notion.com/p/4157ed37c83380dbbb7aea9529a65022)
