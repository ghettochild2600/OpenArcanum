using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Arcanum.Runtime.UI
{
    /// <summary>Small presentation-only bridge for source mouse-inside/outside hint routing.</summary>
    public sealed class SourceHoverHintTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action _entered;
        private Action _exited;

        public void Bind(Action entered, Action exited)
        {
            _entered = entered;
            _exited = exited;
        }

        public void OnPointerEnter(PointerEventData eventData) => _entered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => _exited?.Invoke();
        private void OnDisable() => _exited?.Invoke();
    }
}
