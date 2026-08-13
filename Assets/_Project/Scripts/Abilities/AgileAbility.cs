using Unity.Netcode;
using UnityEngine;
using Splime.Core;

namespace Splime.Abilities
{
    /// <summary>
    /// Habilidad única del Slime Ágil (Slime 2).
    /// Le permite encogerse y volverse compacto ("Modo Escurrirse") para atravesar tuberías,
    /// conductos estrechos y zonas donde el Slime Transformador no puede pasar.
    /// Sincronizado en red mediante NetworkVariable.
    /// </summary>
    public class AgileAbility : NetworkBehaviour, ISlimeAbility
    {
        [Header("Agile Mode Scale Settings")]
        [SerializeField] private Vector3 _normalScale = new Vector3(1.0f, 1.0f, 1.0f);
        [SerializeField] private Vector3 _squeezedScale = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("Character Controller Settings")]
        [SerializeField] private float _normalHeight = 2.0f;
        [SerializeField] private float _normalRadius = 0.5f;
        [SerializeField] private float _squeezedHeight = 0.6f;
        [SerializeField] private float _squeezedRadius = 0.25f;

        // Componentes
        private CharacterController _characterController;
        private SlimeStatsModifier _statsModifier;

        // Variable de Red Sincronizada
        private readonly NetworkVariable<bool> _isAgileModeActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public bool IsAbilityActive => _isAgileModeActive.Value;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _statsModifier = GetComponent<SlimeStatsModifier>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isAgileModeActive.OnValueChanged += OnAgileModeStateChanged;
            ApplyAgileVisuals(_isAgileModeActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _isAgileModeActive.OnValueChanged -= OnAgileModeStateChanged;
        }

        public void ActivateAbility()
        {
            SetAgileMode(true);
        }

        public void DeactivateAbility()
        {
            SetAgileMode(false);
        }

        public void ToggleAbility()
        {
            SetAgileMode(!_isAgileModeActive.Value);
        }

        public void SetAgileMode(bool active)
        {
            if (IsSpawned && !IsOwner) return;

            if (!IsSpawned)
            {
                _isAgileModeActive.Value = active;
                ApplyAgileVisuals(active);
            }
            else
            {
                _isAgileModeActive.Value = active;
            }

            // Aplicar multiplicadores de stats según el estado
            // Slime Líquido activo: doble de fuerza de salto
            // Slime Líquido normal: stats base
            if (_statsModifier != null)
            {
                _statsModifier.JumpMultiplier = active ? 2f : 1f;
                Debug.Log($"[{nameof(AgileAbility)}] 📊 JumpMultiplier = {_statsModifier.JumpMultiplier} " +
                          $"(JumpForce efectivo: {_statsModifier.JumpForce})");
            }
        }

        private void OnAgileModeStateChanged(bool previousValue, bool newValue)
        {
            ApplyAgileVisuals(newValue);
        }

        private void ApplyAgileVisuals(bool active)
        {
            if (active)
            {
                transform.localScale = _squeezedScale;
                if (_characterController != null)
                {
                    _characterController.height = _squeezedHeight;
                    _characterController.radius = _squeezedRadius;
                    _characterController.center = new Vector3(0f, _squeezedHeight / 2.0f, 0f);
                }
                Debug.Log($"[{nameof(AgileAbility)}] 🔵 Slime Ágil activó MODO ESCURRIRSE (Compacto para tuberías) en {gameObject.name}.", this);
            }
            else
            {
                transform.localScale = _normalScale;
                if (_characterController != null)
                {
                    _characterController.height = _normalHeight;
                    _characterController.radius = _normalRadius;
                    _characterController.center = new Vector3(0f, _normalHeight / 2.0f - 1, 0f);
                }
                Debug.Log($"[{nameof(AgileAbility)}] 🟢 Slime Ágil volvió a MODO NORMAL en {gameObject.name}.", this);
            }
        }
    }
}
