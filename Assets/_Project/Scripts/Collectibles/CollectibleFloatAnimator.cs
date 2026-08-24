using UnityEngine;

namespace Splime.Collectibles
{
    public class CollectibleFloatAnimator : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private float _rotationSpeed = 60f;

        [Header("Floating")]
        [SerializeField] private float _floatAmplitude = 0.2f;
        [SerializeField] private float _floatFrequency = 2f;

        private Vector3 _startLocalPosition;
        private float _timeOffset;

        private void Awake()
        {
            _startLocalPosition = transform.localPosition;

            // Evita que todos los collectibles oscilen perfectamente sincronizados.
            _timeOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            // Giro continuo sobre Y.
            transform.Rotate(
                Vector3.up,
                _rotationSpeed * Time.deltaTime,
                Space.Self);

            // Oscilación vertical suave.
            float offsetY =
                Mathf.Sin(
                    Time.time * _floatFrequency +
                    _timeOffset)
                * _floatAmplitude;

            Vector3 position = _startLocalPosition;
            position.y += offsetY;

            transform.localPosition = position;
        }
    }
}