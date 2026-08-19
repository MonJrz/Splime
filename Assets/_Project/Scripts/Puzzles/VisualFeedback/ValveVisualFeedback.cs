using UnityEngine;

namespace Splime.Puzzles
{
    public class ValveVisualFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _handle;

        [Header("Open Rotation")]
        [SerializeField] private Vector3 _openRotationOffset =
            new Vector3(0f, 90f, 0f);

        [Header("Animation")]
        [Min(1f)]
        [SerializeField] private float _rotationSpeed = 180f;

        private Quaternion _closedRotation;
        private Quaternion _openRotation;
        private Quaternion _targetRotation;

        private void Awake()
        {
            if (_handle == null)
                return;

            _closedRotation = _handle.localRotation;

            _openRotation =
                _closedRotation *
                Quaternion.Euler(_openRotationOffset);

            _targetRotation = _closedRotation;
        }

        private void Update()
        {
            if (_handle == null)
                return;

            _handle.localRotation =
                Quaternion.RotateTowards(
                    _handle.localRotation,
                    _targetRotation,
                    _rotationSpeed * Time.deltaTime);
        }

        public void Open()
        {
            _targetRotation = _openRotation;
        }

        public void Close()
        {
            _targetRotation = _closedRotation;
        }
    }
}