using Unity.Netcode;
using UnityEngine;
using Splime.Core;

namespace Splime.Player
{
    /// <summary>
    /// Maneja el movimiento 3D isométrico/top-down del Slime utilizando CharacterController.
    /// Sincronizado en red mediante NetworkTransform de Netcode for GameObjects.
    /// Unifica el movimiento horizontal y vertical en una sola llamada a CharacterController.Move().
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(SlimeInput))]
    public class SlimeMovement : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private SlimeData _slimeData;

        [Header("Movement Tweaks")]
        [SerializeField] private float _acceleration = 15.0f;
        [SerializeField] private float _deceleration = 20.0f;

        // Components
        private CharacterController _characterController;
        private SlimeInput _slimeInput;
        private SlimeJump _slimeJump;
        private SlimeStatsModifier _statsModifier;
        private Transform _mainCameraTransform;

        // Movement State
        private Vector3 _currentVelocity;
        private Vector3 _targetDirection;

        public Vector3 CurrentVelocity => _currentVelocity;
        public bool IsMoving => _currentVelocity.magnitude > 0.1f;
        public bool ShouldProcessInput => !IsSpawned || IsOwner;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _slimeInput = GetComponent<SlimeInput>();
            _slimeJump = GetComponent<SlimeJump>();
            _statsModifier = GetComponent<SlimeStatsModifier>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (Camera.main != null)
            {
                _mainCameraTransform = Camera.main.transform;
            }

            if (!IsOwner)
            {
                enabled = false;
            }
        }

        public void InitializeData(SlimeData data)
        {
            _slimeData = data;
        }

        private void Update()
        {
            if (!ShouldProcessInput) return;

            CalculateMovementDirection();
            ApplyMovement();
            ApplyRotation();
        }

        private void CalculateMovementDirection()
        {
            Vector2 input = _slimeInput != null ? _slimeInput.MoveInput : Vector2.zero;

            if (_mainCameraTransform == null && Camera.main != null)
            {
                _mainCameraTransform = Camera.main.transform;
            }

            if (_mainCameraTransform != null)
            {
                Vector3 camForward = _mainCameraTransform.forward;
                Vector3 camRight = _mainCameraTransform.right;

                camForward.y = 0f;
                camRight.y = 0f;

                camForward.Normalize();
                camRight.Normalize();

                _targetDirection = (camForward * input.y + camRight * input.x).normalized;
            }
            else
            {
                _targetDirection = new Vector3(input.x, 0f, input.y).normalized;
            }
        }

        private void ApplyMovement()
        {
            // Leer velocidad de SlimeStatsModifier (tiene los multiplicadores de habilidades aplicados)
            // Si no existe el modifier, usar SlimeData directamente como fallback
            float baseMoveSpeed = _statsModifier != null ? _statsModifier.MoveSpeed
                                : (_slimeData != null ? _slimeData.MoveSpeed : 6.0f);

            float targetSpeed = _targetDirection.magnitude * baseMoveSpeed;
            Vector3 desiredVelocity = _targetDirection * targetSpeed;

            float accelRate = (_targetDirection.magnitude > 0.01f) ? _acceleration : _deceleration;

            _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, accelRate * Time.deltaTime);

            float verticalVel = _slimeJump != null ? _slimeJump.VerticalVelocity : 0f;

            Vector3 totalVelocity = _currentVelocity + new Vector3(0f, verticalVel, 0f);
            _characterController.Move(totalVelocity * Time.deltaTime);
        }

        private void ApplyRotation()
        {
            if (_targetDirection.sqrMagnitude > 0.001f)
            {
                float rotSpeed = _statsModifier != null ? _statsModifier.RotationSpeed
                               : (_slimeData != null ? _slimeData.RotationSpeed : 12.0f);
                Quaternion targetRotation = Quaternion.LookRotation(_targetDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotSpeed * Time.deltaTime);
            }
        }
    }
}
