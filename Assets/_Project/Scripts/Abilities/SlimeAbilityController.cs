using Unity.Netcode;
using UnityEngine;
using Splime.Player;

namespace Splime.Abilities
{
    /// <summary>
    /// Se conecta con SlimeInput para activar o desactivar la habilidad del Slime (ISlimeAbility)
    /// adjunta al GameObject. Garantiza la ejecución autoritativa local (IsOwner o modo prueba !IsSpawned).
    /// </summary>
    [RequireComponent(typeof(SlimeInput))]
    public class SlimeAbilityController : NetworkBehaviour
    {
        private SlimeInput _slimeInput;
        private ISlimeAbility _slimeAbility;

        private void Awake()
        {
            _slimeInput = GetComponent<SlimeInput>();
            _slimeAbility = GetComponent<ISlimeAbility>();
        }

        private void OnEnable()
        {
            if (_slimeInput == null) _slimeInput = GetComponent<SlimeInput>();
            if (_slimeInput != null)
            {
                _slimeInput.OnAbilityPressed -= HandleAbilityInput;
                _slimeInput.OnAbilityPressed += HandleAbilityInput;
            }
        }

        private void OnDisable()
        {
            if (_slimeInput != null)
            {
                _slimeInput.OnAbilityPressed -= HandleAbilityInput;
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

        private void HandleAbilityInput()
        {
            Debug.Log($"[{nameof(SlimeAbilityController)}] ⚡ Evento OnAbilityPressed recibido en {gameObject.name}.", this);

            if (_slimeAbility != null)
            {
                _slimeAbility.ToggleAbility();
            }
            else
            {
                Debug.LogWarning($"[{nameof(SlimeAbilityController)}] ⚠️ No se encontró ningún componente de habilidad (ISlimeAbility) adjunto en {gameObject.name}.", this);
            }
        }
    }
}
