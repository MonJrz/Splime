using UnityEngine;

namespace Splime.Puzzles
{
    public class WeightPlatformVisualFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _pressurePlate;

        [Header("Pressed State")]
        [SerializeField] private Vector3 _pressedOffset =
            new Vector3(0f, -0.05f, 0f);

        [Header("Animation")]
        [Min(0.01f)]
        [SerializeField] private float _moveSpeed = 0.4f;

        private Vector3 _releasedPosition;
        private Vector3 _pressedPosition;
        private Vector3 _targetPosition;

        private void Awake()
        {
            if (_pressurePlate == null)
                return;

            _releasedPosition = _pressurePlate.localPosition;
            _pressedPosition =
                _releasedPosition + _pressedOffset;

            _targetPosition = _releasedPosition;
        }

        private void Update()
        {
            if (_pressurePlate == null)
                return;

            _pressurePlate.localPosition =
                Vector3.MoveTowards(
                    _pressurePlate.localPosition,
                    _targetPosition,
                    _moveSpeed * Time.deltaTime);
        }

        public void Press()
        {
            _targetPosition = _pressedPosition;
        }

        public void Release()
        {
            _targetPosition = _releasedPosition;
        }
    }
}