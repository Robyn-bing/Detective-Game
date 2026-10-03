using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace DetectiveGame.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class RadialMenuPointerHandler : MonoBehaviour, IPointerClickHandler
    {
        private Action clicked;

        public void Configure(Action onClicked) => clicked = onClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke();
        }
    }
}
