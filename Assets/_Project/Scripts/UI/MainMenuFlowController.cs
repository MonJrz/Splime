using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuFlowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MainMenuUIController _mainMenuUIController;

        [Header("Scene Flow")]
        [SerializeField] private string _lobbySceneName = "Lobby";
        [SerializeField] private string[] _levelSceneNames;

        private bool _isChangingScene;

        private void Awake()
        {
            if (_mainMenuUIController == null)
            {
                _mainMenuUIController = GetComponent<MainMenuUIController>();
            }
        }

        private void OnEnable()
        {
            if (_mainMenuUIController == null)
            {
                Debug.LogError(
                    $"[{nameof(MainMenuFlowController)}] MainMenuUIController reference is missing.",
                    this);
                enabled = false;
                return;
            }

            _mainMenuUIController.PlayOnlineRequested += HandlePlayOnlineRequested;
            _mainMenuUIController.PlaySinglePlayerRequested += HandlePlaySinglePlayerRequested;
            _mainMenuUIController.LevelRequested += HandleLevelRequested;
            _mainMenuUIController.QuitRequested += HandleQuitRequested;
        }

        private void OnDisable()
        {
            if (_mainMenuUIController == null)
            {
                return;
            }

            _mainMenuUIController.PlayOnlineRequested -= HandlePlayOnlineRequested;
            _mainMenuUIController.PlaySinglePlayerRequested -= HandlePlaySinglePlayerRequested;
            _mainMenuUIController.LevelRequested -= HandleLevelRequested;
            _mainMenuUIController.QuitRequested -= HandleQuitRequested;
        }

        private void HandlePlayOnlineRequested()
        {
            LoadScene(_lobbySceneName);
        }

        private void HandlePlaySinglePlayerRequested()
        {
            string firstLevelSceneName =
                _levelSceneNames != null && _levelSceneNames.Length > 0
                    ? _levelSceneNames[0]
                    : "Level1";

            LoadScene(firstLevelSceneName, showHowToPlayOnStart: true);
        }

        private void HandleLevelRequested(int levelIndex)
        {
            if (_levelSceneNames == null ||
                levelIndex < 0 ||
                levelIndex >= _levelSceneNames.Length)
            {
                Debug.LogWarning(
                    $"[{nameof(MainMenuFlowController)}] Level index {levelIndex} is not configured.",
                    this);
                return;
            }

            LoadScene(_levelSceneNames[levelIndex]);
        }

        private void HandleQuitRequested()
        {
            if (_isChangingScene)
            {
                return;
            }

            Application.Quit();
        }

        private void LoadScene(
            string sceneName,
            bool showHowToPlayOnStart = false)
        {
            if (_isChangingScene)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[{nameof(MainMenuFlowController)}] Scene '{sceneName}' is not in Build Settings.",
                    this);
                return;
            }

            _isChangingScene = true;
            _mainMenuUIController.SetBusy(true);

            if (showHowToPlayOnStart)
            {
                HowToPlayStartupRequest.ScheduleFor(sceneName);
            }

            SceneManager.LoadScene(sceneName);
        }
    }

    internal static class HowToPlayStartupRequest
    {
        private static string _pendingSceneName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _pendingSceneName = null;
        }

        public static void ScheduleFor(string sceneName)
        {
            _pendingSceneName = sceneName;
        }

        public static bool ConsumeFor(string sceneName)
        {
            if (!string.Equals(
                    _pendingSceneName,
                    sceneName,
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            _pendingSceneName = null;
            return true;
        }
    }
}
