using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace Splime.UI
{
    public enum LevelUIView
    {
        Gameplay,
        Paused,
        Settings,
        LeaveConfirmation,
        Dialogue,
        Tutorial,
        HowToPlay,
        LevelComplete,
        LevelFailed,
        ConnectionLost,
        BlockingOverlay
    }

    [DisallowMultipleComponent]
    public sealed class LevelUIController : MonoBehaviour
    {
        [Header("Main Views")]
        [SerializeField] private GameObject _inGamePanel;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _leaveConfirmationPanel;
        [SerializeField] private GameObject _levelCompletePanel;
        [SerializeField] private GameObject _levelFailedPanel;

        [Header("Gameplay HUD")]
        [SerializeField] private TMP_Text _timerText;

        [Header("Level Complete")]
        [SerializeField] private TMP_Text _completionTimeText;

        [Header("Blocking Overlay")]
        [SerializeField] private GameObject _blockingOverlayPanel;
        [SerializeField] private bool _showBlockingOverlayOnStart;

        [Header("Connection Feedback")]
        [SerializeField] private GameObject _connectionLostPanel;
        [SerializeField] private TMP_Text _connectionLostText;
        [SerializeField] private Button _connectionLostReturnButton;

        [Header("Reusable Views")]
        [SerializeField] private PagedContentUIController _dialogueController;
        [SerializeField] private PagedContentUIController _tutorialController;
        [SerializeField] private InteractionPromptUIController _interactionPromptController;
        [SerializeField] private GameObject _checkpointPanel;

        [Header("Local Presentation")]
        [SerializeField] private bool _manageCursor = true;

        private LevelUIView _currentView = (LevelUIView)(-1);
        private LevelUIView _returnView = LevelUIView.Paused;
        private LevelUIView _viewBeforePause = LevelUIView.Gameplay;
        private Coroutine _checkpointRoutine;

        public event Action RestartRequested;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action LeaveSessionRequested;
        public event Action ConnectionLostAcknowledged;
        public event Action NextLevelRequested;
        public event Action LevelSelectionRequested;
        public event Action ReplayAllRequested;
        public event Action MainMenuRequested;
        public event Action<LevelUIView> ViewChanged;
        public event Action<bool> InputBlockChanged;
        public event Action<int> DialogueAdvanceRequested;
        public event Action DialogueSkipRequested;

        public LevelUIView CurrentView => _currentView;
        public bool IsInputBlocked =>
            _currentView != LevelUIView.Gameplay &&
            _currentView != LevelUIView.Tutorial;
        public bool HasBlockingOverlay => _blockingOverlayPanel != null;
        public bool IsBlockingOverlayVisible => _currentView == LevelUIView.BlockingOverlay;
        public bool IsLevelTimerPaused =>
            _currentView == LevelUIView.Paused ||
            _currentView == LevelUIView.Dialogue ||
            _currentView == LevelUIView.HowToPlay ||
            IsBlockingOverlayVisible;
        private bool ShouldShowCursor => _currentView != LevelUIView.Gameplay;

        private void Awake()
        {
            SetCheckpointVisible(false);

            if (_showBlockingOverlayOnStart && _blockingOverlayPanel != null)
            {
                ShowBlockingOverlay();
            }
            else
            {
                ShowGameplay();
            }
        }

        private void OnEnable()
        {
            if (_dialogueController != null)
            {
                _dialogueController.Completed += HandleDialogueCompleted;
                _dialogueController.AdvanceRequested += HandleDialogueAdvanceRequested;
                _dialogueController.SkipRequested += HandleDialogueSkipRequested;
            }

            if (_connectionLostReturnButton == null && _connectionLostPanel != null)
            {
                _connectionLostReturnButton = _connectionLostPanel.GetComponentInChildren<Button>(true);
            }

            if (_connectionLostReturnButton != null)
            {
                _connectionLostReturnButton.onClick.AddListener(HandleConnectionLostReturnButtonPressed);
            }

            if (_tutorialController != null)
            {
                _tutorialController.Completed += HandleTutorialCompleted;
            }
        }

        private void OnDisable()
        {
            if (_dialogueController != null)
            {
                _dialogueController.Completed -= HandleDialogueCompleted;
                _dialogueController.AdvanceRequested -= HandleDialogueAdvanceRequested;
                _dialogueController.SkipRequested -= HandleDialogueSkipRequested;
            }

            if (_tutorialController != null)
            {
                _tutorialController.Completed -= HandleTutorialCompleted;
            }

            if (_checkpointRoutine != null)
            {
                StopCoroutine(_checkpointRoutine);
                _checkpointRoutine = null;
            }

            if (_connectionLostReturnButton != null)
            {
                _connectionLostReturnButton.onClick.RemoveListener(HandleConnectionLostReturnButtonPressed);
            }

            if (_manageCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnDestroy()
        {
            if (_manageCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void Update()
        {
            if (_manageCursor && !ShouldShowCursor)
            {
                // Re-lock cursor if player clicks back into the window during active gameplay
                if (Cursor.lockState != CursorLockMode.Locked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                {
                    UpdateCursor();
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _manageCursor)
            {
                UpdateCursor();
            }
        }

        public void HandlePauseButtonPressed()
        {
            if (_currentView == LevelUIView.Gameplay ||
                _currentView == LevelUIView.Tutorial)
            {
                PauseRequested?.Invoke();
            }
        }

        public void HandleResumeButtonPressed()
        {
            if (_currentView == LevelUIView.Paused)
            {
                ResumeRequested?.Invoke();
            }
        }

        public void HandleSettingsButtonPressed()
        {
            if (_currentView != LevelUIView.Paused)
            {
                return;
            }

            _returnView = _currentView;
            SetView(LevelUIView.Settings);
        }

        public void HandleSettingsBackButtonPressed()
        {
            if (_currentView == LevelUIView.Settings)
            {
                SetView(_returnView);
            }
        }

        public void HandleLeaveButtonPressed()
        {
            if (_currentView == LevelUIView.Gameplay ||
                _currentView == LevelUIView.Dialogue ||
                _currentView == LevelUIView.Tutorial)
            {
                return;
            }

            _returnView = _currentView;
            SetView(LevelUIView.LeaveConfirmation);
        }

        public void HandleCancelLeaveButtonPressed()
        {
            if (_currentView == LevelUIView.LeaveConfirmation)
            {
                SetView(_returnView);
            }
        }

        public void HandleConfirmLeaveButtonPressed()
        {
            if (_currentView == LevelUIView.LeaveConfirmation)
            {
                LeaveSessionRequested?.Invoke();
            }
        }

        public void HandleRestartButtonPressed()
        {
            RestartRequested?.Invoke();
        }

        public void HandleConnectionLostReturnButtonPressed()
        {
            ConnectionLostAcknowledged?.Invoke();
        }

        public void HandleReplayButtonPressed()
        {
            RestartRequested?.Invoke();
        }

        public void HandleNextLevelButtonPressed()
        {
            if (_currentView == LevelUIView.LevelComplete)
            {
                NextLevelRequested?.Invoke();
            }
        }

        public void HandleLevelSelectionButtonPressed()
        {
            if (_currentView == LevelUIView.LevelComplete || _currentView == LevelUIView.LevelFailed)
            {
                LevelSelectionRequested?.Invoke();
            }
        }

        public void HandleReplayAllButtonPressed()
        {
            ReplayAllRequested?.Invoke();
        }

        public void HandleMainMenuButtonPressed()
        {
            MainMenuRequested?.Invoke();
        }

        public void ShowGameplay()
        {
            SetView(LevelUIView.Gameplay);
        }

        public void ShowPause()
        {
            if (_currentView == LevelUIView.Gameplay ||
                _currentView == LevelUIView.Tutorial)
            {
                _viewBeforePause = _currentView;
            }

            SetView(LevelUIView.Paused);
        }

        public void RestoreViewAfterPause()
        {
            if (!IsPauseFlowView(_currentView))
            {
                return;
            }

            bool canRestoreTutorial =
                _viewBeforePause == LevelUIView.Tutorial &&
                _tutorialController != null &&
                _tutorialController.HasActiveSequence;

            _viewBeforePause = LevelUIView.Gameplay;
            SetView(canRestoreTutorial ? LevelUIView.Tutorial : LevelUIView.Gameplay);
        }

        public void ShowBlockingOverlay()
        {
            if (_blockingOverlayPanel == null)
            {
                Debug.LogWarning(
                    $"[{nameof(LevelUIController)}] Blocking overlay panel reference is missing.",
                    this);
                return;
            }

            SetView(LevelUIView.BlockingOverlay);
        }

        public void ShowLevelComplete(int elapsedSeconds)
        {
            SetFormattedTime(_completionTimeText, elapsedSeconds);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayVictory();
            }

            SetView(LevelUIView.LevelComplete);
        }

        public void ShowLevelFailed()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDefeat();
            }

            SetView(LevelUIView.LevelFailed);
        }

        public void SetRemainingTime(int totalSeconds)
        {
            SetFormattedTime(_timerText, totalSeconds);
        }

        public void ShowConnectionLost(string message)
        {
            if (_connectionLostText != null)
            {
                _connectionLostText.text = message ?? string.Empty;
            }

            SetView(LevelUIView.ConnectionLost);
        }

        public void ShowDialogue(UIMessageSequence sequence)
        {
            if (!CanOpenSequence(sequence) || _dialogueController == null)
            {
                return;
            }

            SetView(LevelUIView.Dialogue);
            _dialogueController.Show(sequence);
        }

        public bool ShowSynchronizedDialoguePage(
            UIMessageSequence sequence,
            int pageIndex)
        {
            bool canShow =
                (_currentView == LevelUIView.Gameplay ||
                 _currentView == LevelUIView.Dialogue) &&
                sequence != null &&
                pageIndex >= 0 &&
                pageIndex < sequence.PageCount &&
                _dialogueController != null;

            if (!canShow)
            {
                return false;
            }

            SetView(LevelUIView.Dialogue);
            _dialogueController.ShowExternallyControlled(sequence, pageIndex);
            return _dialogueController.IsOpen;
        }

        public void CompleteSynchronizedDialogue()
        {
            _dialogueController?.CompleteExternallyControlledSequence();
        }

        public void ShowTutorial(UIMessageSequence sequence)
        {
            if (!CanOpenSequence(sequence) || _tutorialController == null)
            {
                return;
            }

            SetView(LevelUIView.Tutorial);
            _tutorialController.Show(sequence);
        }

        public void ShowHowToPlay()
        {
            SetView(LevelUIView.HowToPlay);
        }

        public void CompleteHowToPlay()
        {
            if (_currentView == LevelUIView.HowToPlay)
            {
                ShowGameplay();
            }
        }

        public void DismissTutorial(UIMessageSequence sequence)
        {
            if (_tutorialController == null ||
                !_tutorialController.HasActiveSequenceFor(sequence))
            {
                return;
            }

            _tutorialController.Hide();

            if (_viewBeforePause == LevelUIView.Tutorial)
            {
                _viewBeforePause = LevelUIView.Gameplay;
            }

            if (_currentView == LevelUIView.Tutorial)
            {
                ShowGameplay();
            }
        }

        public void ShowInteractionPrompt(string message)
        {
            if (_currentView == LevelUIView.Gameplay)
            {
                _interactionPromptController?.Show(message);
            }
        }

        public void HideInteractionPrompt()
        {
            _interactionPromptController?.Hide();
        }

        public void ShowCheckpoint(float duration = 2f)
        {
            if (_checkpointRoutine != null)
            {
                StopCoroutine(_checkpointRoutine);
            }

            _checkpointRoutine = StartCoroutine(ShowCheckpointTemporarily(Mathf.Max(0f, duration)));
        }

        public void HideCheckpoint()
        {
            if (_checkpointRoutine != null)
            {
                StopCoroutine(_checkpointRoutine);
                _checkpointRoutine = null;
            }

            SetCheckpointVisible(false);
        }

        private bool CanOpenSequence(UIMessageSequence sequence)
        {
            return _currentView == LevelUIView.Gameplay && sequence != null && sequence.PageCount > 0;
        }

        private void HandleDialogueCompleted()
        {
            if (_currentView == LevelUIView.Dialogue)
            {
                ShowGameplay();
            }
        }

        private void HandleDialogueAdvanceRequested(int pageIndex)
        {
            DialogueAdvanceRequested?.Invoke(pageIndex);
        }

        private void HandleDialogueSkipRequested()
        {
            DialogueSkipRequested?.Invoke();
        }

        private void HandleTutorialCompleted()
        {
            if (_currentView == LevelUIView.Tutorial)
            {
                ShowGameplay();
            }
        }

        private void SetView(LevelUIView view)
        {
            if (_currentView == view)
            {
                return;
            }

            _currentView = view;
            bool showGameplayHud =
                view == LevelUIView.Gameplay ||
                view == LevelUIView.Dialogue ||
                view == LevelUIView.Tutorial;

            SetPanelActive(_inGamePanel, showGameplayHud);
            SetPanelActive(_pausePanel, view == LevelUIView.Paused);
            SetPanelActive(_settingsPanel, view == LevelUIView.Settings);
            SetPanelActive(_leaveConfirmationPanel, view == LevelUIView.LeaveConfirmation);
            SetPanelActive(_levelCompletePanel, view == LevelUIView.LevelComplete);
            SetPanelActive(_levelFailedPanel, view == LevelUIView.LevelFailed);
            SetPanelActive(_connectionLostPanel, view == LevelUIView.ConnectionLost);
            SetPanelActive(_blockingOverlayPanel, view == LevelUIView.BlockingOverlay);

            if (view != LevelUIView.Dialogue)
            {
                _dialogueController?.Hide();
            }

            bool preserveTutorial =
                _viewBeforePause == LevelUIView.Tutorial &&
                IsPauseFlowView(view);

            if (view == LevelUIView.Tutorial)
            {
                _tutorialController?.Resume();
            }
            else if (preserveTutorial)
            {
                _tutorialController?.Suspend();
            }
            else
            {
                _tutorialController?.Hide();
            }

            if (view != LevelUIView.Gameplay)
            {
                _interactionPromptController?.Hide();
            }

            UpdateCursor();
            ViewChanged?.Invoke(view);
            InputBlockChanged?.Invoke(IsInputBlocked);
        }

        private static bool IsPauseFlowView(LevelUIView view)
        {
            return view == LevelUIView.Paused ||
                   view == LevelUIView.Settings ||
                   view == LevelUIView.LeaveConfirmation;
        }

        private IEnumerator ShowCheckpointTemporarily(float duration)
        {
            SetCheckpointVisible(true);

            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
            }

            SetCheckpointVisible(false);
            _checkpointRoutine = null;
        }

        private void SetCheckpointVisible(bool isVisible)
        {
            SetPanelActive(_checkpointPanel, isVisible);
        }

        private void UpdateCursor()
        {
            if (!_manageCursor)
            {
                return;
            }

            Cursor.visible = ShouldShowCursor;
            Cursor.lockState = ShouldShowCursor ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private static void SetPanelActive(GameObject panel, bool isActive)
        {
            if (panel != null)
            {
                panel.SetActive(isActive);
            }
        }

        private static void SetFormattedTime(TMP_Text target, int totalSeconds)
        {
            if (target == null)
            {
                return;
            }

            int clampedSeconds = Mathf.Max(0, totalSeconds);
            int minutes = clampedSeconds / 60;
            int seconds = clampedSeconds % 60;
            target.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
