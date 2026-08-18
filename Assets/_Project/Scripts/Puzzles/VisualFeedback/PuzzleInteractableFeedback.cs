using UnityEngine;

namespace Splime.Puzzles
{
    public class PuzzleInteractableFeedback : MonoBehaviour
    {
        private static readonly int SaturationProperty = Shader.PropertyToID("_Saturation");

        [Header("References")]
        [SerializeField] private GameObject _arrow;
        [SerializeField] private Light _spotLight;

        [Header("Desaturation")]
        [Tooltip("Visual renderers that will become desaturated when the mechanism is blocked.")]
        [SerializeField] private Renderer[] _desaturationRenderers;

        [Range(0f, 1f)]
        [SerializeField] private float _normalSaturation = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float _lockedSaturation = 0f;

        [Header("Saturation Transition")]
        [Min(0.01f)]
        [SerializeField] private float _saturationTransitionSpeed = 3f;

        private float _currentSaturation;
        private float _targetSaturation;

        [Header("Light Settings")]
        [Min(0f)]
        [SerializeField] private float _normalLightIntensity = 100f; // 3f;

        [Min(0f)]
        [SerializeField] private float _lockedLightIntensity = 15f; // 0.5f;

        private MaterialPropertyBlock _propertyBlock;

        private bool _isLocked;
        private bool _isUsed;

        public bool IsLocked => _isLocked;
        public bool IsUsed => _isUsed;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            
            _currentSaturation = _normalSaturation;
            _targetSaturation = _normalSaturation;

            ApplyFeedback();
            ApplySaturationValue(_currentSaturation);
        }

        private void Update()
        {
            if (Mathf.Approximately(
                _currentSaturation,
                _targetSaturation))
            {
                return;
            }

            _currentSaturation = Mathf.MoveTowards(
                _currentSaturation,
                _targetSaturation,
                _saturationTransitionSpeed * Time.deltaTime);

            ApplySaturationValue(_currentSaturation);
        }

        public void SetLocked(bool locked)
        {
            _isLocked = locked;
            ApplyFeedback();
        }

        public void SetUsed(bool used)
        {
            _isUsed = used;
            ApplyFeedback();
        }

        public void MarkUsed()
        {
            SetUsed(true);
        }

        public void ResetUsed()
        {
            SetUsed(false);
        }

        private void ApplyFeedback()
        {
            ApplyArrowFeedback();
            ApplyLightFeedback();
            UpdateSaturationTarget();
        }

        private void ApplyArrowFeedback()
        {
            if (_arrow == null)
                return;

            _arrow.SetActive(!_isLocked && !_isUsed);
        }

        private void ApplyLightFeedback()
        {
            if (_spotLight == null)
                return;

            if (_isUsed)
            {
                _spotLight.intensity = 0f;
            }
            else if (_isLocked)
            {
                _spotLight.intensity = _lockedLightIntensity;
            }
            else
            {
                _spotLight.intensity = _normalLightIntensity;
            }
        }

        private void UpdateSaturationTarget()
        {
            _targetSaturation =
                _isLocked
                    ? _lockedSaturation
                    : _normalSaturation;
        }

        private void ApplySaturationValue(float saturation)
        {
            if (_desaturationRenderers == null)
                return;

            foreach (Renderer targetRenderer in _desaturationRenderers)
            {
                if (targetRenderer == null)
                    continue;

                _propertyBlock.Clear();

                // Conserva cualquier otra propiedad que ya tenga
                // este Renderer mediante MaterialPropertyBlock.
                targetRenderer.GetPropertyBlock(_propertyBlock);

                _propertyBlock.SetFloat(
                    SaturationProperty,
                    saturation);

                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}