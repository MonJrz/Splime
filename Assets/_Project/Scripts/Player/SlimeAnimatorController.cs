using UnityEngine;

namespace Splime.Player
{
    /// <summary>
    /// Traduce el estado de movimiento del Slime (SlimeMovement, SlimeJump) a los parámetros
    /// del Animator del modelo visual, para que la máquina de estados (Idle/Walk/Jump/Action)
    /// reaccione en tiempo real.
    /// El modelo animado vive como GameObject hijo del jugador (no en la misma raíz que
    /// SlimeMovement/SlimeJump), por eso el Animator se busca en los hijos si no se asigna
    /// manualmente en el Inspector.
    /// </summary>
    [RequireComponent(typeof(SlimeMovement))]
    [RequireComponent(typeof(SlimeJump))]
    // Se ejecuta después que SlimeMovement/SlimeJump (orden 0 por defecto) para garantizar
    // que ya calcularon el estado de este frame antes de que lo leamos.
    [DefaultExecutionOrder(100)]
    public class SlimeAnimatorController : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int ActionParam = Animator.StringToHash("Action");
        private static readonly int WalkSpeedMultiplierParam = Animator.StringToHash("WalkSpeedMultiplier");

        [Header("References")]
        [Tooltip("Si se deja vacío, se busca automáticamente en los hijos (GetComponentInChildren).")]
        [SerializeField] private Animator _animator;

        [Header("Walk Speed Matching")]
        [Tooltip("walk.anim no tiene desplazamiento propio (no usa Root Motion), así que el ciclo de rebote necesita reproducirse más rápido cuanto más rápido se mueva el personaje. Este valor es la velocidad (unidades/seg) para la que el rebote SE VE natural a Speed Multiplier = 1. Ajustar en vivo en Play mode hasta que las 'zancadas' se vean correctas.")]
        [SerializeField] private float _referenceWalkSpeed = 2f;
        [Tooltip("Límites para evitar que el multiplicador se vaya a extremos si la velocidad real varía mucho (ej. por habilidades).")]
        [SerializeField] private float _minWalkSpeedMultiplier = 0.5f;
        [SerializeField] private float _maxWalkSpeedMultiplier = 3f;

        private SlimeMovement _slimeMovement;
        private SlimeJump _slimeJump;
        private CharacterController _characterController;

        private Vector3 _lastPosition;
        private float _smoothedRemoteSpeed;

        private void Awake()
        {
            _slimeMovement = GetComponent<SlimeMovement>();
            _slimeJump = GetComponent<SlimeJump>();
            _characterController = GetComponent<CharacterController>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator == null)
            {
                Debug.LogWarning($"[{nameof(SlimeAnimatorController)}] Animator component not found on {gameObject.name} or its children.", this);
            }
        }

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (_animator == null) return;

            float speed;
            bool isGrounded;

            bool isLocalOwner = _slimeMovement != null && _slimeMovement.IsSpawned && _slimeMovement.IsOwner;

            if (isLocalOwner || (_slimeMovement != null && !_slimeMovement.IsSpawned))
            {
                // Jugador local: respuesta instantánea desde la velocidad calculada por input/físicas
                speed = _slimeMovement != null ? _slimeMovement.CurrentVelocity.magnitude : 0f;
                isGrounded = _slimeJump != null ? _slimeJump.IsGrounded : true;
            }
            else
            {
                // Jugador remoto: calcular la velocidad real horizontal desde la interpolación del NetworkTransform
                float dt = Time.deltaTime;
                if (dt > 0.0001f)
                {
                    Vector3 displacement = transform.position - _lastPosition;
                    displacement.y = 0f;
                    float rawSpeed = displacement.magnitude / dt;
                    _smoothedRemoteSpeed = Mathf.Lerp(_smoothedRemoteSpeed, rawSpeed, dt * 15f);
                    speed = _smoothedRemoteSpeed > 0.05f ? _smoothedRemoteSpeed : 0f;
                }
                else
                {
                    speed = _smoothedRemoteSpeed;
                }

                isGrounded = CheckRemoteGrounded();
            }

            _lastPosition = transform.position;

            _animator.SetFloat(SpeedParam, speed);
            _animator.SetBool(IsGroundedParam, isGrounded);

            // El estado Walk tiene su Speed Multiplier ligado a este parámetro (ver Animator
            // Controller). Sin esto, el ciclo de rebote de walk.anim (que no tiene desplazamiento
            // propio) se reproduce siempre al mismo ritmo sin importar qué tan rápido se mueva
            // el personaje por código, dando la sensación de zancadas gigantes.
            float referenceSpeed = Mathf.Max(_referenceWalkSpeed, 0.01f);
            float walkSpeedMultiplier = speed > 0.01f
                ? Mathf.Clamp(speed / referenceSpeed, _minWalkSpeedMultiplier, _maxWalkSpeedMultiplier)
                : 1f;
            _animator.SetFloat(WalkSpeedMultiplierParam, walkSpeedMultiplier);
        }

        private bool CheckRemoteGrounded()
        {
            if (_characterController != null && _characterController.isGrounded)
            {
                return true;
            }

            Vector3 origin = transform.position + Vector3.up * 0.2f;
            return Physics.Raycast(origin, Vector3.down, 0.45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Dispara la animación de interacción (Action). Se llama desde el punto donde
        /// efectivamente ocurre la interacción con un objeto del mundo (Paso 4).
        /// </summary>
        public void TriggerAction()
        {
            if (_animator == null) return;
            _animator.SetTrigger(ActionParam);
        }
    }
}
