using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    /// <summary>
    /// Comprueba que todos los PuzzleButton requeridos hayan sido pulsados.
    /// Cuando se cumple la condición, ejecuta una acción una sola vez.
    ///
    /// Compatible con:
    /// - pruebas locales/offline;
    /// - Host/Client;
    /// - botones pulsados en cualquier orden;
    /// - botones pulsados en momentos diferentes.
    /// </summary>
    public class PuzzleButtonGroup : NetworkBehaviour
    {
        [Header("Required Buttons")]
        [SerializeField] private PuzzleButton[] _requiredButtons;

        [Header("Behaviour")]
        [SerializeField] private bool _oneShot = true;

        [Header("Events")]
        [SerializeField] private UnityEvent _onAllButtonsPressed =
            new UnityEvent();

        private bool _localActivated;

        private readonly NetworkVariable<bool> _networkActivated =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public bool IsActivated =>
            IsSpawned
                ? _networkActivated.Value
                : _localActivated;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _networkActivated.OnValueChanged +=
                OnActivatedStateChanged;

            if (_networkActivated.Value)
            {
                ApplyActivatedState();
            }
        }

        public override void OnNetworkDespawn()
        {
            _networkActivated.OnValueChanged -=
                OnActivatedStateChanged;

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Llamar desde OnPressed de cualquiera de los botones
        /// que pertenecen al grupo.
        /// </summary>
        public void Evaluate()
        {
            // ─────────────────────────────
            // LOCAL / OFFLINE
            // ─────────────────────────────
            if (!IsSpawned)
            {
                EvaluateLocal();
                return;
            }

            // ─────────────────────────────
            // ONLINE
            // Sólo el servidor decide cuándo
            // se cumple la condición.
            // ─────────────────────────────
            if (!IsServer)
                return;

            EvaluateServer();
        }

        private void EvaluateLocal()
        {
            if (_oneShot && _localActivated)
                return;

            if (!AreAllButtonsPressed())
                return;

            _localActivated = true;
            ApplyActivatedState();
        }

        private void EvaluateServer()
        {
            if (_oneShot && _networkActivated.Value)
                return;

            if (!AreAllButtonsPressed())
                return;

            _networkActivated.Value = true;
        }

        private bool AreAllButtonsPressed()
        {
            if (_requiredButtons == null ||
                _requiredButtons.Length == 0)
            {
                Debug.LogWarning(
                    $"[{nameof(PuzzleButtonGroup)}] No required buttons assigned on {gameObject.name}.",
                    this);

                return false;
            }

            foreach (PuzzleButton button in _requiredButtons)
            {
                if (button == null || !button.HasBeenPressed)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnActivatedStateChanged(
            bool previousValue,
            bool newValue)
        {
            _localActivated = newValue;

            if (newValue)
            {
                ApplyActivatedState();
            }
        }

        private void ApplyActivatedState()
        {
            _onAllButtonsPressed.Invoke();
        }
    }
}