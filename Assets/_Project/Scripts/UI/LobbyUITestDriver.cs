using System.Collections;
using UnityEngine;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyUITestDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LobbyUIController _lobbyUIController;

        [Header("Simulation")]
        [SerializeField] private string _simulatedRoomCode = "ABC123";
        [SerializeField, Min(0f)] private float _requestDelay = 0.5f;
        [SerializeField, Min(0f)] private float _remotePlayerDelay = 1f;
        [SerializeField] private bool _autoConnectRemotePlayer = true;
        [SerializeField] private bool _autoReadyRemotePlayer = true;
        [SerializeField] private bool _rejectHostRequest;
        [SerializeField] private bool _rejectJoinRequest;

        private bool _isLocalHost;
        private bool _hostReady;
        private bool _guestReady;
        private Coroutine _activeSimulation;

        public void HandleHostRequested()
        {
            StartSimulation(SimulateHostRequest());
        }

        public void HandleJoinRequested(string joinCode)
        {
            StartSimulation(SimulateJoinRequest(joinCode));
        }

        public void HandleReadyChangeRequested(bool isReady)
        {
            StartSimulation(SimulateReadyChange(isReady));
        }

        public void HandleStartGameRequested()
        {
            StartSimulation(SimulateStartGameRequest());
        }

        public void HandleLeaveSessionRequested()
        {
            StartSimulation(SimulateLeaveSessionRequest());
        }

        public void HandleBackToMainRequested()
        {
            Debug.Log("[Lobby UI Test] Back-to-main request received. No scene was loaded by the test driver.", this);
        }

        [ContextMenu("Simulate Remote Player Connected")]
        public void SimulateRemotePlayerConnected()
        {
            if (_lobbyUIController == null)
            {
                return;
            }

            ShowSharedLobby();
        }

        [ContextMenu("Toggle Remote Ready")]
        public void ToggleRemoteReady()
        {
            if (_lobbyUIController == null)
            {
                return;
            }

            if (_isLocalHost)
            {
                _guestReady = !_guestReady;
            }
            else
            {
                _hostReady = !_hostReady;
            }

            _lobbyUIController.SetReadyStates(_hostReady, _guestReady);
        }

        private IEnumerator SimulateHostRequest()
        {
            yield return WaitForSimulationDelay(_requestDelay);

            if (_rejectHostRequest)
            {
                _lobbyUIController.ShowError("Simulated host request failure.");
                yield break;
            }

            _isLocalHost = true;
            _hostReady = false;
            _guestReady = false;
            _lobbyUIController.ShowHostWaitingRoom(_simulatedRoomCode);

            if (!_autoConnectRemotePlayer)
            {
                yield break;
            }

            yield return WaitForSimulationDelay(_remotePlayerDelay);
            ShowSharedLobby();
        }

        private IEnumerator SimulateJoinRequest(string joinCode)
        {
            yield return WaitForSimulationDelay(_requestDelay);

            if (_rejectJoinRequest)
            {
                _lobbyUIController.ShowError($"Simulated join failure for code {joinCode}.");
                yield break;
            }

            _isLocalHost = false;
            _hostReady = false;
            _guestReady = false;
            ShowSharedLobby();
        }

        private IEnumerator SimulateReadyChange(bool isReady)
        {
            yield return WaitForSimulationDelay(_requestDelay);

            if (_isLocalHost)
            {
                _hostReady = isReady;

                if (_autoReadyRemotePlayer)
                {
                    _guestReady = isReady;
                }
            }
            else
            {
                _guestReady = isReady;

                if (_autoReadyRemotePlayer)
                {
                    _hostReady = isReady;
                }
            }

            _lobbyUIController.SetReadyStates(_hostReady, _guestReady);
        }

        private IEnumerator SimulateStartGameRequest()
        {
            yield return WaitForSimulationDelay(_requestDelay);
            _lobbyUIController.SetBusy(false);
            Debug.Log("[Lobby UI Test] Start-game request received. No gameplay scene was loaded by the test driver.", this);
        }

        private IEnumerator SimulateLeaveSessionRequest()
        {
            yield return WaitForSimulationDelay(_requestDelay);
            _hostReady = false;
            _guestReady = false;
            _lobbyUIController.NotifySessionLeft();
        }

        private void ShowSharedLobby()
        {
            if (_isLocalHost)
            {
                _lobbyUIController.ShowSharedLobbyAsHost();
            }
            else
            {
                _lobbyUIController.ShowSharedLobbyAsGuest();
            }

            _lobbyUIController.SetConnectedPlayerCount(2);
            _lobbyUIController.SetReadyStates(_hostReady, _guestReady);
        }

        private void StartSimulation(IEnumerator simulation)
        {
            if (_lobbyUIController == null)
            {
                Debug.LogError("[Lobby UI Test] LobbyUIController reference is missing.", this);
                return;
            }

            if (_activeSimulation != null)
            {
                StopCoroutine(_activeSimulation);
            }

            _activeSimulation = StartCoroutine(RunSimulation(simulation));
        }

        private IEnumerator RunSimulation(IEnumerator simulation)
        {
            yield return simulation;
            _activeSimulation = null;
        }

        private static IEnumerator WaitForSimulationDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
        }
    }
}
