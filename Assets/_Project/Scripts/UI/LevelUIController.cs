using System;
using System.Collections;
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
        LevelComplete,
        LevelFailed
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

        [Header("Reusable Views")]
        [SerializeField] private PagedContentUIController _dialogueController;
        [SerializeField] private PagedContentUIController _tutorialController;
        [SerializeField] private InteractionPromptUIController _interactionPromptController;
        [SerializeField] private GameObject _checkpointPanel;

        [Header("Local Presentation")]
        [SerializeField] private bool _manageCursor;

        private LevelUIView _currentView = (LevelUIView)(-1);
        private LevelUIView _returnView = LevelUIView.Paused;
        private Coroutine _checkpointRoutine;
        private bool _initialCursorVisible;
        private CursorLockMode _initialCursorLockMode;

        public event Action RestartRequested;
        public event Action LeaveSessionRequested;
        public event Action NextLevelRequested;
        public event Action LevelSelectionRequested;
        public event Action<LevelUIView> ViewChanged;
        public event Action<bool> InputBlockChanged;

        public LevelUIView CurrentView => _currentView;
        public bool IsInputBlocked => _currentView != LevelUIView.Gameplay;

        private void Awake()
        {
            _initialCursorVisible = Cursor.visible;
            _initialCursorLockMode = Cursor.lockState;
            SetCheckpointVisible(false);
            ShowGameplay();
        }

        private void OnEnable()
        {
            if (_dialogueController != null)
            {
                _dialogueController.Completed += HandleDialogueCompleted;
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
        }

        private void OnDestroy()
        {
            if (_manageCursor)
            {
                Cursor.visible = _initialCursorVisible;
                Cursor.lockState = _initialCursorLockMode;
            }
        }

        public void HandlePauseButtonPressed()
        {
            if (_currentView == LevelUIView.Gameplay)
            {
                SetView(LevelUIView.Paused);
            }
        }

        public void HandleResumeButtonPressed()
        {
            if (_currentView == LevelUIView.Paused)
            {
                ShowGameplay();
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

        public void ShowGameplay()
        {
            SetView(LevelUIView.Gameplay);
        }

        public void ShowLevelComplete()
        {
            SetView(LevelUIView.LevelComplete);
        }

        public void ShowLevelFailed()
        {
            SetView(LevelUIView.LevelFailed);
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

        public void ShowTutorial(UIMessageSequence sequence)
        {
            if (!CanOpenSequence(sequence) || _tutorialController == null)
            {
                return;
            }

            SetView(LevelUIView.Tutorial);
            _tutorialController.Show(sequence);
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

            if (view != LevelUIView.Dialogue)
            {
                _dialogueController?.Hide();
            }

            if (view != LevelUIView.Tutorial)
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

            Cursor.visible = IsInputBlocked;
            Cursor.lockState = IsInputBlocked ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private static void SetPanelActive(GameObject panel, bool isActive)
        {
            if (panel != null)
            {
                panel.SetActive(isActive);
            }
        }
    }
}
