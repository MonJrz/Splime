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
        [Header("Visual Settings")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private float _visualScaleFactor = 0.5f;
        private int _normalFormBlockCount;

        private Vector3 _normalVisualScale;

        [Header("Character Controller Settings")]
        [SerializeField] private float _squeezeHeightFactor = 0.3f;
        [SerializeField] private float _squeezeRadiusFactor = 0.5f;

        [Header("Agile Stats")]
        [SerializeField] private float _squeezeWeightMultiplier = 0.5f;

        private float _normalHeight;
        private float _normalRadius;
        private Vector3 _normalCenter;

        // Componentes
        private CharacterController _characterController;
        private SlimeStatsModifier _statsModifier;
        private CharacterSFX _characterSFX;

        // Variable de Red Sincronizada
        private bool _localAgileModeActive;
        private readonly NetworkVariable<bool> _isAgileModeActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public bool IsNormalFormBlocked =>
            _normalFormBlockCount > 0;

        public bool IsAbilityActive =>
            IsSpawned
                ? _isAgileModeActive.Value
                : _localAgileModeActive;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _statsModifier = GetComponent<SlimeStatsModifier>();
            _characterSFX = GetComponentInChildren<CharacterSFX>();

            _normalHeight = _characterController.height;
            _normalRadius = _characterController.radius;
            _normalCenter = _characterController.center;

            if (_visualRoot != null)
            {
                _normalVisualScale = _visualRoot.localScale;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isAgileModeActive.OnValueChanged += OnAgileModeStateChanged;

            bool active = _isAgileModeActive.Value;

            ApplyAgileVisuals(active);
            ApplyAgileStats(active);
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
            SetAgileMode(!IsAbilityActive);
        }

        public void PushNormalFormBlock()
        {
            _normalFormBlockCount++;

            Debug.Log(
                $"[{nameof(PlayerSqueezeAbility)}] " +
                $"Normal form BLOCKED | Count={_normalFormBlockCount}",
                this
            );
        }

        public void PopNormalFormBlock()
        {
            _normalFormBlockCount =
                Mathf.Max(0, _normalFormBlockCount - 1);

            Debug.Log(
                $"[{nameof(PlayerSqueezeAbility)}] " +
                $"Normal form {(IsNormalFormBlocked ? "BLOCKED" : "UNLOCKED")} | " +
                $"Count={_normalFormBlockCount}",
                this
            );
        }

        public void SetAgileMode(bool active)
        {
            // Si estamos actualmente en una zona estrecha,
            // permitir seguir/entrar en Small, pero NO volver a Normal.
            if (!active &&
                IsAbilityActive &&
                IsNormalFormBlocked)
            {
                Debug.Log(
                    $"[{nameof(PlayerSqueezeAbility)}] " +
                    $"No se puede volver a Normal dentro de una zona estrecha.",
                    this
                );

                return;
            }

            // Partida en red
            if (IsSpawned)
            {
                if (!IsOwner)
                    return;

                _isAgileModeActive.Value = active;
                return;
            }

            // Prueba local
            _localAgileModeActive = active;

            ApplyAgileVisuals(active);
            ApplyAgileStats(active);

            if (_characterSFX != null)
            {
                if (active) _characterSFX.PlaySqueezeOn();
                else _characterSFX.PlaySqueezeOff();
            }

            Debug.Log(
                $"[{nameof(PlayerSqueezeAbility)}] " +
                $"Local Agile={active} | " +
                $"MaxJumps={_statsModifier?.MaxJumpCount} | " +
                $"Weight={_statsModifier?.Weight}",
                this
            );
        }

        private void OnAgileModeStateChanged(bool previousValue, bool newValue)
        {
            ApplyAgileVisuals(newValue);
            ApplyAgileStats(newValue);

            if (_characterSFX != null)
            {
                if (newValue) _characterSFX.PlaySqueezeOn();
                else _characterSFX.PlaySqueezeOff();
            }
        }

        private void ApplyAgileVisuals(bool active)
        {
            if (_visualRoot != null)
            {
                _visualRoot.localScale = active
                    ? _normalVisualScale * _visualScaleFactor
                    : _normalVisualScale;
            }

            if (_characterController != null)
            {
                if (active)
                {
                    float newHeight = _normalHeight * _squeezeHeightFactor;
                    float newRadius = _normalRadius * _squeezeRadiusFactor;

                    _characterController.height = newHeight;
                    _characterController.radius = newRadius;

                    Vector3 newCenter = _normalCenter;

                    // Keep the base of the collider roughly
                    // at the same point while reducing its height.
                    float originalBottom =
                        _normalCenter.y - (_normalHeight * 0.5f);

                    newCenter.y =
                        originalBottom + (newHeight * 0.5f) - 0.05f; // Additional adjustment to avoid collisions with the ground

                    _characterController.center = newCenter;
                }
                else
                {
                    _characterController.height = _normalHeight;
                    _characterController.radius = _normalRadius;
                    _characterController.center = _normalCenter;
                }
            }

            Debug.Log(
                $"[{nameof(PlayerSqueezeAbility)}] " +
                $"{(active ? "MODO SMALL" : "MODO NORMAL")} en {gameObject.name}.",
                this);
        }

        private void ApplyAgileStats(bool active)
        {
            if (_statsModifier == null)
                return;

            _statsModifier.MaxJumpCount = active ? 2 : 1;
            _statsModifier.WeightMultiplier =
                active ? _squeezeWeightMultiplier : 1f;
        }
    }
}
