# 스킬 마스터 테이블

| Column Name | Data Type | Key | Description | Example |
| --- | --- | --- | --- | --- |
| Skill_ID | VARCHAR(10) | PK | 스킬 고유 식별 번호 | SK001 |
| Skill_Type | VARCHAR(10) | - | 도망자 / 술래 | 도망자 |
| Character | VARCHAR(10) | - | 스킬을 사용하는 캐릭터 | 강아지 |
| Skill_Name | VARCHAR(30) | - | 스킬 이름 | 물어와! |
| Skill_Input | VARCHAR(20) | - | 스킬 사용 입력 방식 | 스킬 버튼 클릭 / 왼쪽 Shift |
| Cooldown | FLOAT | - | 재사용 대기시간 | 15 |
| Skill_Duration | FLOAT | - | 스킬 효과 지속시간 | 3 |
| Skill_Range | FLOAT | - | 스킬 적용 범위 및 사거리 | 10 |
| Target | VARCHAR(30) | - | 스킬 적용 대상 | 피격 플레이어 |
| Object_Limit | INT | - | 설치 가능한 오브젝트 최대 개수 | 2 |
| Object_Destroy_Condition_1 | VARCHAR(50) | - | 오브젝트가 제거되는 조건 1 | 플레이어 피격시 |
| Object_Destroy_Condition_2 | VARCHAR(50) | - | 오브젝트가 제거되는 조건 2 | 사거리 초과 시 |
| Object_Destroy_On_Contact | BOOLEAN | - | 대상과 접촉했을 때 제거 여부 | TRUE |
| skillRangeEnum | ENUM | - | 스킬 키를 누르는 동안 표시할 범위 형태 | DirectionLine |

| Skill_ID | Skill_Type | Character | Skill_Name | Skill_Input | Cooldown | Skill_Duration | Skill_Range | Target | Object_Limit | Object_Destroy_Condition_1 | Object_Destroy_Condition_2 | Object_Destroy_On_Contact | skillRangeEnum |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SK001 | 도망자 | 강아지 | 물어와! | 스킬 버튼 클릭 / 왼쪽 Shift | 20 | 4 | 10 | 피격 플레이어 | - | 플레이어 피격시 | 스킬 오브젝트가 사거리를 초과할 시 | TRUE | DirectionLine |
| SK002 | 도망자 | 고양이 | 생선 투척! | 스킬 버튼 클릭 / 왼쪽 Shift | 20 | 3 | 10 | 피격 플레이어 | - | 플레이어 피격시 | 스킬 오브젝트가 사거리를 초과할 시 | TRUE | DirectionLine |
| SK003 | 도망자 | 토끼 | 도망가자! | 스킬 버튼 클릭 / 왼쪽 Shift | 20 | 3 | - | 피격 플레이어 | - | - | - | TRUE | None |
| SK004 | 도망자 | 오리 | 놀랐잖아! | 스킬 버튼 클릭 / 왼쪽 Shift | 20 | 3 | 1 | 피격 플레이어 | 2 | 플레이어 피격시 | 설치 후 10초가 지날 시 | TRUE | SelfCircle |
| SK005 | 도망자 | 다람쥐 | 숨자! | 스킬 버튼 클릭 / 왼쪽 Shift | 20 | 5 | - | 피격 플레이어 | - | - | - | TRUE | None |
| SK006 | 술래 | ALL | 잡자! | 스킬 버튼 클릭 / 왼쪽 Shift | 20 | 3 | 1 | 피격 플레이어 | 3 | 플레이어 피격시 | 설치 후 10초가 지날 시 | TRUE | SelfCircle |

## 표시 설정

- `TargetCircle` — 마우스 조준점을 캐릭터의 `Skill_Range` 안에 두고 그 위치에 원을 그림. 원 크기는 에셋의 `Target Circle Radius`로 설정함.
- `DirectionLine` — 캐릭터에서 마우스 방향으로 조준점까지 직선을 그림. 길이는 `Skill_Range`에서 제한함.
- `SelfCircle` — 캐릭터를 중심으로 반지름 `Skill_Range`인 원을 그림.
- `None` — 범위를 표시하지 않음.

현재 `SkillDefinition` 에셋은 ID와 범위 표시 설정을 담음. CubePlayer의 `SkillRangePreview`에서 에셋을 선택함. 기본값은 SK001이며 경기에서 왼쪽 Shift를 누르는 동안 본인 화면에만 표시함.
