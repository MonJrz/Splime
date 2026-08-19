using UnityEngine;

namespace Splime.Puzzles
{
    public class SwitchVisualFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _lever;

        [Header("Locked Rotation")]
        [SerializeField] private Vector3 _lockedRotationOffset =
            new Vector3(0f, 0f, 50f);

        [Header("Animation")]
        [Min(1f)]
        [SerializeField] private float _rotationSpeed = 180f;

        private Quaternion _unlockedRotation;
        private Quaternion _lockedRotation;
        private Quaternion _targetRotation;

        private void Awake()
        {
            if (_lever == null)
                return;

            _unlockedRotation = _lever.localRotation;

            _lockedRotation =
                _unlockedRotation *
                Quaternion.Euler(_lockedRotationOffset);

            _targetRotation = _unlockedRotation;
        }

        private void Update()
        {
            if (_lever == null)
                return;

            _lever.localRotation =
                Quaternion.RotateTowards(
                    _lever.localRotation,
                    _targetRotation,
                    _rotationSpeed * Time.deltaTime);
        }

        public void SetLocked()
        {
            _targetRotation = _lockedRotation;
        }

        public void SetUnlocked()
        {
            _targetRotation = _unlockedRotation;
        }
    }
}