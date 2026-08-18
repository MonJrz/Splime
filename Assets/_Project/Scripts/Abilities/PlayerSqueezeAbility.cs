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
    public class PlayerSqueezeAbility : NetworkBehaviour, ISlimeAbility
    {
        [Header("Agile Mode Scale Settings")]
        [SerializeField] private Vector3 _normalScale = new Vector3(1.0f, 1.0f, 1.0f);
        [SerializeField] private Vector3 _squeezedScale = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("Character Controller Settings")]
        [SerializeField] private float _squeezeHeightFactor = 0.3f;
        [SerializeField] private float _squeezeRadiusFactor = 0.5f;

        [Header("Jump Settings")]
        [SerializeField] private float _normalJumpForce = 10.5f;
        [SerializeField] private float _squeezeJumpForce = 21f;

        private float _normalHeight;
        private float _normalRadius;
        private Vector3 _normalCenter;

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

private void Update()
        {
            if (_statsModifier != null)
            {
                _statsModifier.JumpForceOverride = IsAbilityActive ? _squeezeJumpForce : _normalJumpForce;
            }
        }

private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _statsModifier = GetComponent<SlimeStatsModifier>();

            _normalHeight = _characterController.height;
            _normalRadius = _characterController.radius;
            _normalCenter = _characterController.center;
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

            if (_statsModifier != null)
            {
                Debug.Log($"[{nameof(PlayerSqueezeAbility)}] 📊 JumpForce efectivo = {_statsModifier.JumpForce}");
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
                    _characterController.height = _normalHeight * _squeezeHeightFactor;
                    _characterController.radius = _normalRadius * _squeezeRadiusFactor;
                    _characterController.center = _normalCenter * _squeezeHeightFactor;
                }
                Debug.Log($"[{nameof(PlayerSqueezeAbility)}] 🔵 Slime Ágil activó MODO ESCURRIRSE (Compacto para tuberías) en {gameObject.name}.", this);
            }
            else
            {
                transform.localScale = _normalScale;
                if (_characterController != null)
                {
                    _characterController.height = _normalHeight;
                    _characterController.radius = _normalRadius;
                    _characterController.center = _normalCenter;
                }
                Debug.Log($"[{nameof(PlayerSqueezeAbility)}] 🟢 Slime Ágil volvió a MODO NORMAL en {gameObject.name}.", this);
            }
        }
    }
}
