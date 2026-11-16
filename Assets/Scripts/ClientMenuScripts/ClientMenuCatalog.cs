using System;
using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    public enum ClientCharacterId { Dog, Cat, Rabbit, Duck, Squirrel, Any }
    public enum ClientCosmeticKind { Skin, Voice, VictoryPose }
    public enum ClientItemKind { TrapItem, Throwable, Consumable }

    [Serializable]
    public sealed class ClientRendererMaterialOverride
    {
        [Tooltip("ClientCharacterPresenter slot key; this is presentation data, not a scene object reference.")]
        public string rendererKey;
        public Material[] materials = Array.Empty<Material>();
    }

    [Serializable]
    public sealed class ClientCharacterDefinition
    {
        public ClientCharacterId id;
        public string displayName;
        [TextArea] public string description;
        public Sprite portrait;
        [Tooltip("스킬 매니저가 스킬을 할당하는 캐릭터인지 여부만 표시합니다. 스킬 정의는 여기에 두지 않습니다.")]
        public bool hasAssignedSkill;
    }

    [Serializable]
    public sealed class ClientCosmeticDefinition
    {
        public string id;
        public ClientCosmeticKind kind;
        public ClientCharacterId character;
        public string displayName;
        [TextArea] public string description;
        [Min(0)] public int price;
        public Sprite icon;
        public Sprite previewSprite;
        public AudioClip previewAudio;
        public AudioClip characterAudio;
        [Tooltip("Animator state used by the client-only victory-pose preview.")]
        public string animatorState;
        public ClientRendererMaterialOverride[] materialOverrides = Array.Empty<ClientRendererMaterialOverride>();
    }

    [Serializable]
    public sealed class ClientItemDefinition
    {
        public string itemId;
        public string displayName;
        public ClientItemKind itemType;
        public Sprite iconSprite;
        public Sprite tileSprite;
        [Min(-1)] public float activeDurationSeconds = -1f;
        [TextArea] public string effectDescription;
        [Min(0)] public int throwRange;
        [Min(0)] public int skillTileRange;
        public string targetTag;
        [Min(0)] public int objectLimit;
        public bool isDestroyOnCollision;
    }

    /// <summary>Inspector-authored data only. No skill execution definitions belong in the client menu catalog.</summary>
    [CreateAssetMenu(fileName = "ClientMenuCatalog", menuName = "Mushroom Survival/Client Menu Catalog")]
    public sealed class ClientMenuCatalog : ScriptableObject
    {
        [Header("Economy defaults: temporary balance values")]
        [Min(0)] public int startingCurrency = 500;
        public int[] rankRewards = { 500, 300, 200, 100 };
        [Header("Catalogs")]
        public List<ClientCharacterDefinition> characters = new List<ClientCharacterDefinition>();
        public List<ClientCosmeticDefinition> cosmetics = new List<ClientCosmeticDefinition>();
        public List<ClientItemDefinition> items = new List<ClientItemDefinition>();

        public ClientCharacterDefinition FindCharacter(ClientCharacterId id)
        {
            return characters != null ? characters.Find(entry => entry != null && entry.id == id) : null;
        }

        public ClientCosmeticDefinition FindCosmetic(string id)
        {
            return cosmetics != null ? cosmetics.Find(entry => entry != null && entry.id == id) : null;
        }
    }
}
