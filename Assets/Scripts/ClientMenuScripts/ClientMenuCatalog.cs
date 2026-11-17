using System;
using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival.ClientUI
{
    // 메뉴 데이터에서 사용하는 캐릭터, 코스메틱, 아이템 분류입니다.
    public enum ClientCharacterId { Dog, Cat, Rabbit, Duck, Squirrel, Any }
    public enum ClientCosmeticKind { Skin, Voice, VictoryPose }
    public enum ClientItemKind { TrapItem, Throwable, Consumable }

    // 특정 Renderer 슬롯에 적용할 스킨 Material 묶음입니다.
    [Serializable]
    public sealed class ClientRendererMaterialOverride
    {
        [Tooltip("ClientCharacterPresenter의 슬롯 키입니다. 씬 오브젝트 참조가 아닌 표시 데이터입니다.")]
        public string rendererKey;
        public Material[] materials = Array.Empty<Material>();
    }

    // 캐릭터 선택 화면에 표시할 기본 정보입니다.
    [Serializable]
    public sealed class ClientCharacterDefinition
    {
        // 카탈로그에서 캐릭터를 구분하는 ID입니다.
        public ClientCharacterId id;
        // 화면에 표시할 이름입니다.
        public string displayName;
        [TextArea] public string description;
        // 캐릭터 선택 화면에서 표시할 초상화입니다.
        public Sprite portrait;
        [Tooltip("스킬 매니저가 스킬을 할당하는 캐릭터인지 여부만 표시합니다. 스킬 정의는 여기에 두지 않습니다.")]
        public bool hasAssignedSkill;
    }

    // 스킨, 음성, 승리 포즈의 상품 정보와 미리보기 에셋입니다.
    [Serializable]
    public sealed class ClientCosmeticDefinition
    {
        // 상품을 저장하고 조회할 때 사용하는 고유 ID입니다.
        public string id;
        // 상품 종류와 사용할 수 있는 캐릭터입니다. Any는 공용 상품입니다.
        public ClientCosmeticKind kind;
        public ClientCharacterId character;
        // 화면에 표시할 상품 이름입니다.
        public string displayName;
        [TextArea] public string description;
        // 구매 가격과 목록 아이콘입니다.
        [Min(0)] public int price;
        public Sprite icon;
        // 상세 화면에 표시할 이미지, 오디오, Animator 상태입니다.
        public Sprite previewSprite;
        public AudioClip previewAudio;
        public AudioClip characterAudio;
        [Tooltip("클라이언트 승리 포즈 미리보기에 사용할 Animator 상태 이름입니다.")]
        public string animatorState;
        // 스킨 적용 시 Renderer 키별로 덮어쓸 Material 목록입니다.
        public ClientRendererMaterialOverride[] materialOverrides = Array.Empty<ClientRendererMaterialOverride>();
    }

    // 아이템 안내 화면의 메타데이터입니다. 실제 효과 실행은 담당하지 않습니다.
    [Serializable]
    public sealed class ClientItemDefinition
    {
        // 아이템 ID, 표시 이름, 메뉴 분류 및 아이콘입니다.
        public string itemId;
        public string displayName;
        public ClientItemKind itemType;
        public Sprite iconSprite;
        public Sprite tileSprite;
        // 지속 시간, 범위, 대상 태그와 배치 제한 등 표시·설정용 값입니다.
        [Min(-1)] public float activeDurationSeconds = -1f;
        [TextArea] public string effectDescription;
        [Min(0)] public int throwRange;
        [Min(0)] public int skillTileRange;
        public string targetTag;
        [Min(0)] public int objectLimit;
        public bool isDestroyOnCollision;
    }

    /// <summary>Inspector에서 입력하는 메뉴 데이터 에셋입니다. 스킬 실행 정의는 포함하지 않습니다.</summary>
    [CreateAssetMenu(fileName = "ClientMenuCatalog", menuName = "Mushroom Survival/Client Menu Catalog")]
    public sealed class ClientMenuCatalog : ScriptableObject
    {
        // 로컬 미리보기용 시작 재화와 등수별 보상입니다.
        [Header("Economy defaults: temporary balance values")]
        [Min(0)] public int startingCurrency = 500;
        public int[] rankRewards = { 500, 300, 200, 100 };
        // 화면에서 사용할 캐릭터·코스메틱·아이템 데이터 목록입니다.
        [Header("Catalogs")]
        public List<ClientCharacterDefinition> characters = new List<ClientCharacterDefinition>();
        public List<ClientCosmeticDefinition> cosmetics = new List<ClientCosmeticDefinition>();
        public List<ClientItemDefinition> items = new List<ClientItemDefinition>();

        // ID가 일치하는 캐릭터 정의를 반환합니다.
        public ClientCharacterDefinition FindCharacter(ClientCharacterId id)
        {
            return characters != null ? characters.Find(entry => entry != null && entry.id == id) : null;
        }

        // ID가 일치하는 코스메틱 정의를 반환합니다.
        public ClientCosmeticDefinition FindCosmetic(string id)
        {
            return cosmetics != null ? cosmetics.Find(entry => entry != null && entry.id == id) : null;
        }
    }
}
