using System.Threading.Tasks;
using System.Collections;
using Splime.Network;
using Splime.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Splime.CameraSystem;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelFlowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelUIController _levelUIController;
        [SerializeField] private TimedOverlayUIController _introController;
        [SerializeField] private HowToPlayCarouselController _howToPlayController;
        [SerializeField] private UIMessageSequence _openingDialogue;
        [SerializeField] private Button[] _hostOnlyButtons;

        [Header("Pre-Level Flow")]
        [SerializeField] private bool _showHowToPlayAfterLobby;

        [Header("Level Timer")]
        [SerializeField, Min(1f)] private float _levelDurationSeconds = 300f;

        [Header("Scene Flow")]
        [SerializeField] private string _nextLevelSceneName;
        [SerializeField] private bool _showBlockingOverlayInsteadOfNextLevel;
        [SerializeField] private string _campaignStartSceneName = "Level1";
        [SerializeField] private string _levelSelectionSceneName = "Main";
        [SerializeField] private string _leaveDestinationSceneName = "Main";

        [Header("Connection Failure")]
        [SerializeField] private string _connectionFailureDestinationSceneName = "Lobby";
        [SerializeField, Min(0f)] private float _connectionFailureRedirectDelay = 3f;
        [SerializeField, TextArea] private string _connectionFailureMessage =
            "Damn it!The other player lost connection.\nReturning to the lobby...";

        private SlimeInput _localInput;
        private NetworkManager _networkManager;
        private PlayerLevelNetworkController _levelNetworkBridge;
        private PlayerLevelNetworkController _localLevelNetworkBridge;
        private bool _isChangingScene;
        private bool _isTimerPaused;
        private bool _isWaitingForIntro;
        private bool _isWaitingForHowToPlay;
        private bool _shouldShowHowToPlay;
        private bool _howToPlayReadySent;
        private bool _startIntroOnStart;
        private bool _hasOpeningDialoguePending;
        private bool _openingDialogueShown;
        private bool _openingDialogueReadySent;
        private bool _levelEnded;
        private bool _failureRequested;
        private int _synchronizedOpeningDialoguePageIndex = -1;
        private int _synchronizedHowToPlayPageIndex = -1;
        private float _remainingTime;
        private int _lastPublishedTime = -1;
        private Coroutine _connectionFailureCoroutine;
        private InputAction _togglePauseAction;

        public bool IsChangingScene => _isChangingScene;
        public bool CanControlLevel =>
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening ||
            NetworkManager.Singleton.IsServer;

        private bool ShouldBlockLocalInput =>
            (_levelUIController != null && _levelUIController.IsInputBlocked) ||
            _isWaitingForHowToPlay ||
            _isWaitingForIntro ||
            _hasOpeningDialoguePending;
            
        private void Awake()
        {
            PlayerLevelNetworkController.ResetLevelState();

            if (_levelUIController == null)
            {
                _levelUIController = GetComponent<LevelUIController>();
            }

            if (_introController == null)
            {
                _introController = GetComponent<TimedOverlayUIController>();
            }

            if (_howToPlayController == null)
            {
                _howToPlayController =
                    FindFirstObjectByType<HowToPlayCarouselController>(
                        FindObjectsInactive.Include);
            }

            _shouldShowHowToPlay =
                _showHowToPlayAfterLobby &&
                _howToPlayController != null &&
                NetworkGameManager.Instance != null &&
                NetworkGameManager.Instance.ConsumeHowToPlayForLobbyEntry();

            _remainingTime = Mathf.Max(1f, _levelDurationSeconds);
            _levelUIController?.SetRemainingTime(Mathf.CeilToInt(_remainingTime));

            _togglePauseAction = new InputAction(
                "Toggle Pause",
                InputActionType.Button,
                "<Keyboard>/p");
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
            _levelUIController.ResumeRequested += HandleResumeRequested;
            _levelUIController.LeaveSessionRequested += HandleLeaveRequested;
            _levelUIController.ConnectionLostAcknowledged += HandleConnectionLostAcknowledged;
            _levelUIController.NextLevelRequested += HandleNextLevelRequested;
            _levelUIController.LevelSelectionRequested += HandleLevelSelectionRequested;
            _levelUIController.ReplayAllRequested += HandleReplayAllRequested;
            _levelUIController.MainMenuRequested += HandleMainMenuRequested;
            _levelUIController.InputBlockChanged += HandleInputBlockChanged;
            _levelUIController.DialogueAdvanceRequested += HandleDialogueAdvanceRequested;
            _levelUIController.DialogueSkipRequested += HandleDialogueSkipRequested;
            _togglePauseAction.performed += HandleTogglePausePerformed;
            _togglePauseAction.Enable();
            SlimeInput.LocalInputReady += HandleLocalInputReady;
            SlimeInput.PauseStateReceived += HandlePauseStateReceived;
            PlayerLevelNetworkController.LevelCompletedReceived += HandleLevelCompletedReceived;
            PlayerLevelNetworkController.LevelFailedReceived += HandleLevelFailedReceived;
            PlayerLevelNetworkController.LevelTimerUpdatedReceived += HandleLevelTimerUpdatedReceived;
            PlayerLevelNetworkController.AvailableContentEndedReceived +=
                HandleAvailableContentEndedReceived;
            PlayerLevelNetworkController.SharedDialoguePageChangedReceived +=
                HandleSharedDialoguePageChangedReceived;
            PlayerLevelNetworkController.SharedDialogueCompletedReceived +=
                HandleSharedDialogueCompletedReceived;
            PlayerLevelNetworkController.SharedHowToPlayPageChangedReceived +=
                HandleSharedHowToPlayPageChangedReceived;
            PlayerLevelNetworkController.SharedHowToPlayCompletedReceived +=
                HandleSharedHowToPlayCompletedReceived;

            if (_howToPlayController != null)
            {
                _howToPlayController.PreviousPageRequested +=
                    HandleHowToPlayPreviousPageRequested;
                _howToPlayController.NextPageRequested +=
                    HandleHowToPlayNextPageRequested;
                _howToPlayController.CloseRequested += HandleHowToPlayCloseRequested;
            }

            if (_introController != null)
            {
                _isWaitingForIntro =
                    _showHowToPlayAfterLobby || _introController.WillShowOnEnable;
                _introController.Completed += HandleIntroCompleted;
            }

            _isWaitingForHowToPlay = _shouldShowHowToPlay;

            _hasOpeningDialoguePending =
                !_openingDialogueShown &&
                _openingDialogue != null &&
                _openingDialogue.PageCount > 0;

            if (_isWaitingForHowToPlay)
            {
                _levelUIController.ShowHowToPlay();
                TryStartHowToPlay();
            }
            else if (_showHowToPlayAfterLobby && _introController != null)
            {
                _startIntroOnStart = true;
            }
            else if (!_isWaitingForIntro)
            {
                TryShowOpeningDialogue();
            }

            BindNetworkEvents();
            FindLocalInput();
            ApplyCurrentInputState();
            RefreshHostOnlyButtons();
        }

        private void Start()
        {
            if (_startIntroOnStart)
            {
                _startIntroOnStart = false;
                StartIntroAfterHowToPlay();
                return;
            }

            // Algunos niveles muestran el TimedOverlay automáticamente
            // mediante TimedOverlayUIController.OnEnable().
            // En ese caso el intro ya está visible y no pasa por
            // StartIntroAfterHowToPlay(), así que activamos aquí la Overview.
            if (_isWaitingForIntro &&
                _introController != null &&
                _introController.IsVisible)
            {
                LocalLevelCameraDirector.Instance?.StartOverview();
            }
        }

        private void OnDisable()
        {
            _togglePauseAction.Disable();
            _togglePauseAction.performed -= HandleTogglePausePerformed;

            if (_levelUIController != null)
            {
                _levelUIController.RestartRequested -= HandleRestartRequested;
                _levelUIController.PauseRequested -= HandlePauseRequested;
                _levelUIController.ResumeRequested -= HandleResumeRequested;
                _levelUIController.LeaveSessionRequested -= HandleLeaveRequested;
                _levelUIController.ConnectionLostAcknowledged -= HandleConnectionLostAcknowledged;
                _levelUIController.NextLevelRequested -= HandleNextLevelRequested;
                _levelUIController.LevelSelectionRequested -= HandleLevelSelectionRequested;
                _levelUIController.ReplayAllRequested -= HandleReplayAllRequested;
                _levelUIController.MainMenuRequested -= HandleMainMenuRequested;
                _levelUIController.InputBlockChanged -= HandleInputBlockChanged;
                _levelUIController.DialogueAdvanceRequested -= HandleDialogueAdvanceRequested;
                _levelUIController.DialogueSkipRequested -= HandleDialogueSkipRequested;
            }

            SlimeInput.LocalInputReady -= HandleLocalInputReady;
            SlimeInput.PauseStateReceived -= HandlePauseStateReceived;
            PlayerLevelNetworkController.LevelCompletedReceived -= HandleLevelCompletedReceived;
            PlayerLevelNetworkController.LevelFailedReceived -= HandleLevelFailedReceived;
            PlayerLevelNetworkController.LevelTimerUpdatedReceived -= HandleLevelTimerUpdatedReceived;
            PlayerLevelNetworkController.AvailableContentEndedReceived -=
                HandleAvailableContentEndedReceived;
            PlayerLevelNetworkController.SharedDialoguePageChangedReceived -=
                HandleSharedDialoguePageChangedReceived;
            PlayerLevelNetworkController.SharedDialogueCompletedReceived -=
                HandleSharedDialogueCompletedReceived;
            PlayerLevelNetworkController.SharedHowToPlayPageChangedReceived -=
                HandleSharedHowToPlayPageChangedReceived;
            PlayerLevelNetworkController.SharedHowToPlayCompletedReceived -=
                HandleSharedHowToPlayCompletedReceived;

            if (_howToPlayController != null)
            {
                _howToPlayController.PreviousPageRequested -=
                    HandleHowToPlayPreviousPageRequested;
                _howToPlayController.NextPageRequested -=
                    HandleHowToPlayNextPageRequested;
                _howToPlayController.CloseRequested -= HandleHowToPlayCloseRequested;
            }

            if (_introController != null)
            {
                _introController.Completed -= HandleIntroCompleted;
            }

            UnbindNetworkEvents();

            SlimeInput[] inputs =
                FindObjectsByType<SlimeInput>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (SlimeInput input in inputs)
            {
                if (input != null && input.IsLocalInputSource)
                {
                    input.SetInputBlocked(false);
                }
            }
        }

        private void OnDestroy()
        {
            _togglePauseAction?.Dispose();
        }

        private void Update()
        {
            if (_isWaitingForHowToPlay)
            {
                TryStartHowToPlay();
            }

            if (_hasOpeningDialoguePending && !_isWaitingForIntro)
            {
                TryShowOpeningDialogue();
            }

            if (_levelEnded ||
                _failureRequested ||
                _isChangingScene ||
                _isTimerPaused ||
                _levelUIController.IsLevelTimerPaused ||
                _isWaitingForHowToPlay ||
                _isWaitingForIntro ||
                _hasOpeningDialoguePending ||
                !HasTimerAuthority)
            {
                return;
            }

            if (IsNetworkSessionActive && !TryGetLevelNetworkBridge(out _))
            {
                return;
            }

            _remainingTime = Mathf.Max(0f, _remainingTime - Time.unscaledDeltaTime);
            int displayedSeconds = Mathf.CeilToInt(_remainingTime);

            if (displayedSeconds != _lastPublishedTime)
            {
                PublishRemainingTime(displayedSeconds);
            }

            if (_remainingTime <= 0f)
            {
                RequestLevelFailure();
            }
        }

        private void HandleIntroCompleted()
        {
            _isWaitingForIntro = false;
            TryShowOpeningDialogue();

            ApplyCurrentInputState();
        }

        private void TryStartHowToPlay()
        {
            if (!_isWaitingForHowToPlay ||
                _howToPlayController == null ||
                _howToPlayController.PageCount <= 0)
            {
                return;
            }

            if (_synchronizedHowToPlayPageIndex >= 0)
            {
                PresentHowToPlayPage(_synchronizedHowToPlayPageIndex);
                return;
            }

            if (_howToPlayReadySent ||
                !TryGetLocalLevelNetworkBridge(
                    out PlayerLevelNetworkController localBridge))
            {
                return;
            }

            _howToPlayReadySent =
                localBridge.MarkSharedHowToPlayReady(_howToPlayController.PageCount);
        }

        private void HandleSharedHowToPlayPageChangedReceived(int pageIndex)
        {
            if (!_shouldShowHowToPlay ||
                _howToPlayController == null ||
                pageIndex < 0 ||
                pageIndex >= _howToPlayController.PageCount)
            {
                return;
            }

            _synchronizedHowToPlayPageIndex = pageIndex;
            PresentHowToPlayPage(pageIndex);
        }

        private void PresentHowToPlayPage(int pageIndex)
        {
            _levelUIController.ShowHowToPlay();
            _howToPlayController.ShowExternallyControlled(pageIndex);
            _isWaitingForHowToPlay = true;
        }

        private void HandleHowToPlayPreviousPageRequested(int expectedPageIndex)
        {
            RequestHowToPlayPageChange(expectedPageIndex, -1);
        }

        private void HandleHowToPlayNextPageRequested(int expectedPageIndex)
        {
            RequestHowToPlayPageChange(expectedPageIndex, 1);
        }

        private void RequestHowToPlayPageChange(int expectedPageIndex, int direction)
        {
            if (TryGetLocalLevelNetworkBridge(
                    out PlayerLevelNetworkController localBridge))
            {
                localBridge.RequestSharedHowToPlayPageChange(
                    expectedPageIndex,
                    direction);
            }
        }

        private void HandleHowToPlayCloseRequested()
        {
            if (TryGetLocalLevelNetworkBridge(
                    out PlayerLevelNetworkController localBridge))
            {
                localBridge.RequestSharedHowToPlayClose();
            }
        }

        private void HandleSharedHowToPlayCompletedReceived()
        {
            if (!_shouldShowHowToPlay)
            {
                return;
            }

            _shouldShowHowToPlay = false;
            _isWaitingForHowToPlay = false;
            _synchronizedHowToPlayPageIndex = -1;

            // Preparar el siguiente bloqueo ANTES de volver a Gameplay.
            if (_introController != null)
            {
                _isWaitingForIntro = true;
            }

            _howToPlayController?.HideExternallyControlled();

            _levelUIController.CompleteHowToPlay();

            ApplyCurrentInputState();

            StartIntroAfterHowToPlay();
        }

        private void StartIntroAfterHowToPlay()
        {
            if (_introController == null)
            {
                _isWaitingForIntro = false;

                TryShowOpeningDialogue();
                ApplyCurrentInputState();

                return;
            }

            _isWaitingForIntro = true;

            ApplyCurrentInputState();

            LocalLevelCameraDirector.Instance?.StartOverview();

            _introController.Show();
        }

        private void TryShowOpeningDialogue()
        {
            if (!_hasOpeningDialoguePending ||
                _levelUIController.CurrentView != LevelUIView.Gameplay)
            {
                return;
            }

            if (IsNetworkSessionActive)
            {
                if (_synchronizedOpeningDialoguePageIndex >= 0)
                {
                    TryPresentSynchronizedOpeningDialogue();
                    return;
                }

                if (_openingDialogueReadySent ||
                    !TryGetLocalLevelNetworkBridge(
                        out PlayerLevelNetworkController localBridge))
                {
                    return;
                }

                _openingDialogueReadySent =
                    localBridge.MarkSharedDialogueReady(_openingDialogue.PageCount);
                return;
            }

            _levelUIController.ShowDialogue(_openingDialogue);
            bool dialogueOpened = _levelUIController.CurrentView == LevelUIView.Dialogue;
            _openingDialogueShown = dialogueOpened;
            _hasOpeningDialoguePending = !dialogueOpened;
        }

        private void HandleSharedDialoguePageChangedReceived(int pageIndex)
        {
            if (_openingDialogue == null ||
                pageIndex < 0 ||
                pageIndex >= _openingDialogue.PageCount ||
                (_synchronizedOpeningDialoguePageIndex >= 0 &&
                 pageIndex < _synchronizedOpeningDialoguePageIndex))
            {
                return;
            }

            _synchronizedOpeningDialoguePageIndex = pageIndex;
            TryPresentSynchronizedOpeningDialogue();
        }

        private void HandleSharedDialogueCompletedReceived()
        {
            if (_openingDialogue == null)
            {
                return;
            }

            _synchronizedOpeningDialoguePageIndex = -1;
            _openingDialogueReadySent = true;
            _openingDialogueShown = true;
            _hasOpeningDialoguePending = false;
            _levelUIController.CompleteSynchronizedDialogue();
        }

        private void TryPresentSynchronizedOpeningDialogue()
        {
            if (_synchronizedOpeningDialoguePageIndex < 0)
            {
                return;
            }

            bool dialogueOpened = _levelUIController.ShowSynchronizedDialoguePage(
                _openingDialogue,
                _synchronizedOpeningDialoguePageIndex);

            if (dialogueOpened)
            {
                _openingDialogueShown = true;
                _hasOpeningDialoguePending = false;
            }
        }

        private void HandleDialogueAdvanceRequested(int expectedPageIndex)
        {
            if (TryGetLocalLevelNetworkBridge(
                    out PlayerLevelNetworkController localBridge))
            {
                localBridge.RequestSharedDialogueAdvance(expectedPageIndex);
            }
        }

        private void HandleDialogueSkipRequested()
        {
            if (TryGetLocalLevelNetworkBridge(
                    out PlayerLevelNetworkController localBridge))
            {
                localBridge.RequestSharedDialogueSkip();
            }
        }

        private void HandleRestartRequested()
        {
            RequestLevelLoad(SceneManager.GetActiveScene().name);
        }

        private void HandlePauseRequested()
        {
            RequestPauseState(true);
        }

        private void HandleResumeRequested()
        {
            RequestPauseState(false);
        }

        private void HandleTogglePausePerformed(InputAction.CallbackContext context)
        {
            if (_isWaitingForHowToPlay ||
                _isWaitingForIntro ||
                _hasOpeningDialoguePending)
            {
                return;
            }

            switch (_levelUIController.CurrentView)
            {
                case LevelUIView.Gameplay:
                case LevelUIView.Tutorial:
                    RequestPauseState(true);
                    break;
                case LevelUIView.Paused:
                    RequestPauseState(false);
                    break;
            }
        }

        private void RequestPauseState(bool isPaused)
        {
            if (_localInput == null)
            {
                FindLocalInput();
            }

            if (_localInput != null)
            {
                _localInput.RequestPauseStateForAllPlayers(isPaused);
                return;
            }

            HandlePauseStateReceived(isPaused);
        }

        private void HandlePauseStateReceived(bool isPaused)
        {
            if (_isChangingScene)
            {
                return;
            }

            _isTimerPaused = isPaused;

            if (isPaused)
            {
                _levelUIController.ShowPause();
            }
            else if (!_levelUIController.IsBlockingOverlayVisible)
            {
                _levelUIController.RestoreViewAfterPause();
            }
        }

        private void HandleLevelCompletedReceived()
        {
            if (!_isChangingScene)
            {
                _levelEnded = true;
                int totalSeconds = Mathf.CeilToInt(Mathf.Max(1f, _levelDurationSeconds));
                int remainingSeconds = _lastPublishedTime >= 0
                    ? _lastPublishedTime
                    : Mathf.CeilToInt(_remainingTime);
                int elapsedSeconds = totalSeconds -
                                     Mathf.Clamp(remainingSeconds, 0, totalSeconds);

                _levelUIController.ShowLevelComplete(elapsedSeconds);
            }
        }

        private void HandleLevelFailedReceived()
        {
            if (_isChangingScene)
            {
                return;
            }

            _levelEnded = true;
            _remainingTime = 0f;
            _levelUIController.SetRemainingTime(0);
            _levelUIController.ShowLevelFailed();
        }

        private void HandleLevelTimerUpdatedReceived(int remainingSeconds)
        {
            if (_levelEnded || _isChangingScene)
            {
                return;
            }

            int clampedSeconds = Mathf.Max(0, remainingSeconds);
            if (!HasTimerAuthority)
            {
                _remainingTime = clampedSeconds;
            }

            _levelUIController.SetRemainingTime(clampedSeconds);
        }

        private void PublishRemainingTime(int remainingSeconds)
        {
            _lastPublishedTime = remainingSeconds;

            if (TryGetLevelNetworkBridge(out PlayerLevelNetworkController bridge))
            {
                bridge.SyncLevelTimerForAllPlayers(remainingSeconds);
                return;
            }

            HandleLevelTimerUpdatedReceived(remainingSeconds);
        }

        private void RequestLevelFailure()
        {
            _failureRequested = true;

            if (TryGetLevelNetworkBridge(out PlayerLevelNetworkController bridge))
            {
                bridge.FailLevelForAllPlayers();
                return;
            }

            HandleLevelFailedReceived();
        }

        private bool TryGetLevelNetworkBridge(out PlayerLevelNetworkController bridge)
        {
            if (_levelNetworkBridge != null &&
                (!IsNetworkSessionActive ||
                 (_levelNetworkBridge.IsSpawned && _levelNetworkBridge.IsServer)))
            {
                bridge = _levelNetworkBridge;
                return true;
            }

            _levelNetworkBridge = null;
            PlayerLevelNetworkController[] players =
                FindObjectsByType<PlayerLevelNetworkController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            foreach (PlayerLevelNetworkController player in players)
            {
                if (player == null ||
                    (IsNetworkSessionActive && (!player.IsSpawned || !player.IsServer)))
                {
                    continue;
                }

                _levelNetworkBridge = player;
                bridge = player;
                return true;
            }

            bridge = null;
            return false;
        }

        private bool TryGetLocalLevelNetworkBridge(
            out PlayerLevelNetworkController bridge)
        {
            if (_localLevelNetworkBridge != null &&
                (!IsNetworkSessionActive ||
                 (_localLevelNetworkBridge.IsSpawned &&
                  _localLevelNetworkBridge.IsOwner)))
            {
                bridge = _localLevelNetworkBridge;
                return true;
            }

            _localLevelNetworkBridge = null;

            if (_localInput != null)
            {
                PlayerLevelNetworkController inputBridge =
                    _localInput.GetComponent<PlayerLevelNetworkController>();

                if (inputBridge != null &&
                    (!IsNetworkSessionActive ||
                     (inputBridge.IsSpawned && inputBridge.IsOwner)))
                {
                    _localLevelNetworkBridge = inputBridge;
                    bridge = inputBridge;
                    return true;
                }
            }

            PlayerLevelNetworkController[] players =
                FindObjectsByType<PlayerLevelNetworkController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            foreach (PlayerLevelNetworkController player in players)
            {
                if (player == null ||
                    (IsNetworkSessionActive &&
                     (!player.IsSpawned || !player.IsOwner)))
                {
                    continue;
                }

                _localLevelNetworkBridge = player;
                bridge = player;
                return true;
            }

            bridge = null;
            return false;
        }

        private bool HasTimerAuthority
        {
            get
            {
                NetworkManager networkManager = NetworkManager.Singleton;
                return networkManager == null ||
                       !networkManager.IsListening ||
                       networkManager.IsServer;
            }
        }

        private bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private void HandleNextLevelRequested()
        {
            if (_showBlockingOverlayInsteadOfNextLevel)
            {
                RequestAvailableContentEnd();
                return;
            }

            if (string.IsNullOrWhiteSpace(_nextLevelSceneName))
            {
                Debug.LogWarning($"[{nameof(LevelFlowController)}] Next level scene is not configured.", this);
                return;
            }

            RequestLevelLoad(_nextLevelSceneName);
        }

        private void RequestAvailableContentEnd()
        {
            if (!_levelUIController.HasBlockingOverlay)
            {
                Debug.LogError(
                    $"[{nameof(LevelFlowController)}] Blocking overlay is not configured.",
                    this);
                return;
            }

            if (!CanControlLevel)
            {
                Debug.LogWarning(
                    $"[{nameof(LevelFlowController)}] Only the host can finish the available content.",
                    this);
                return;
            }

            if (TryGetLevelNetworkBridge(out PlayerLevelNetworkController bridge))
            {
                bridge.ShowAvailableContentEndForAllPlayers();
                return;
            }

            if (!IsNetworkSessionActive)
            {
                HandleAvailableContentEndedReceived();
                return;
            }

            Debug.LogWarning(
                $"[{nameof(LevelFlowController)}] No server player is available to synchronize the blocking overlay.",
                this);
        }

        private void HandleAvailableContentEndedReceived()
        {
            if (!_isChangingScene)
            {
                _levelEnded = true;
                _levelUIController.ShowBlockingOverlay();
            }
        }

        private void HandleLeaveRequested()
        {
            LeaveSessionAndLoad(_leaveDestinationSceneName);
        }

        private void HandleLevelSelectionRequested()
        {
            LeaveSessionAndLoad(_levelSelectionSceneName);
        }

        private void HandleReplayAllRequested()
        {
            RequestLevelLoad(_campaignStartSceneName);
        }

        private void HandleMainMenuRequested()
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

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_isChangingScene || _networkManager == null)
            {
                return;
            }

            bool localServerDisconnected =
                _networkManager.IsServer && clientId == NetworkManager.ServerClientId;

            if (localServerDisconnected)
            {
                return;
            }

            if (_connectionFailureCoroutine != null)
            {
                return;
            }

            _connectionFailureCoroutine = StartCoroutine(HandleConnectionFailureCoroutine());
        }
        
        private IEnumerator HandleConnectionFailureCoroutine()
        {
            string destinationSceneName = string.IsNullOrWhiteSpace(_connectionFailureDestinationSceneName)
                ? "Lobby"
                : _connectionFailureDestinationSceneName;

            if (!Application.CanStreamedLevelBeLoaded(destinationSceneName))
            {
                Debug.LogError(
                    $"[{nameof(LevelFlowController)}] Scene '{destinationSceneName}' is not in Build Settings.",
                    this);
                _connectionFailureCoroutine = null;
                yield break;
            }

            _isChangingScene = true;
            _levelUIController.ShowConnectionLost(_connectionFailureMessage);

            float delay = Mathf.Max(0f, _connectionFailureRedirectDelay);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (this == null)
            {
                _connectionFailureCoroutine = null;
                yield break;
            }

            if (NetworkGameManager.Instance != null)
            {
                var task = NetworkGameManager.Instance.DisconnectAsync();
                yield return new WaitUntil(() => task.IsCompleted);
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(destinationSceneName);
            _connectionFailureCoroutine = null;
        }

        private void HandleConnectionLostAcknowledged()
        {
            if (_isChangingScene)
            {
                return;
            }

            if (_connectionFailureCoroutine != null)
            {
                StopCoroutine(_connectionFailureCoroutine);
                _connectionFailureCoroutine = null;
            }

            string destinationSceneName = string.IsNullOrWhiteSpace(_connectionFailureDestinationSceneName)
                ? "Lobby"
                : _connectionFailureDestinationSceneName;

            if (!Application.CanStreamedLevelBeLoaded(destinationSceneName))
            {
                Debug.LogError(
                    $"[{nameof(LevelFlowController)}] Scene '{destinationSceneName}' is not in Build Settings.",
                    this);
                return;
            }

            _isChangingScene = true;
            _levelUIController.ShowConnectionLost(_connectionFailureMessage);
            _connectionFailureCoroutine = StartCoroutine(DisconnectAndLoadSceneCoroutine(destinationSceneName));
        }

        private IEnumerator DisconnectAndLoadSceneCoroutine(string destinationSceneName)
        {
            if (NetworkGameManager.Instance != null)
            {
                var task = NetworkGameManager.Instance.DisconnectAsync();
                yield return new WaitUntil(() => task.IsCompleted);
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(destinationSceneName);
            _connectionFailureCoroutine = null;
        }

        private void HandleInputBlockChanged(bool isBlocked)
        {
            if (_localInput == null)
            {
                FindLocalInput();
            }

            ApplyCurrentInputState();
        }

        private void HandleLocalInputReady(SlimeInput slimeInput)
        {
            if (slimeInput == null || !slimeInput.IsLocalInputSource)
            {
                return;
            }

            _localInput = slimeInput;
            _localLevelNetworkBridge =
                slimeInput.GetComponent<PlayerLevelNetworkController>();
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
                    _localLevelNetworkBridge =
                        input.GetComponent<PlayerLevelNetworkController>();
                    return;
                }
            }
        }

        private void ApplyCurrentInputState()
        {
            SlimeInput[] inputs =
                FindObjectsByType<SlimeInput>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (SlimeInput input in inputs)
            {
                if (input == null || !input.IsLocalInputSource)
                {
                    continue;
                }

                input.SetInputBlocked(ShouldBlockLocalInput);
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
