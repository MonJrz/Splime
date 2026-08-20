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
        private SlimeStatsModifier _statsModifier;

        // Jump & Gravity State
        private float _verticalVelocity;
        private bool _isGrounded;
        private float _coyoteTimer;
        private int _jumpsUsed;

        // Public Properties
        public bool IsGrounded => _isGrounded || _coyoteTimer > 0f;
        public bool IsGroundedRaw => _isGrounded;
        public float VerticalVelocity => _verticalVelocity;
        public bool ShouldProcessInput => !IsSpawned || IsOwner;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _slimeInput = GetComponent<SlimeInput>();
            _statsModifier = GetComponent<SlimeStatsModifier>();
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

        private void Start()
        {
            if (_statsModifier != null &&
                !_statsModifier.IsInitialized &&
                _slimeData != null)
            {
                _statsModifier.Initialize(_slimeData);
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

            if (_statsModifier != null &&
                !_statsModifier.IsInitialized &&
                data != null)
            {
                _statsModifier.Initialize(data);
            }
        }

        private void Update()
        {
            if (!ShouldProcessInput) return;

            CheckGrounded();
            ApplyGravity();
        }

        private void CheckGrounded()
        {
            // LayerMask: sólo detectar capas de suelo (configuradas en SlimeData).
            // Esto excluye los CharacterControllers de otros Slimes (capa "Player"),
            // evitando que el Slime Ágil encogido pueda saltar infinitamente sobre el otro Slime.
            LayerMask groundMask = _slimeData != null ? _slimeData.GroundLayer : Physics.DefaultRaycastLayers;

            // CharacterController.isGrounded no acepta LayerMask, así que
            // filtramos adicionalmente: solo lo consideramos válido si el raycast
            // con la máscara correcta también confirma el suelo.
            Vector3 rayStart = _characterController.bounds.center;
            float rayDistance = _characterController.bounds.extents.y + 0.15f;

            // Raycast con máscara de capas — ignora triggers y capas no-suelo (ej. capa "Player")
            bool rayGrounded = Physics.Raycast(
                rayStart,
                Vector3.down,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            // ccGrounded es el check nativo del CharacterController (rápido pero sin LayerMask).
            // Lo combinamos con AND del raycast para evitar falsos positivos sobre otros Slimes.
            bool ccGrounded = _characterController.isGrounded && rayGrounded;

            // No considerar "grounded" mientras estamos ascendiendo activamente (recién saltamos).
            // Sin esto, el raycast de este mismo frame en que se dispara el salto todavía ve el
            // suelo (porque CharacterController.Move() todavía no aplicó el impulso hacia arriba
            // este frame, eso pasa en SlimeMovement), lo que resetea el coyote timer a full y deja
            // una ventana de ~_coyoteTime segundos donde un segundo input de salto vuelve a sumar
            // impulso a mitad de vuelo.
            bool isAscending = _verticalVelocity > 0.01f;

            _isGrounded = (ccGrounded || rayGrounded) && !isAscending;

            if (_isGrounded)
            {
                _jumpsUsed = 0;
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

        // private void HandleJump()
        // {
        //     if (!ShouldProcessInput) return;

        //     if (_coyoteTimer > 0f)
        //     {
        //         // Leer fuerza de salto del modifier (puede ser 2x si Agile está activo, 0.1x si Solid activo)
        //         float jumpForce = _statsModifier != null ? _statsModifier.JumpForce
        //                         : (_slimeData != null ? _slimeData.JumpForce : 8.0f);
        //         _verticalVelocity = jumpForce;
        //         _coyoteTimer = 0f;
        //     }
        // }
        private void HandleJump()
        {
            if (!ShouldProcessInput)
                return;

            int maxJumpCount =
                _statsModifier != null
                    ? _statsModifier.MaxJumpCount
                    : 1;

            float jumpForce =
                _statsModifier != null
                    ? _statsModifier.JumpForce
                    : (_slimeData != null
                        ? _slimeData.JumpForce
                        : 8f);

            Debug.Log(
                $"[SlimeJump] Jump input | " +
                $"Grounded={_isGrounded} | " +
                $"Coyote={_coyoteTimer:F2} | " +
                $"JumpsUsed={_jumpsUsed} | " +
                $"MaxJumps={maxJumpCount} | " +
                $"JumpForce={jumpForce:F2}",
                this
            );

            bool canGroundJump =
                _coyoteTimer > 0f;

            bool canAirJump =
                !_isGrounded &&
                _coyoteTimer <= 0f &&
                _jumpsUsed < maxJumpCount;

            if (!canGroundJump && !canAirJump)
                return;

            _verticalVelocity = jumpForce;

            if (canGroundJump)
            {
                _jumpsUsed = 1;
            }
            else
            {
                _jumpsUsed++;
            }

            _coyoteTimer = 0f;
        }

        public void HandleCollisionFlags(CollisionFlags collisionFlags)
        {
            if ((collisionFlags & CollisionFlags.Above) != 0 &&
                _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }

            if ((collisionFlags & CollisionFlags.Below) != 0 &&
                _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
        }

        private void ApplyGravity()
        {
            // Leer gravedad del modifier (Slime Sólido en forma pesada tendrá más gravedad)
            float gravity = _statsModifier != null ? _statsModifier.Gravity
                          : (_slimeData != null ? _slimeData.Gravity : -20.0f);
            _verticalVelocity += gravity * Time.deltaTime;
        }
    }
}
