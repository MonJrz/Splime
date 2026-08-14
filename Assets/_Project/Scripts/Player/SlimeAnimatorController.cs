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

        private void Awake()
        {
            _slimeMovement = GetComponent<SlimeMovement>();
            _slimeJump = GetComponent<SlimeJump>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator == null)
            {
                Debug.LogWarning($"[{nameof(SlimeAnimatorController)}] No se encontró un Animator en {gameObject.name} ni en sus hijos.", this);
            }
        }

        // LateUpdate en vez de Update: garantiza que SlimeMovement y SlimeJump ya hayan
        // calculado su estado de este frame antes de que lo leamos, evitando un frame de retraso.
// IMPORTANTE: Update, no LateUpdate. Unity evalúa el Animator (transiciones, estados)
        // entre Update() y LateUpdate() de todos los scripts. Si seteamos los parámetros en
        // LateUpdate, el Animator los ve recién en el frame SIGUIENTE -> vamos un frame atrás,
        // y en el primer frame de Play usa los valores por defecto (IsGrounded=false), lo que
        // causaba el salto fantasma al arrancar.
// IMPORTANTE: Update, no LateUpdate. Unity evalúa el Animator (transiciones, estados)
        // entre Update() y LateUpdate() de todos los scripts. Si seteamos los parámetros en
        // LateUpdate, el Animator los ve recién en el frame SIGUIENTE -> vamos un frame atrás,
        // y en el primer frame de Play usa los valores por defecto (IsGrounded=false), lo que
        // causaba el salto fantasma al arrancar.
        private void Update()
        {
            if (_animator == null) return;

            float speed = _slimeMovement != null ? _slimeMovement.CurrentVelocity.magnitude : 0f;
            bool isGrounded = _slimeJump != null ? _slimeJump.IsGrounded : true;

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

        /// <summary>
        /// Dispara la animación de interacción (Action). Se llama desde el punto donde
        /// efectivamente ocurre la interacción con un objeto del mundo (Paso 4).
        /// </summary>
public void TriggerAction()
        {
            Debug.Log($"[{nameof(SlimeAnimatorController)}] TriggerAction llamado en '{gameObject.name}'. animator={(_animator != null ? _animator.name : "NULL")}", this);
            if (_animator == null) return;
            _animator.SetTrigger(ActionParam);
        }
    }
}
