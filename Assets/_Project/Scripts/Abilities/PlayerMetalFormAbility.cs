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

        [Header("Movement Settings")]
        [SerializeField] private float _normalMoveSpeed = 6f;
        [SerializeField] private float _metalMoveSpeed = 3f;

        [Header("Push Settings")]
        [SerializeField] private float _pushStrength = 10f;

        [Header("Jump Settings")]
        [SerializeField] private float _metalJumpForce = 0f;

        private Material _normalMaterial;
        private SlimeStatsModifier _statsModifier;

        private readonly NetworkVariable<bool> _isMetalFormActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public bool IsAbilityActive => _isMetalFormActive.Value;

private void Awake()
        {
            _statsModifier = GetComponent<SlimeStatsModifier>();

            if (_bodyRenderer == null)
            {
                _bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (_bodyRenderer != null)
            {
                _normalMaterial = _bodyRenderer.sharedMaterial;
            }
        }

private void Update()
        {
            if (_statsModifier != null)
            {
                _statsModifier.MoveSpeedOverride = IsAbilityActive ? _metalMoveSpeed : _normalMoveSpeed;
                _statsModifier.PushStrength = IsAbilityActive ? _pushStrength : 0f;
                _statsModifier.JumpForceOverride = IsAbilityActive ? _metalJumpForce : (float?)null;
            }
        }


public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isMetalFormActive.OnValueChanged += OnMetalFormStateChanged;
            ApplyMetalFormVisuals(_isMetalFormActive.Value);
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
            SetMetalForm(!_isMetalFormActive.Value);
        }

public void SetMetalForm(bool active)
        {
            if (IsSpawned && !IsOwner) return;

            if (!IsSpawned)
            {
                _isMetalFormActive.Value = active;
                ApplyMetalFormVisuals(active);
            }
            else
            {
                _isMetalFormActive.Value = active;
            }
        }

private void OnMetalFormStateChanged(bool previousValue, bool newValue)
        {
            ApplyMetalFormVisuals(newValue);
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
    

private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsAbilityActive) return;

            var pushable = hit.collider.GetComponent<PushableObject>();
            if (pushable == null) return;

            Vector3 direction = hit.moveDirection;
            direction.y = 0f;
            pushable.TryPush(direction.normalized, _pushStrength, Time.deltaTime);
        }
}
}
