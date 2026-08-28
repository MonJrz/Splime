using System;
using UnityEngine;

namespace Splime.UI
{
    public enum MainMenuView
    {
        Main,
        LevelSelection,
        Settings,
        HowToPlay,
        Credits
    }

    [DisallowMultipleComponent]
    public sealed class MainMenuUIController : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _levelSelectionPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _howToPlayPanel;
        [SerializeField] private GameObject _creditsPanel;

        [Header("Interaction")]
        [SerializeField] private CanvasGroup _interactionCanvasGroup;

        private MainMenuView _currentView = (MainMenuView)(-1);
        private bool _isBusy;

        public event Action PlayOnlineRequested;
        public event Action PlaySinglePlayerRequested;
        public event Action<int> LevelRequested;
        public event Action QuitRequested;
        public event Action<MainMenuView> ViewChanged;

        public MainMenuView CurrentView => _currentView;
        public bool IsBusy => _isBusy;

        private void Awake()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ShowMainMenu();
        }

        private void OnEnable()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void HandlePlayOnlineButtonPressed()
        {
            if (!_isBusy)
            {
                PlayOnlineRequested?.Invoke();
            }
        }

        public void HandlePlaySinglePlayerButtonPressed()
        {
            if (!_isBusy)
            {
                PlaySinglePlayerRequested?.Invoke();
            }
        }

        public void HandleLevelButtonPressed(int levelIndex)
        {
            if (!_isBusy && levelIndex >= 0)
            {
                LevelRequested?.Invoke(levelIndex);
            }
        }

        public void HandleQuitButtonPressed()
        {
            if (!_isBusy)
            {
                QuitRequested?.Invoke();
            }
        }

        public void ShowMainMenu()
        {
            SetView(MainMenuView.Main);
        }

        public void ShowLevelSelection()
        {
            SetView(MainMenuView.LevelSelection);
        }

        public void ShowSettings()
        {
            SetView(MainMenuView.Settings);
        }

        public void ShowHowToPlay()
        {
            SetView(MainMenuView.HowToPlay);
        }

        public void ShowCredits()
        {
            SetView(MainMenuView.Credits);
        }

        public void HandleBackButtonPressed()
        {
            if (!_isBusy && _currentView != MainMenuView.Main)
            {
                ShowMainMenu();
            }
        }

        public void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;

            if (_interactionCanvasGroup != null)
            {
                _interactionCanvasGroup.interactable = !isBusy;
                _interactionCanvasGroup.blocksRaycasts = !isBusy;
            }
        }

        private void SetView(MainMenuView view)
        {
            if (_currentView == view)
            {
                return;
            }

            _currentView = view;
            SetPanelActive(_mainMenuPanel, view == MainMenuView.Main);
            SetPanelActive(_levelSelectionPanel, view == MainMenuView.LevelSelection);
            SetPanelActive(_settingsPanel, view == MainMenuView.Settings);
            SetPanelActive(_howToPlayPanel, view == MainMenuView.HowToPlay);
            SetPanelActive(_creditsPanel, view == MainMenuView.Credits);
            ViewChanged?.Invoke(view);
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
