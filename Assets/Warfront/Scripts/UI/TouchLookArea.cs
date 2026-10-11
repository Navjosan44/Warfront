using UnityEngine;
using UnityEngine.EventSystems;

namespace Warfront.UI
{
    /// <summary>
    /// Camera drag surface.
    ///
    /// Accumulates pointer movement in screen pixels (the camera applies sensitivity and smoothing).
    /// A quick double tap in the area raises <see cref="DoubleTapped"/> so the player can snap to aim
    /// without hunting for the aim button.
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float DoubleTapWindow = 0.28f;

        private Vector2 _lastPosition;
        private bool _tracking;
        private Vector2 _pendingDelta;
        private float _lastTapTime = -10f;
        private float _pixelsPerUnitScale = 0.06f;

        public bool DoubleTapped { get; private set; }

        public void Configure(float scale)
        {
            _pixelsPerUnitScale = Mathf.Max(0.005f, scale);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _tracking = true;
            _lastPosition = eventData.position;

            if (Time.unscaledTime - _lastTapTime <= DoubleTapWindow)
            {
                DoubleTapped = true;
                _lastTapTime = -10f;
            }
            else
            {
                _lastTapTime = Time.unscaledTime;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_tracking)
            {
                _tracking = true;
                _lastPosition = eventData.position;
                return;
            }
            Vector2 delta = eventData.position - _lastPosition;
            _lastPosition = eventData.position;
            _pendingDelta += delta * _pixelsPerUnitScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _tracking = false;
        }

        /// <summary>Returns the accumulated look delta and clears it.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 delta = _pendingDelta;
            _pendingDelta = Vector2.zero;
            return delta;
        }

        public bool ConsumeDoubleTap()
        {
            bool value = DoubleTapped;
            DoubleTapped = false;
            return value;
        }

        public void Reset()
        {
            _tracking = false;
            _pendingDelta = Vector2.zero;
            DoubleTapped = false;
        }
    }
}
