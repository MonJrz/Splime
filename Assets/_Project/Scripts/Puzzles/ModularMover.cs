using UnityEngine;

namespace Splime.Puzzles
{
    public class ModularMover : MonoBehaviour
    {
        [Header("Modular Movement")]
        [Tooltip("Direction of movement. Example: (-1,0,0) = left.")]
        [SerializeField] private Vector3 _moveDirection = Vector3.left;

        [Tooltip("Size of a level module.")]
        [Min(0.01f)]
        [SerializeField] private float _moduleSize = 1f;

        [Tooltip("The number of modules the object will traverse.")]
        [Min(1)]
        [SerializeField] private int _modules = 1;

        [Header("Movement")]
        [Tooltip("Speed of movement in units per second.")]
        [Min(0.01f)]
        [SerializeField] private float _moveSpeed = 2f;

        [Tooltip("Distance at which we consider the object has reached its destination.")]
        [SerializeField] private float _arrivalThreshold = 0.001f;

        private Vector3 _startPosition;
        private Vector3 _activePosition;
        private Vector3 _targetPosition;

        private bool _isActive;
        private bool _isMoving;
        private bool _isMovementLocked;

        public bool IsActive => _isActive;
        public bool IsMoving => _isMoving;
        public bool IsMovementLocked => _isMovementLocked;

        public Vector3 StartPosition => _startPosition;
        public Vector3 ActivePosition => _activePosition;

        public Vector3 FrameDelta { get; private set; }

        private void Awake()
        {
            CachePositions();
        }

        private void Update()
        {
            FrameDelta = Vector3.zero;
            MoveTowardsTarget();
        }

        private void CachePositions()
        {
            _startPosition = transform.position;

            Vector3 direction = _moveDirection.sqrMagnitude > 0f
                ? _moveDirection.normalized
                : Vector3.zero;

            _activePosition =
                _startPosition +
                direction * (_moduleSize * _modules);

            _targetPosition = _startPosition;
        }

        public void LockMovement()
        {
            _isMovementLocked = true;
        }

        public void UnlockMovement()
        {
            _isMovementLocked = false;
        }

        private void MoveTowardsTarget()
        {
            if (!_isMoving || _isMovementLocked) return;

            Vector3 previousPosition = transform.position;

            transform.position = Vector3.MoveTowards(
                transform.position,
                _targetPosition,
                _moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(
                    transform.position,
                    _targetPosition) <= _arrivalThreshold)
            {
                transform.position = _targetPosition;
                _isMoving = false;
            }

            // Include any minor final adjustments.
            FrameDelta = transform.position - previousPosition;
        }

        /// <summary>
        /// Moves the object from its initial position
        /// to the active position.
        /// </summary>
        public void MoveToActive()
        {
            _isActive = true;
            _targetPosition = _activePosition;
            _isMoving = true;
        }

        /// <summary>
        /// Returns the object to its initial position.
        /// </summary>
        public void ReturnToStart()
        {
            _isActive = false;
            _targetPosition = _startPosition;
            _isMoving = true;
        }

        /// <summary>
        /// Toggles between the initial and active positions.
        /// Useful for Switch.
        /// </summary>
        public void TogglePosition()
        {
            if (_isActive)
                ReturnToStart();
            else
                MoveToActive();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 start =
                Application.isPlaying
                    ? _startPosition
                    : transform.position;

            Vector3 direction = _moveDirection.sqrMagnitude > 0f
                ? _moveDirection.normalized
                : Vector3.zero;

            Vector3 end =
                start +
                direction * (_moduleSize * _modules);

            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.15f);
        }
    }
}