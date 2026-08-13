using System.Collections;
using UnityEngine;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class TimedOverlayUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField, Min(0f)] private float _displayDuration = 3f;
        [SerializeField] private bool _showOnEnable = true;

        private Coroutine _hideRoutine;

        public bool IsVisible => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_showOnEnable)
            {
                Show();
            }
        }

        private void OnDisable()
        {
            StopHideRoutine();
            SetVisible(false);
        }

        public void Show()
        {
            if (_panel == null)
            {
                Debug.LogWarning($"[{nameof(TimedOverlayUIController)}] Panel reference is missing.", this);
                return;
            }

            StopHideRoutine();

            // Reactivating the panel also restarts Animator states configured to play on enable.
            SetVisible(false);
            SetVisible(true);

            if (_displayDuration > 0f)
            {
                _hideRoutine = StartCoroutine(HideAfterDelay());
            }
        }

        public void Hide()
        {
            StopHideRoutine();
            SetVisible(false);
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_displayDuration);
            _hideRoutine = null;
            SetVisible(false);
        }

        private void StopHideRoutine()
        {
            if (_hideRoutine == null)
            {
                return;
            }

            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        private void SetVisible(bool isVisible)
        {
            if (_panel != null && _panel.activeSelf != isVisible)
            {
                _panel.SetActive(isVisible);
            }
        }
    }
}
