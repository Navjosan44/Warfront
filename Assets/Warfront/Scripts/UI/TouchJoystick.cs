using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warfront.UI
{
    /// <summary>
    /// Dynamic on-screen thumbstick.
    ///
    /// The whole left half of the screen is the capture area: the stick materialises under the thumb
    /// where the player actually pressed and recentres when released, which is how mobile shooters
    /// have to feel (a fixed stick is unusable on a phone held in two hands).
    /// </summary>
    public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _area;
        private RectTransform _background;
        private RectTransform _handle;
        private float _radius = 120f;
        private Vector2 _origin;
        private int _pointerId = -1;

        public Vector2 Value { get; private set; }
        public bool IsHeld { get { return _pointerId >= 0; } }
        public float Magnitude { get { return Value.magnitude; } }

        public void Configure(RectTransform area, RectTransform background, RectTransform handle, float radius)
        {
            _area = area;
            _background = background;
            _handle = handle;
            _radius = Mathf.Max(40f, radius);
            if (_background != null) _background.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId >= 0) return;
            _pointerId = eventData.pointerId;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, eventData.pressEventCamera, out local))
            {
                local = Vector2.zero;
            }
            _origin = local;
            Value = Vector2.zero;

            if (_background != null)
            {
                _background.gameObject.SetActive(true);
                _background.anchoredPosition = _origin;
            }
            if (_handle != null) _handle.anchoredPosition = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            Vector2 delta = local - _origin;
            Vector2 clamped = Vector2.ClampMagnitude(delta, _radius);
            Value = clamped / _radius;
            if (_handle != null) _handle.anchoredPosition = clamped;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        /// <summary>Forces the stick back to centre - used when the controls are hidden or paused.</summary>
        public void Release()
        {
            _pointerId = -1;
            Value = Vector2.zero;
            if (_background != null) _background.gameObject.SetActive(false);
            if (_handle != null) _handle.anchoredPosition = Vector2.zero;
        }
    }
}
