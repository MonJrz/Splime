using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    public class PuzzleMechanismLock : NetworkBehaviour
    {
        [Header("State")]
        [SerializeField] private bool _startLocked;

        [Header("Events")]
        [SerializeField] private UnityEvent _onLocked = new UnityEvent();
        [SerializeField] private UnityEvent _onUnlocked = new UnityEvent();

        // Estado para pruebas locales sin Netcode.
        private bool _localIsLocked;

        // Estado autoritativo online.
        private readonly NetworkVariable<bool> _networkIsLocked =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public bool IsLocked =>
            IsSpawned
                ? _networkIsLocked.Value
                : _localIsLocked;

        private void Awake()
        {
            _localIsLocked = _startLocked;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _networkIsLocked.OnValueChanged += OnLockStateChanged;

            // El servidor establece el estado inicial.
            if (IsServer)
            {
                _networkIsLocked.Value = _startLocked;
            }

            // Aplicar el estado actual también al spawnear.
            ApplyState(_networkIsLocked.Value);
        }

        public override void OnNetworkDespawn()
        {
            _networkIsLocked.OnValueChanged -= OnLockStateChanged;

            base.OnNetworkDespawn();
        }

        public void ToggleLock()
        {
            // Offline/local
            if (!IsSpawned)
            {
                SetLocalState(!_localIsLocked);
                return;
            }

            // Online: solo el servidor cambia el estado.
            if (!IsServer)
                return;

            SetNetworkState(!_networkIsLocked.Value);
        }

        public void Lock()
        {
            if (!IsSpawned)
            {
                SetLocalState(true);
                return;
            }

            if (!IsServer)
                return;

            SetNetworkState(true);
        }

        public void Unlock()
        {
            if (!IsSpawned)
            {
                SetLocalState(false);
                return;
            }

            if (!IsServer)
                return;

            SetNetworkState(false);
        }

        private void SetNetworkState(bool locked)
        {
            if (_networkIsLocked.Value == locked)
                return;

            _networkIsLocked.Value = locked;
        }

        private void SetLocalState(bool locked)
        {
            if (_localIsLocked == locked)
                return;

            _localIsLocked = locked;
            ApplyState(locked);
        }

        private void OnLockStateChanged(
            bool previousValue,
            bool newValue)
        {
            ApplyState(newValue);
        }

        private void ApplyState(bool locked)
        {
            _localIsLocked = locked;

            if (locked)
                _onLocked.Invoke();
            else
                _onUnlocked.Invoke();
        }
    }
}