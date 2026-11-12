using System;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    public enum ClientCharacterId
    {
        Dog,
        Cat,
        Rabbit,
        Duck,
        Squirrel,
        Any
    }

    public enum ClientCosmeticKind
    {
        Skin,
        Voice,
        VictoryPose
    }

    public enum ClientItemKind
    {
        TrapItem,
        Throwable,
        Consumable
    }

    public enum ClientSkillRole
    {
        Runner,
        Chaser
    }

    [Serializable]
    public sealed class ClientCharacterDefinition
    {
        public ClientCharacterId id;
        public string displayName;
        [TextArea] public string description;
        public Color placeholderColor = Color.white;
        public Sprite portrait;
        public GameObject previewPrefab;
    }

    [Serializable]
    public sealed class ClientCosmeticDefinition
    {
        public string id;
        public ClientCosmeticKind kind;
        public ClientCharacterId character = ClientCharacterId.Any;
        public string displayName;
        [TextArea] public string description;
        [Min(0)] public int price;
        public Sprite icon;
        public Sprite previewSprite;
        public AudioClip previewAudio;
        public string animatorState;
    }

    /// <summary>
    /// Read-only presentation data for the item table. The effect text is never evaluated as code.
    /// </summary>
    [Serializable]
    public sealed class ClientItemDefinition
    {
        public string itemId;
        public string displayName;
        public ClientItemKind itemType;
        public Sprite iconSprite;
        public Sprite tileSprite;
        [Min(-1f)] public float activeDurationSeconds = -1f;
        [TextArea] public string effectDescription;
        [Min(0)] public int throwRange;
        [Min(0)] public int skillTileRange;
        public string targetTag;
        [Min(0)] public int objectLimit;
        public bool isDestroyOnCollision;
    }

    /// <summary>
    /// Read-only client catalog data. Runtime gameplay behavior belongs to the match implementation.
    /// </summary>
    [Serializable]
    public sealed class ClientSkillDefinition
    {
        public string skillId;
        public string displayName;
        public ClientCharacterId character = ClientCharacterId.Any;
        public ClientSkillRole role;
        [Min(-1f)] public float cooldownSeconds = -1f;
        [Min(-1f)] public float activeDurationSeconds = -1f;
        [Min(0)] public int skillTileRange;
        [TextArea] public string effectDescription;
        public Sprite iconSprite;
    }

    [CreateAssetMenu(fileName = "ClientMenuCatalog", menuName = "Mushroom Survival/Client Menu Catalog")]
    public sealed class ClientMenuCatalog : ScriptableObject
    {
        [Header("Economy defaults (tune these values before release)")]
        [Min(0)] public int startingCurrency = 500;
        [Tooltip("Index 0 is 1st place. Values are intentionally easy to tune while match systems are out of scope.")]
        public int[] rankRewards = { 500, 300, 200, 100 };

        [Header("Character and cosmetic catalog")]
        public ClientCharacterDefinition[] characters = Array.Empty<ClientCharacterDefinition>();
        public ClientCosmeticDefinition[] cosmetics = Array.Empty<ClientCosmeticDefinition>();

        [Header("Client-facing reference tables; no gameplay effects are executed here")]
        public ClientItemDefinition[] items = Array.Empty<ClientItemDefinition>();
        public ClientSkillDefinition[] skills = Array.Empty<ClientSkillDefinition>();

        public ClientCharacterDefinition FindCharacter(ClientCharacterId id)
        {
            if (characters == null) return null;
            for (int i = 0; i < characters.Length; i++)
                if (characters[i] != null && characters[i].id == id) return characters[i];
            return null;
        }

        public ClientCosmeticDefinition FindCosmetic(string id)
        {
            if (cosmetics == null) return null;
            for (int i = 0; i < cosmetics.Length; i++)
                if (cosmetics[i] != null && cosmetics[i].id == id) return cosmetics[i];
            return null;
        }

        public static ClientMenuCatalog CreateFallback()
        {
            var catalog = CreateInstance<ClientMenuCatalog>();
            catalog.name = "Runtime Client Menu Defaults";
            catalog.startingCurrency = 500;
            catalog.rankRewards = new[] { 500, 300, 200, 100 };
            catalog.characters = new[]
            {
                Character(ClientCharacterId.Dog, "강아지", "마을의 리더", new Color(0.78f, 0.55f, 0.32f)),
                Character(ClientCharacterId.Cat, "고양이", "자기주장이 강한 친구", new Color(0.67f, 0.60f, 0.78f)),
                Character(ClientCharacterId.Rabbit, "토끼", "숲을 빠르게 살피는 친구", new Color(0.93f, 0.76f, 0.78f)),
                Character(ClientCharacterId.Duck, "오리", "물웅덩이를 좋아하는 친구", new Color(0.96f, 0.82f, 0.35f)),
                Character(ClientCharacterId.Squirrel, "다람쥐", "성실한 수집가", new Color(0.68f, 0.40f, 0.25f))
            };
            catalog.cosmetics = new[]
            {
                Cosmetic("skin_default", ClientCosmeticKind.Skin, "기본 숲 친구", "기본 외형", 0),
                Cosmetic("skin_moss", ClientCosmeticKind.Skin, "이끼 숲 산책", "준비 중인 스킨 미리보기", 150),
                Cosmetic("skin_berry", ClientCosmeticKind.Skin, "산딸기 축제", "준비 중인 스킨 미리보기", 250),
                Cosmetic("voice_default", ClientCosmeticKind.Voice, "기본 목소리", "기본 캐릭터 사운드", 0),
                Cosmetic("voice_forest", ClientCosmeticKind.Voice, "숲의 인사", "준비 중인 사운드 미리보기", 100),
                Cosmetic("pose_default", ClientCosmeticKind.VictoryPose, "기본 승리 포즈", "기본 포즈", 0),
                Cosmetic("pose_dance", ClientCosmeticKind.VictoryPose, "버섯 축하 춤", "준비 중인 포즈 미리보기", 200),
                Cosmetic("pose_wave", ClientCosmeticKind.VictoryPose, "숲 친구에게 손 흔들기", "준비 중인 포즈 미리보기", 300)
            };
            catalog.items = new[]
            {
                new ClientItemDefinition
                {
                    itemId = "ItemID_Water_Bucket", displayName = "물웅덩이", itemType = ClientItemKind.TrapItem,
                    activeDurationSeconds = 5f, effectDescription = "speed = speed / 50;",
                    throwRange = 2, skillTileRange = 1, targetTag = "Ground", objectLimit = 3,
                    isDestroyOnCollision = false
                },
                new ClientItemDefinition
                {
                    itemId = "ItemID_Apple", displayName = "사과", itemType = ClientItemKind.Throwable,
                    activeDurationSeconds = -1f, effectDescription = "피격 시 스턴 (지속 시간은 데이터 설정값).",
                    targetTag = "Player"
                },
                new ClientItemDefinition
                {
                    itemId = "ItemID_Banana", displayName = "바나나", itemType = ClientItemKind.TrapItem,
                    activeDurationSeconds = -1f, effectDescription = "밟은 플레이어가 미끄러지고 스턴됩니다.",
                    targetTag = "Ground", objectLimit = 1
                }
            };
            catalog.skills = new[]
            {
                Skill("runner_dog", "물어와!", ClientCharacterId.Dog, ClientSkillRole.Runner, "대상에게 반대 방향 이동 효과를 적용합니다."),
                Skill("runner_cat", "생선 투척!", ClientCharacterId.Cat, ClientSkillRole.Runner, "대상의 화면을 잠시 가립니다."),
                Skill("runner_rabbit", "도망가자!", ClientCharacterId.Rabbit, ClientSkillRole.Runner, "일정 시간 이동 속도를 높입니다."),
                Skill("runner_duck", "놀랐잖아!", ClientCharacterId.Duck, ClientSkillRole.Runner, "알을 최대 2개 설치합니다."),
                Skill("runner_squirrel", "숨자!", ClientCharacterId.Squirrel, ClientSkillRole.Runner, "일정 시간 모습을 숨깁니다."),
                Skill("chaser_trap", "잡자!", ClientCharacterId.Any, ClientSkillRole.Chaser, "덫을 최대 3개 설치합니다.")
            };
            return catalog;
        }

        private static ClientCharacterDefinition Character(ClientCharacterId id, string name, string description, Color color)
        {
            return new ClientCharacterDefinition { id = id, displayName = name, description = description, placeholderColor = color };
        }

        private static ClientCosmeticDefinition Cosmetic(string id, ClientCosmeticKind kind, string name, string description, int price)
        {
            return new ClientCosmeticDefinition { id = id, kind = kind, character = ClientCharacterId.Any, displayName = name, description = description, price = price };
        }

        private static ClientSkillDefinition Skill(string id, string name, ClientCharacterId character, ClientSkillRole role, string description)
        {
            return new ClientSkillDefinition
            {
                skillId = id, displayName = name, character = character, role = role,
                cooldownSeconds = -1f, activeDurationSeconds = -1f, effectDescription = description
            };
        }
    }
}
