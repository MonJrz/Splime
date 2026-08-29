using System.Collections;
using UnityEngine;

namespace Splime.Levels
{
    [DisallowMultipleComponent]
    public sealed class CheckpointBeaconVisual : MonoBehaviour
    {
        [Header("Beacon")]
        [SerializeField] private Renderer _beamRenderer;

        [Tooltip("Reference de la propiedad Beacon Color en Shader Graph.")]
        [SerializeField] private string _colorProperty = "_BeaconColor";

        [Header("Transition")]
        [SerializeField, Min(0f)] private float _transitionDuration = 0.4f;

        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _transitionRoutine;

        private Color _currentColor = Color.white;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            if (_beamRenderer != null &&
                _beamRenderer.sharedMaterial != null &&
                _beamRenderer.sharedMaterial.HasProperty(_colorProperty))
            {
                _currentColor =
                    _beamRenderer.sharedMaterial.GetColor(_colorProperty);
            }
        }

        public void SetColor(Color targetColor)
        {
            if (_beamRenderer == null)
            {
                return;
            }

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
            }

            if (_transitionDuration <= 0f)
            {
                ApplyColor(targetColor);
                return;
            }

            _transitionRoutine =
                StartCoroutine(
                    TransitionColorRoutine(
                        _currentColor,
                        targetColor));
        }

        private IEnumerator TransitionColorRoutine(
            Color startColor,
            Color targetColor)
        {
            float elapsed = 0f;

            while (elapsed < _transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(
                    elapsed / _transitionDuration);

                // Transición un poco más agradable que un Lerp lineal puro.
                t = t * t * (3f - 2f * t);

                ApplyColor(
                    Color.Lerp(
                        startColor,
                        targetColor,
                        t));

                yield return null;
            }

            ApplyColor(targetColor);

            _transitionRoutine = null;
        }

        private void ApplyColor(Color color)
        {
            _propertyBlock ??= new MaterialPropertyBlock();

            _beamRenderer.GetPropertyBlock(
                _propertyBlock);

            Material sharedMaterial =
                _beamRenderer.sharedMaterial;

            if (sharedMaterial == null ||
                !sharedMaterial.HasProperty(_colorProperty))
            {
                return;
            }

            _propertyBlock.SetColor(
                _colorProperty,
                color);

            _beamRenderer.SetPropertyBlock(
                _propertyBlock);

            _currentColor = color;
        }
    }
}