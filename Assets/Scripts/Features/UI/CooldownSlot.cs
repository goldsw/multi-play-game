using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CooldownTimer))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CooldownSlot : MonoBehaviour
    {
        [SerializeField] private Image icon;
        private CooldownTimer timer;
        private CanvasGroup interaction;

        public CooldownTimer Timer => timer;
        public bool IsEquipped => icon.sprite != null;
        public bool IsLocked => !interaction.interactable;

        private void Awake()
        {
            timer = GetComponent<CooldownTimer>();
            interaction = GetComponent<CanvasGroup>();
        }

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

        public void SetLocked(bool locked)
        {
            interaction.interactable = !locked;
            interaction.blocksRaycasts = !locked;
            interaction.alpha = locked ? 0.45f : 1f;
        }
    }
}
