using Splime.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    public class PuzzleButton : NetworkBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] private bool _oneShot = true;

        [Header("Optional Lock")]
        [SerializeField] private PuzzleMechanismLock _mechanismLock;

        [Header("Events")]
        [SerializeField] private UnityEvent _onPressed = new UnityEvent();

        // Estado local para pruebas sin Netcode.
        private bool _localHasBeenPressed;

        // Estado compartido online.
        private readonly NetworkVariable<bool> _networkHasBeenPressed =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private InteractionPromptTrigger _interactionPromptTrigger;

        public bool HasBeenPressed =>
            IsSpawned
                ? _networkHasBeenPressed.Value
                : _localHasBeenPressed;

        private void Awake()
        {
            _interactionPromptTrigger = GetComponent<InteractionPromptTrigger>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _networkHasBeenPressed.OnValueChanged +=
                OnPressedStateChanged;

            // Si por alguna razón el botón ya estaba usado
            // cuando este peer recibe el estado, aplica su
            // apariencia/efectos correspondientes.
            if (_networkHasBeenPressed.Value)
            {
                ApplyPressedState();
            }
        }

        public override void OnNetworkDespawn()
        {
            _networkHasBeenPressed.OnValueChanged -=
                OnPressedStateChanged;

            base.OnNetworkDespawn();
        }

        public void Press()
        {
            // ─────────────────────────────
            // PRUEBA LOCAL / OFFLINE
            // ─────────────────────────────

            if (!IsSpawned)
            {
                TryPressLocal();
                return;
            }

            // ─────────────────────────────
            // ONLINE
            // ─────────────────────────────

            if (IsServer)
            {
                TryPressServer();
            }
            else
            {
                RequestPressRpc();
            }
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone
        )]
        private void RequestPressRpc()
        {
            TryPressServer();
        }

        private void TryPressLocal()
        {
            if (IsBlocked())
                return;

            if (_oneShot && _localHasBeenPressed)
                return;

            _localHasBeenPressed = true;

            ApplyPressedState();
        }

        private void TryPressServer()
        {
            if (!IsServer)
                return;

            if (IsBlocked())
                return;

            if (_oneShot && _networkHasBeenPressed.Value)
                return;

            // Para los botones actuales One Shot:
            // false -> true provoca OnValueChanged en todos.
            _networkHasBeenPressed.Value = true;
        }

        private bool IsBlocked()
        {
            if (_mechanismLock != null &&
                _mechanismLock.IsLocked)
            {
                return true;
            }

            return false;
        }

        private void OnPressedStateChanged(
            bool previousValue,
            bool newValue)
        {
            _localHasBeenPressed = newValue;

            if (newValue)
            {
                ApplyPressedState();
            }
            else
            {
                _interactionPromptTrigger?.SetInteractionAvailable(true);
            }
        }

        private void ApplyPressedState()
        {
            if (_oneShot)
            {
                _interactionPromptTrigger?.SetInteractionAvailable(false);
            }

            _onPressed.Invoke();
        }

        public void ResetButton()
        {
            // Local/offline.
            if (!IsSpawned)
            {
                _localHasBeenPressed = false;
                _interactionPromptTrigger?.SetInteractionAvailable(true);
                return;
            }

            // Online: sólo Server.
            if (!IsServer)
                return;

            _networkHasBeenPressed.Value = false;
        }
    }
}
