using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoolDownButton))]
    public sealed class CooldownSlot : MonoBehaviour
    {
        [SerializeField] private Image icon;
        private CoolDownButton timer;

        public CoolDownButton Timer => timer;
        public bool IsEquipped => icon.sprite != null;

        private void Awake() => timer = GetComponent<CoolDownButton>();

        public void Configure(Image iconImage)
        {
            icon = iconImage;
            Init(null);
        }

        public void Init(Sprite equippedIcon)
        {
            icon.sprite = equippedIcon;
            icon.enabled = equippedIcon != null;
            timer.Clear();
        }

        public void Unequip() => Init(null);
    }
}
