using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    internal sealed class CooldownSlotPress : MonoBehaviour, IPointerDownHandler
    {
        private Action pressed;

        public void Configure(Action onPressed) => pressed = onPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                pressed?.Invoke();
        }
    }
}
