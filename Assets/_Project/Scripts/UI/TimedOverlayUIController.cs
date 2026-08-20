using System;
using System.Collections;
using UnityEngine;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class TimedOverlayUIController : MonoBehaviour
    {
        private const float MaxDisplayDuration = 5f;

        [Header("Overlay")]
        [SerializeField] private GameObject _panel;
        [SerializeField, Range(0.1f, MaxDisplayDuration)] private float _displayDuration = 3f;
        [SerializeField] private bool _showOnEnable = true;

        [Header("Intro Sequence")]
        [SerializeField] private RectTransform _graphics;
        [SerializeField] private RectTransform _circlesImage;
        [SerializeField] private RectTransform _levelText;
        [SerializeField] private RectTransform _titleText;
        [SerializeField, Range(0.1f, 1.5f)] private float _elementAnimationDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _graphicsStartScale = 0.1f;
        [SerializeField, Min(0f)] private float _slideDistance = 1200f;

        [Header("Exit Sequence")]
        [SerializeField, Range(0.1f, 1f)] private float _circleRotationDuration = 0.35f;
        [SerializeField, Range(0f, 720f)] private float _circleRotationDegrees = 360f;
        [SerializeField, Range(0.1f, 1f)] private float _exitAnimationDuration = 0.5f;

        private Coroutine _sequenceRoutine;
        private Vector3 _graphicsFinalScale;
        private Vector2 _graphicsFinalPosition;
        private Quaternion _circlesFinalRotation;
        private Vector2 _levelTextFinalPosition;
        private Vector2 _titleTextFinalPosition;
        private bool _finalPoseCached;
        private bool _completionRaised;

        public event Action Completed;

        public bool IsVisible => _panel != null && _panel.activeSelf;
        public bool WillShowOnEnable => _showOnEnable && _panel != null;

        private void Awake()
        {
            CacheFinalPose();
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
            StopSequence();
            RestoreFinalPose();
            SetVisible(false);
        }

        public void Show()
        {
            if (_panel == null)
            {
                Debug.LogWarning($"[{nameof(TimedOverlayUIController)}] Panel reference is missing.", this);
                return;
            }

            StopSequence();
            CacheFinalPose();
            _completionRaised = false;

            SetVisible(false);
            SetVisible(true);
            PrepareInitialPose();

            _sequenceRoutine = StartCoroutine(PlaySequence());
        }

        public void Hide()
        {
            StopSequence();
            CompleteSequence();
        }

        private IEnumerator PlaySequence()
        {
            float totalDuration = Mathf.Clamp(_displayDuration, 0.1f, MaxDisplayDuration);
            float rotationDuration = Mathf.Min(_circleRotationDuration, totalDuration / 5f);
            float exitDuration = Mathf.Min(_exitAnimationDuration, totalDuration / 5f);
            float entranceBudget = Mathf.Max(0f, totalDuration - rotationDuration - exitDuration);
            float phaseDuration = Mathf.Min(_elementAnimationDuration, entranceBudget / 3f);
            float sequenceStartTime = Time.unscaledTime;

            yield return AnimateGraphics(phaseDuration);
            yield return AnimatePosition(_levelText, _levelTextFinalPosition, phaseDuration);
            yield return AnimatePosition(_titleText, _titleTextFinalPosition, phaseDuration);

            float holdDuration = totalDuration -
                                 (Time.unscaledTime - sequenceStartTime) -
                                 rotationDuration -
                                 exitDuration;
            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return AnimateCircleRotation(rotationDuration);
            yield return AnimateExit(exitDuration);

            _sequenceRoutine = null;
            CompleteSequence();
        }

        private IEnumerator AnimateGraphics(float duration)
        {
            if (_graphics == null)
            {
                yield break;
            }

            Vector3 startScale = _graphicsFinalScale * _graphicsStartScale;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = duration > 0f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
                _graphics.localScale = Vector3.LerpUnclamped(
                    startScale,
                    _graphicsFinalScale,
                    EaseOutCubic(progress));
                yield return null;
            }

            _graphics.localScale = _graphicsFinalScale;
        }

        private static IEnumerator AnimatePosition(
            RectTransform element,
            Vector2 finalPosition,
            float duration)
        {
            if (element == null)
            {
                yield break;
            }

            Vector2 startPosition = element.anchoredPosition;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = duration > 0f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
                element.anchoredPosition = Vector2.LerpUnclamped(
                    startPosition,
                    finalPosition,
                    EaseOutCubic(progress));
                yield return null;
            }

            element.anchoredPosition = finalPosition;
        }

        private IEnumerator AnimateCircleRotation(float duration)
        {
            if (_circlesImage == null || _circleRotationDegrees <= 0f)
            {
                yield break;
            }

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = duration > 0f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
                float angle = -_circleRotationDegrees * EaseInOutCubic(progress);
                _circlesImage.localRotation =
                    _circlesFinalRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            _circlesImage.localRotation =
                _circlesFinalRotation * Quaternion.Euler(0f, 0f, -_circleRotationDegrees);
        }

        private IEnumerator AnimateExit(float duration)
        {
            Vector2 graphicsTarget = _graphicsFinalPosition + Vector2.left * _slideDistance;
            Vector2 levelTextTarget = _levelTextFinalPosition + Vector2.right * _slideDistance;
            Vector2 titleTextTarget = _titleTextFinalPosition + Vector2.right * _slideDistance;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = duration > 0f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
                float easedProgress = EaseInCubic(progress);

                SetExitPositions(
                    Vector2.LerpUnclamped(_graphicsFinalPosition, graphicsTarget, easedProgress),
                    Vector2.LerpUnclamped(_levelTextFinalPosition, levelTextTarget, easedProgress),
                    Vector2.LerpUnclamped(_titleTextFinalPosition, titleTextTarget, easedProgress));
                yield return null;
            }

            SetExitPositions(graphicsTarget, levelTextTarget, titleTextTarget);
        }

        private void SetExitPositions(
            Vector2 graphicsPosition,
            Vector2 levelTextPosition,
            Vector2 titleTextPosition)
        {
            if (_graphics != null)
            {
                _graphics.anchoredPosition = graphicsPosition;
            }

            if (_levelText != null)
            {
                _levelText.anchoredPosition = levelTextPosition;
            }

            if (_titleText != null)
            {
                _titleText.anchoredPosition = titleTextPosition;
            }
        }

        private void CacheFinalPose()
        {
            if (_finalPoseCached)
            {
                return;
            }

            if (_graphics != null)
            {
                _graphicsFinalScale = _graphics.localScale;
                _graphicsFinalPosition = _graphics.anchoredPosition;
            }

            if (_circlesImage != null)
            {
                _circlesFinalRotation = _circlesImage.localRotation;
            }

            if (_levelText != null)
            {
                _levelTextFinalPosition = _levelText.anchoredPosition;
            }

            if (_titleText != null)
            {
                _titleTextFinalPosition = _titleText.anchoredPosition;
            }

            _finalPoseCached = true;
        }

        private void PrepareInitialPose()
        {
            if (_graphics != null)
            {
                _graphics.localScale = _graphicsFinalScale * _graphicsStartScale;
                _graphics.anchoredPosition = _graphicsFinalPosition;
            }

            if (_circlesImage != null)
            {
                _circlesImage.localRotation = _circlesFinalRotation;
            }

            if (_levelText != null)
            {
                _levelText.anchoredPosition =
                    _levelTextFinalPosition + Vector2.left * _slideDistance;
            }

            if (_titleText != null)
            {
                _titleText.anchoredPosition =
                    _titleTextFinalPosition + Vector2.right * _slideDistance;
            }
        }

        private void RestoreFinalPose()
        {
            if (!_finalPoseCached)
            {
                return;
            }

            if (_graphics != null)
            {
                _graphics.localScale = _graphicsFinalScale;
                _graphics.anchoredPosition = _graphicsFinalPosition;
            }

            if (_circlesImage != null)
            {
                _circlesImage.localRotation = _circlesFinalRotation;
            }

            if (_levelText != null)
            {
                _levelText.anchoredPosition = _levelTextFinalPosition;
            }

            if (_titleText != null)
            {
                _titleText.anchoredPosition = _titleTextFinalPosition;
            }
        }

        private void StopSequence()
        {
            if (_sequenceRoutine == null)
            {
                return;
            }

            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        private void CompleteSequence()
        {
            RestoreFinalPose();
            SetVisible(false);

            if (_completionRaised)
            {
                return;
            }

            _completionRaised = true;
            Completed?.Invoke();
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float value)
        {
            float clampedValue = Mathf.Clamp01(value);
            return clampedValue * clampedValue * clampedValue;
        }

        private static float EaseInOutCubic(float value)
        {
            float clampedValue = Mathf.Clamp01(value);
            return clampedValue < 0.5f
                ? 4f * clampedValue * clampedValue * clampedValue
                : 1f - Mathf.Pow(-2f * clampedValue + 2f, 3f) / 2f;
        }

        private void SetVisible(bool isVisible)
        {
            if (_panel != null && _panel.activeSelf != isVisible)
            {
                _panel.SetActive(isVisible);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _displayDuration = Mathf.Clamp(_displayDuration, 0.1f, MaxDisplayDuration);
            _elementAnimationDuration = Mathf.Clamp(_elementAnimationDuration, 0.1f, 1.5f);
            _graphicsStartScale = Mathf.Clamp01(_graphicsStartScale);
            _slideDistance = Mathf.Max(0f, _slideDistance);
            _circleRotationDuration = Mathf.Clamp(_circleRotationDuration, 0.1f, 1f);
            _circleRotationDegrees = Mathf.Clamp(_circleRotationDegrees, 0f, 720f);
            _exitAnimationDuration = Mathf.Clamp(_exitAnimationDuration, 0.1f, 1f);
        }
#endif
    }
}
