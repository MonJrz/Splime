using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    public class Valve : NetworkBehaviour
    {
        [Header("Optional Lock")]
        [SerializeField] private PuzzleMechanismLock _mechanismLock;

        [Header("Events")]
        [SerializeField] private UnityEvent _onOpened = new UnityEvent();
        [SerializeField] private UnityEvent _onClosed = new UnityEvent();

        // Para pruebas offline.
        private bool _localIsOpen;

        // Fuente de verdad online.
        private readonly NetworkVariable<bool> _networkIsOpen =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public bool IsOpen =>
            IsSpawned
                ? _networkIsOpen.Value
                : _localIsOpen;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _networkIsOpen.OnValueChanged += OnOpenStateChanged;

            // Aplica el estado actual también al conectarse/spawnear.
            ApplyState(_networkIsOpen.Value);
        }

        public override void OnNetworkDespawn()
        {
            _networkIsOpen.OnValueChanged -= OnOpenStateChanged;

            base.OnNetworkDespawn();
        }

        public void Toggle()
        {
            // ─────────────────────────────
            // PRUEBA LOCAL / SIN NETWORK
            // ─────────────────────────────
            if (!IsSpawned)
            {
                if (IsBlocked())
                    return;

                _localIsOpen = !_localIsOpen;
                ApplyState(_localIsOpen);
                return;
            }

            // ─────────────────────────────
            // ONLINE
            // ─────────────────────────────

            if (IsServer)
            {
                ToggleServer();
            }
            else
            {
                RequestToggleRpc();
            }
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone
        )]
        private void RequestToggleRpc()
        {
            ToggleServer();
        }

        private void ToggleServer()
        {
            if (!IsServer)
                return;

            if (IsBlocked())
                return;

            _networkIsOpen.Value =
                !_networkIsOpen.Value;
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

        private void OnOpenStateChanged(
            bool previousValue,
            bool newValue)
        {
            ApplyState(newValue);
        }

        private void ApplyState(bool open)
        {
            _localIsOpen = open;

            if (open)
            {
                _onOpened.Invoke();
            }
            else
            {
                _onClosed.Invoke();
            }
        }
    }
}