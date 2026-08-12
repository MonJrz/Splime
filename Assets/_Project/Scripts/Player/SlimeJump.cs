using Unity.Netcode;
using UnityEngine;
using Splime.Core;

namespace Splime.Player
{
    /// <summary>
    /// Maneja el salto y la gravedad del Slime de forma autoritativa local (IsOwner)
    /// utilizando el CharacterController y aplicando los parámetros de SlimeData.
    /// Incluye detección de suelo robusta (Raycast/SphereCast + Coyote Time) para
    /// evitar fallos de salto durante el movimiento en superficies.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(SlimeInput))]
    public class SlimeJump : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private SlimeData _slimeData;

        [Header("Ground Check Settings")]
        [SerializeField] private float _coyoteTime = 0.15f;

        // Components
        private CharacterController _characterController;
        private SlimeInput _slimeInput;

        // Jump & Gravity State
        private float _verticalVelocity;
        private bool _isGrounded;
        private float _coyoteTimer;

        // Public Properties
        public bool IsGrounded => _isGrounded || _coyoteTimer > 0f;
        public float VerticalVelocity => _verticalVelocity;
        public bool ShouldProcessInput => !IsSpawned || IsOwner;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _slimeInput = GetComponent<SlimeInput>();
        }

        private void OnEnable()
        {
            if (_slimeInput == null) _slimeInput = GetComponent<SlimeInput>();
            if (_slimeInput != null)
            {
                _slimeInput.OnJumpPressed -= HandleJump;
                _slimeInput.OnJumpPressed += HandleJump;
            }
        }

        private void OnDisable()
        {
            if (_slimeInput != null)
            {
                _slimeInput.OnJumpPressed -= HandleJump;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

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

            CheckGrounded();
            ApplyGravity();
        }

        private void CheckGrounded()
        {
            bool ccGrounded = _characterController.isGrounded;

            Vector3 rayStart = transform.position + _characterController.center;
            float rayDistance = (_characterController.height / 2.0f) + 0.15f;
            bool rayGrounded = Physics.Raycast(rayStart, Vector3.down, rayDistance);

            _isGrounded = ccGrounded || rayGrounded;

            if (_isGrounded)
            {
                _coyoteTimer = _coyoteTime;
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2.0f;
                }
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
            }
        }

        private void HandleJump()
        {
            if (!ShouldProcessInput) return;

            if (_coyoteTimer > 0f)
            {
                float jumpForce = _slimeData != null ? _slimeData.JumpForce : 8.0f;
                _verticalVelocity = jumpForce;
                _coyoteTimer = 0f;
            }
        }

        private void ApplyGravity()
        {
            float gravity = _slimeData != null ? _slimeData.Gravity : -20.0f;
            _verticalVelocity += gravity * Time.deltaTime;
        }
    }
}
