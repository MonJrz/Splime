using Splime.Network;
using Splime.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelFlowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelUIController _levelUIController;
        [SerializeField] private Button[] _hostOnlyButtons;

        [Header("Scene Flow")]
        [SerializeField] private string _nextLevelSceneName;
        [SerializeField] private string _levelSelectionSceneName = "Main";
        [SerializeField] private string _leaveDestinationSceneName = "Main";

        private SlimeInput _localInput;
        private NetworkManager _networkManager;
        private bool _isChangingScene;

        public bool IsChangingScene => _isChangingScene;
        public bool CanControlLevel =>
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening ||
            NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            if (_levelUIController == null)
            {
                _levelUIController = GetComponent<LevelUIController>();
            }
        }

        private void OnEnable()
        {
            if (_levelUIController == null)
            {
                Debug.LogError($"[{nameof(LevelFlowController)}] LevelUIController reference is missing.", this);
                enabled = false;
                return;
            }

            _levelUIController.RestartRequested += HandleRestartRequested;
            _levelUIController.PauseRequested += HandlePauseRequested;
            _levelUIController.LeaveSessionRequested += HandleLeaveRequested;
            _levelUIController.NextLevelRequested += HandleNextLevelRequested;
            _levelUIController.LevelSelectionRequested += HandleLevelSelectionRequested;
            _levelUIController.InputBlockChanged += HandleInputBlockChanged;
            SlimeInput.LocalInputReady += HandleLocalInputReady;
            SlimeInput.PauseReceived += HandlePauseReceived;

            BindNetworkEvents();
            FindLocalInput();
            ApplyCurrentInputState();
            RefreshHostOnlyButtons();
        }

        private void OnDisable()
        {
            if (_levelUIController != null)
            {
                _levelUIController.RestartRequested -= HandleRestartRequested;
                _levelUIController.PauseRequested -= HandlePauseRequested;
                _levelUIController.LeaveSessionRequested -= HandleLeaveRequested;
                _levelUIController.NextLevelRequested -= HandleNextLevelRequested;
                _levelUIController.LevelSelectionRequested -= HandleLevelSelectionRequested;
                _levelUIController.InputBlockChanged -= HandleInputBlockChanged;
            }

            SlimeInput.LocalInputReady -= HandleLocalInputReady;
            SlimeInput.PauseReceived -= HandlePauseReceived;
            UnbindNetworkEvents();

            if (_localInput != null)
            {
                _localInput.SetInputBlocked(false);
            }
        }

        private void HandleRestartRequested()
        {
            RequestLevelLoad(SceneManager.GetActiveScene().name);
        }

        private void HandlePauseRequested()
        {
            if (_localInput == null)
            {
                FindLocalInput();
            }

            _localInput?.RequestPauseForAllPlayers();
        }

        private void HandlePauseReceived()
        {
            _levelUIController.ShowPause();
        }

        private void HandleNextLevelRequested()
        {
            if (string.IsNullOrWhiteSpace(_nextLevelSceneName))
            {
                Debug.LogWarning($"[{nameof(LevelFlowController)}] Next level scene is not configured.", this);
                return;
            }

            RequestLevelLoad(_nextLevelSceneName);
        }

        private void HandleLeaveRequested()
        {
            LeaveSessionAndLoad(_leaveDestinationSceneName);
        }

        private void HandleLevelSelectionRequested()
        {
            LeaveSessionAndLoad(_levelSelectionSceneName);
        }

        private void RequestLevelLoad(string sceneName)
        {
            if (_isChangingScene || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!CanControlLevel)
            {
                Debug.LogWarning($"[{nameof(LevelFlowController)}] Only the host can change the level.", this);
                return;
            }

            _isChangingScene = true;

            if (NetworkGameManager.Instance != null)
            {
                if (!NetworkGameManager.Instance.TryLoadLevelScene(sceneName))
                {
                    _isChangingScene = false;
                }

                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                _isChangingScene = false;
                Debug.LogError($"[{nameof(LevelFlowController)}] Scene '{sceneName}' is not in Build Settings.", this);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private async void LeaveSessionAndLoad(string destinationSceneName)
        {
            if (_isChangingScene || string.IsNullOrWhiteSpace(destinationSceneName))
            {
                return;
            }

            _isChangingScene = true;

            if (NetworkGameManager.Instance != null)
            {
                await NetworkGameManager.Instance.DisconnectAsync();
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            if (!Application.CanStreamedLevelBeLoaded(destinationSceneName))
            {
                _isChangingScene = false;
                Debug.LogError(
                    $"[{nameof(LevelFlowController)}] Scene '{destinationSceneName}' is not in Build Settings.",
                    this);
                return;
            }

            SceneManager.LoadScene(destinationSceneName);
        }

        private void BindNetworkEvents()
        {
            UnbindNetworkEvents();
            _networkManager = NetworkManager.Singleton;

            if (_networkManager != null)
            {
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        private void UnbindNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                _networkManager = null;
            }
        }

        private async void HandleClientDisconnected(ulong clientId)
        {
            if (_isChangingScene || _networkManager == null || _networkManager.IsServer)
            {
                return;
            }

            _isChangingScene = true;

            if (NetworkGameManager.Instance != null)
            {
                await NetworkGameManager.Instance.DisconnectAsync();
            }

            if (Application.CanStreamedLevelBeLoaded(_leaveDestinationSceneName))
            {
                SceneManager.LoadScene(_leaveDestinationSceneName);
            }
            else
            {
                _isChangingScene = false;
                Debug.LogError(
                    $"[{nameof(LevelFlowController)}] Scene '{_leaveDestinationSceneName}' is not in Build Settings.",
                    this);
            }
        }

        private void HandleInputBlockChanged(bool isBlocked)
        {
            if (_localInput == null)
            {
                FindLocalInput();
            }

            _localInput?.SetInputBlocked(isBlocked);
        }

        private void HandleLocalInputReady(SlimeInput slimeInput)
        {
            if (slimeInput == null || !slimeInput.IsLocalInputSource)
            {
                return;
            }

            _localInput = slimeInput;
            ApplyCurrentInputState();
        }

        private void FindLocalInput()
        {
            SlimeInput[] inputs = FindObjectsByType<SlimeInput>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (SlimeInput input in inputs)
            {
                if (input.IsLocalInputSource)
                {
                    _localInput = input;
                    return;
                }
            }
        }

        private void ApplyCurrentInputState()
        {
            if (_localInput != null && _levelUIController != null)
            {
                _localInput.SetInputBlocked(_levelUIController.IsInputBlocked);
            }
        }

        private void RefreshHostOnlyButtons()
        {
            foreach (Button button in _hostOnlyButtons)
            {
                if (button != null)
                {
                    button.interactable = CanControlLevel;
                }
            }
        }
    }
}
