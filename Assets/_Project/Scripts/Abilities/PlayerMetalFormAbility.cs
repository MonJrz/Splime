using Unity.Netcode;
using UnityEngine;
using Splime.Core;
using Splime.Puzzles;

namespace Splime.Abilities
{
    public class PlayerMetalFormAbility : NetworkBehaviour, ISlimeAbility
    {
        [Header("Visual Settings")]
        [SerializeField] private SkinnedMeshRenderer _bodyRenderer;
        [SerializeField] private Material _metalMaterial;


        [Header("Metal Form Stats")]
        [SerializeField] private float _metalSpeedMultiplier = 0.6f;
        [SerializeField] private float _metalJumpMultiplier = 0.5f;
        [SerializeField] private float _metalGravityMultiplier = 1.35f;
        [SerializeField] private float _metalWeightMultiplier = 5f;
        [SerializeField] private float _metalPushStrength = 10f;

        private CharacterSFX _characterSFX;

        private Material _normalMaterial;
        private SlimeStatsModifier _statsModifier;

        private readonly NetworkVariable<bool> _isMetalFormActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        private bool _localMetalFormActive;

        public bool IsAbilityActive =>
            IsSpawned
                ? _isMetalFormActive.Value
                : _localMetalFormActive;

        private void Awake()
        {
            _statsModifier = GetComponent<SlimeStatsModifier>();
            _characterSFX = GetComponentInChildren<CharacterSFX>();

            if (_bodyRenderer == null)
            {
                _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (_bodyRenderer != null)
            {
                _normalMaterial = _bodyRenderer.sharedMaterial;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isMetalFormActive.OnValueChanged += OnMetalFormStateChanged;
            bool active = _isMetalFormActive.Value;
            
            ApplyMetalFormVisuals(_isMetalFormActive.Value);
            ApplyMetalStats(active);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _isMetalFormActive.OnValueChanged -= OnMetalFormStateChanged;
        }

        public void ActivateAbility()
        {
            SetMetalForm(true);
        }

        public void DeactivateAbility()
        {
            SetMetalForm(false);
        }

        public void ToggleAbility()
        {
            SetMetalForm(!IsAbilityActive);
        }

        public void SetMetalForm(bool active)
        {
            // Partida en red
            if (IsSpawned)
            {
                if (!IsOwner)
                    return;

                _isMetalFormActive.Value = active;
                return;
            }

            // Prueba local
            _localMetalFormActive = active;

            ApplyMetalFormVisuals(active);
            ApplyMetalStats(active);

            if (_characterSFX != null)
            {
                if (active) _characterSFX.PlayMetalFormOn();
                else _characterSFX.PlayMetalFormOff();
            }
        }

        private void OnMetalFormStateChanged(bool previousValue, bool newValue)
        {
            ApplyMetalFormVisuals(newValue);
            ApplyMetalStats(newValue);

            if (_characterSFX != null)
            {
                if (newValue) _characterSFX.PlayMetalFormOn();
                else _characterSFX.PlayMetalFormOff();
            }
        }

        private void ApplyMetalFormVisuals(bool active)
        {
            if (_bodyRenderer == null) return;

            if (active)
            {
                if (_metalMaterial != null)
                {
                    _bodyRenderer.material = _metalMaterial;
                }
                Debug.Log($"[{nameof(PlayerMetalFormAbility)}] Modo metalico activado en {gameObject.name}.", this);
            }
            else
            {
                _bodyRenderer.material = _normalMaterial;
                Debug.Log($"[{nameof(PlayerMetalFormAbility)}] Modo normal en {gameObject.name}.", this);
            }
        }

        private void ApplyMetalStats(bool active)
        {
            if (_statsModifier == null)
                return;

            if (active)
            {
                _statsModifier.SpeedMultiplier = _metalSpeedMultiplier;
                _statsModifier.JumpMultiplier = _metalJumpMultiplier;
                _statsModifier.GravityMultiplier = _metalGravityMultiplier;
                _statsModifier.WeightMultiplier = _metalWeightMultiplier;
                _statsModifier.StrengthOverride = _metalPushStrength;
            }
            else
            {
                _statsModifier.SpeedMultiplier = 1f;
                _statsModifier.JumpMultiplier = 1f;
                _statsModifier.GravityMultiplier = 1f;
                _statsModifier.WeightMultiplier = 1f;
                _statsModifier.StrengthOverride = null;
            }
        }
    

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsAbilityActive) return;

            PushableObject pushable = hit.collider.GetComponentInParent<PushableObject>();
            if (pushable == null) return;

            Vector3 direction = hit.moveDirection;
            direction.y = 0f;

            float strength = _statsModifier != null ? _statsModifier.PushStrength : 0f;
            pushable.TryPush(direction.normalized, strength, Time.deltaTime);
        }
    }
}
