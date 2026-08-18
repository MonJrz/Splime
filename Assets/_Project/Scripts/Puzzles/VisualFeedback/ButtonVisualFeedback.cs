using UnityEngine;

namespace Splime.Puzzles
{
    public class ButtonVisualFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _buttonCap;

        [Header("Pressed State")]
        [Tooltip("Desplazamiento local al quedar presionado.")]
        [SerializeField] private Vector3 _pressedOffset =
            new Vector3(0f, -0.08f, 0f);

        [Header("Animation")]
        [Min(0.01f)]
        [SerializeField] private float _moveSpeed = 0.5f;

        private Vector3 _releasedPosition;
        private Vector3 _pressedPosition;
        private Vector3 _targetPosition;

        private bool _isPressed;

        public bool IsPressed => _isPressed;

        private void Awake()
        {
            if (_buttonCap == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ButtonVisualFeedback)}] " +
                    $"Button Cap no asignado en {gameObject.name}.",
                    this);

                return;
            }

            _releasedPosition = _buttonCap.localPosition;
            _pressedPosition =
                _releasedPosition + _pressedOffset;

            _targetPosition = _releasedPosition;
        }

        private void Update()
        {
            if (_buttonCap == null)
                return;

            _buttonCap.localPosition =
                Vector3.MoveTowards(
                    _buttonCap.localPosition,
                    _targetPosition,
                    _moveSpeed * Time.deltaTime);
        }

        public void Press()
        {
            _isPressed = true;
            _targetPosition = _pressedPosition;
        }

        public void Release()
        {
            _isPressed = false;
            _targetPosition = _releasedPosition;
        }

        public void Toggle()
        {
            if (_isPressed)
                Release();
            else
                Press();
        }
    }
}