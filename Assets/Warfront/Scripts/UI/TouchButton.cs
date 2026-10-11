using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warfront.UI
{
    /// <summary>
    /// Touch button that reports both "held" and "pressed this frame".
    ///
    /// Unity's Button only fires on release, which is unusable for fire/aim/sprint, so the mobile
    /// controls use this instead and map the states onto the shared InputService flags.
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Image _background;
        private Text _label;
        private Color _normalColor;
        private Color _pressedColor;
        private bool _pressedThisFrame;

        public bool Held { get; private set; }
        public bool Interactable { get; set; }
        public System.Action Pressed;
        public System.Action Released;

        public void Configure(Image background, Text label, Color normalColor)
        {
            _background = background;
            _label = label;
            _normalColor = normalColor;
            _pressedColor = new Color(
                Mathf.Clamp01(normalColor.r + 0.18f),
                Mathf.Clamp01(normalColor.g + 0.18f),
                Mathf.Clamp01(normalColor.b + 0.18f),
                normalColor.a);
            Interactable = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            Held = true;
            _pressedThisFrame = true;
            if (_background != null) _background.color = _pressedColor;
            if (Pressed != null) Pressed();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Sliding a thumb off the button is a release, matching real touchscreen expectations.
            Release();
        }

        public void Release()
        {
            if (!Held) return;
            Held = false;
            if (_background != null) _background.color = _normalColor;
            if (Released != null) Released();
        }

        /// <summary>Returns true once per press (rising edge).</summary>
        public bool ConsumePress()
        {
            if (!_pressedThisFrame) return false;
            _pressedThisFrame = false;
            return true;
        }

        public void SetLabel(string text)
        {
            if (_label != null) _label.text = text;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject != null && gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
