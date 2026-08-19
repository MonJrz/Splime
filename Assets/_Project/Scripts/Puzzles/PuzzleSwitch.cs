using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    public enum PuzzleSwitchMode
    {
        ToggleMover,
        MechanismLock
    }

    [RequireComponent(typeof(NetworkObject))]
    public class PuzzleSwitch : NetworkBehaviour
    {
        [Header("Mode")]
        [SerializeField] private PuzzleSwitchMode _mode;

        [Header("Toggle Mover Mode")]
        [SerializeField] private ModularMover _targetMover;

        [Header("Mechanism Lock Mode")]
        [SerializeField] private PuzzleMechanismLock _mechanismLock;

        [Header("Feedback Events")]
        [SerializeField] private UnityEvent _onStateA = new UnityEvent();
        [SerializeField] private UnityEvent _onStateB = new UnityEvent();

        // Estado local/offline.
        private bool _localState;

        // Estado compartido online.
        private readonly NetworkVariable<bool> _networkState =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public bool State =>
            IsSpawned
                ? _networkState.Value
                : _localState;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _networkState.OnValueChanged += OnSwitchStateChanged;

            ApplyFeedback(_networkState.Value);
        }

        public override void OnNetworkDespawn()
        {
            _networkState.OnValueChanged -= OnSwitchStateChanged;

            base.OnNetworkDespawn();
        }

        public void Interact()
        {
            // ─────────────────────────────
            // OFFLINE / PRUEBA LOCAL
            // ─────────────────────────────
            if (!IsSpawned)
            {
                ExecuteLocalInteraction();
                return;
            }

            // ─────────────────────────────
            // ONLINE
            // ─────────────────────────────

            if (IsServer)
            {
                ExecuteServerInteraction();
            }
            else
            {
                RequestInteractRpc();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestInteractRpc()
        {
            ExecuteServerInteraction();
        }

        private void ExecuteLocalInteraction()
        {
            switch (_mode)
            {
                case PuzzleSwitchMode.ToggleMover:
                    ExecuteLocalToggleMover();
                    break;

                case PuzzleSwitchMode.MechanismLock:
                    ExecuteLocalMechanismLock();
                    break;
            }
        }

        private void ExecuteServerInteraction()
        {
            if (!IsServer)
                return;

            switch (_mode)
            {
                case PuzzleSwitchMode.ToggleMover:
                    ExecuteServerToggleMover();
                    break;

                case PuzzleSwitchMode.MechanismLock:
                    ExecuteServerMechanismLock();
                    break;
            }
        }

        // =========================================================
        // TOGGLE MOVER
        // =========================================================

        private void ExecuteLocalToggleMover()
        {
            if (_targetMover == null)
            {
                LogMissingMover();
                return;
            }

            _localState = !_localState;

            _targetMover.TogglePosition();

            ApplyFeedback(_localState);
        }

        private void ExecuteServerToggleMover()
        {
            if (_targetMover == null)
            {
                LogMissingMover();
                return;
            }

            bool newState = !_networkState.Value;

            _networkState.Value = newState;
        }

        // =========================================================
        // MECHANISM LOCK
        // =========================================================

        private void ExecuteLocalMechanismLock()
        {
            if (_mechanismLock == null)
            {
                LogMissingLock();
                return;
            }

            _mechanismLock.ToggleLock();

            _localState = _mechanismLock.IsLocked;

            ApplyFeedback(_localState);
        }

        private void ExecuteServerMechanismLock()
        {
            if (_mechanismLock == null)
            {
                LogMissingLock();
                return;
            }

            _mechanismLock.ToggleLock();

            _networkState.Value =
                _mechanismLock.IsLocked;
        }

        // =========================================================
        // NETWORK STATE
        // =========================================================

        private void OnSwitchStateChanged(
            bool previousValue,
            bool newValue)
        {
            _localState = newValue;

            // En ToggleMover, ambos peers ejecutan localmente
            // el mismo movimiento determinista.
            if (_mode == PuzzleSwitchMode.ToggleMover &&
                _targetMover != null)
            {
                if (newValue)
                    _targetMover.MoveToActive();
                else
                    _targetMover.ReturnToStart();
            }

            ApplyFeedback(newValue);
        }

        private void ApplyFeedback(bool state)
        {
            if (state)
                _onStateB.Invoke();
            else
                _onStateA.Invoke();
        }

        private void LogMissingMover()
        {
            Debug.LogWarning(
                $"[{nameof(PuzzleSwitch)}] " +
                $"No ModularMover assigned on {gameObject.name}.",
                this
            );
        }

        private void LogMissingLock()
        {
            Debug.LogWarning(
                $"[{nameof(PuzzleSwitch)}] " +
                $"No PuzzleMechanismLock assigned on {gameObject.name}.",
                this
            );
        }
    }
}