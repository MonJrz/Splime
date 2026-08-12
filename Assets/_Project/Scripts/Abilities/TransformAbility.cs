using Unity.Netcode;
using UnityEngine;

namespace Splime.Abilities
{
    /// <summary>
    /// Enum de las formas posibles del Slime Transformador.
    /// Diseñado de forma modular para agregar nuevas formas fácilmente.
    /// </summary>
    public enum SlimeForm
    {
        Normal = 0,
        Platform = 1,
        Bridge = 2
    }

    /// <summary>
    /// Habilidad modular del Slime Transformador.
    /// Permite activar/desactivar la transformación y sincronizar el estado visual
    /// y físico en red entre Host y Clientes mediante NetworkVariable y ServerRpc.
    /// </summary>
    public class TransformAbility : NetworkBehaviour, ISlimeAbility
    {
        [Header("Transform Visual Settings")]
        [SerializeField] private Vector3 _normalScale = new Vector3(1.0f, 1.0f, 1.0f);
        [SerializeField] private Vector3 _platformScale = new Vector3(2.2f, 0.4f, 2.2f);

        [Header("Character Controller Adjustment")]
        [SerializeField] private float _normalHeight = 2.0f;
        [SerializeField] private float _platformHeight = 0.8f;

        // Components
        private CharacterController _characterController;

        // Synchronized Network Variable (0 = Normal, 1 = Platform, etc.)
        private readonly NetworkVariable<int> _currentFormIndex = new NetworkVariable<int>(
            (int)SlimeForm.Normal,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner // Permite al cliente dueño cambiar la forma directamente
        );

        public bool IsAbilityActive => _currentFormIndex.Value != (int)SlimeForm.Normal;
        public SlimeForm CurrentForm => (SlimeForm)_currentFormIndex.Value;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Suscribirse a cambios en la variable de red para actualizar visuales en todos los clientes
            _currentFormIndex.OnValueChanged += OnFormIndexChanged;

            // Aplicar estado inicial
            ApplyFormVisuals((SlimeForm)_currentFormIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _currentFormIndex.OnValueChanged -= OnFormIndexChanged;
        }

        public void ActivateAbility()
        {
            SetForm(SlimeForm.Platform);
        }

        public void DeactivateAbility()
        {
            SetForm(SlimeForm.Normal);
        }

        public void ToggleAbility()
        {
            if (IsAbilityActive)
            {
                DeactivateAbility();
            }
            else
            {
                ActivateAbility();
            }
        }

        public void SetForm(SlimeForm newForm)
        {
            // Solo procesar si somos el dueño (IsOwner) o si estamos en pruebas locales sin red (!IsSpawned)
            if (IsSpawned && !IsOwner) return;

            int newIndex = (int)newForm;

            if (!IsSpawned)
            {
                // Modo pruebas locales en Unity Editor
                _currentFormIndex.Value = newIndex;
                ApplyFormVisuals(newForm);
            }
            else
            {
                // Modo Red NGO (escribir en NetworkVariable)
                _currentFormIndex.Value = newIndex;
            }
        }

        private void OnFormIndexChanged(int previousValue, int newValue)
        {
            ApplyFormVisuals((SlimeForm)newValue);
        }

        private void ApplyFormVisuals(SlimeForm form)
        {
            switch (form)
            {
                case SlimeForm.Platform:
                    transform.localScale = _platformScale;
                    if (_characterController != null)
                    {
                        _characterController.height = _platformHeight;
                        _characterController.center = new Vector3(0f, _platformHeight / 2.0f, 0f);
                    }
                    Debug.Log($"[{nameof(TransformAbility)}] 🟩 Slime Transformado en PLATAFORMA en {gameObject.name}.", this);
                    break;

                case SlimeForm.Normal:
                default:
                    transform.localScale = _normalScale;
                    if (_characterController != null)
                    {
                        _characterController.height = _normalHeight;
                        _characterController.center = new Vector3(0f, _normalHeight / 2.0f - 1, 0f); // se ajusta el centro para que el Slime esté correctamente posicionado en el suelo
                    }
                    Debug.Log($"[{nameof(TransformAbility)}] 🟢 Slime regresó a Forma NORMAL en {gameObject.name}.", this);
                    break;
            }
        }
    }
}
