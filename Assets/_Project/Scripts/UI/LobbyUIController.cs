using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splime.UI
{
    public sealed class LobbyUIController : MonoBehaviour
    {
        private enum LobbyView
        {
            Lobby,
            HostWaitingRoom,
            GuestWaitingRoom,
            SharedLobby
        }

        [Header("Panels")]
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private GameObject _hostWaitingRoomPanel;
        [SerializeField] private GameObject _guestWaitingRoomPanel;
        [SerializeField] private GameObject _sharedLobbyPanel;

        [Header("Lobby Buttons")]
        [SerializeField] private Button _hostGameButton;
        [SerializeField] private Button _joinGameButton;

        [Header("Join Code")]
        [SerializeField] private TMP_InputField _codeInputField;
        [SerializeField] private Button _joinButton;
        [SerializeField, Min(1)] private int _joinCodeLength = 6;
        [SerializeField] private string _allowedJoinCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Header("Shared Lobby")]
        [SerializeField] private Button _hostReadyButton;
        [SerializeField] private Button _guestReadyButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _hostReadyText;
        [SerializeField] private TMP_Text _guestReadyText;
        [SerializeField] private TMP_Text _roomCodeText;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private Graphic _hostReadyIndicator;
        [SerializeField] private Graphic _guestReadyIndicator;
        [SerializeField] private Color _readyColor = new Color(0.35f, 0.8f, 0.45f, 1f);
        [SerializeField] private Color _notReadyColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private bool _hideStartButtonForGuest = true;

        [Header("Ready Text")]
        [SerializeField] private string _readyActionText = "Ready";
        [SerializeField] private string _cancelReadyActionText = "Cancel Ready";
        [SerializeField] private string _notReadyStatusText = "Not Ready";
        [SerializeField] private string _readyStatusText = "Ready";

        private bool _hasActiveSession;
        private bool _isBusy;
        private bool _isLocalHost;
        private bool _localRoleAssigned;
        private bool _hostReady;
        private bool _guestReady;
        private bool _readyRequestPending;
        private int _connectedPlayerCount;
        private string _normalizedJoinCode = string.Empty;

        public string NormalizedJoinCode => _normalizedJoinCode;
        public bool IsJoinCodeValid => ValidateJoinCode(_normalizedJoinCode);
        public event Action HostRequested;
        public event Action<string> JoinRequested;
        public event Action<bool> ReadyChangeRequested;
        public event Action StartGameRequested;
        public event Action LeaveSessionRequested;
        public event Action BackToMainRequested;

        public bool CanStartGame =>
            _hasActiveSession &&
            _localRoleAssigned &&
            _isLocalHost &&
            _connectedPlayerCount >= 2 &&
            _hostReady &&
            _guestReady &&
            !_isBusy;

        private void Awake()
        {
            ConfigureJoinCodeInput();
            ResetLobbyState();
        }

        private void OnEnable()
        {
            if (_codeInputField != null)
            {
                _codeInputField.onValueChanged.AddListener(HandleJoinCodeChanged);
            }
        }

        private void OnDisable()
        {
            if (_codeInputField != null)
            {
                _codeInputField.onValueChanged.RemoveListener(HandleJoinCodeChanged);
            }
        }

        private void OnValidate()
        {
            _joinCodeLength = Mathf.Max(1, _joinCodeLength);
        }

        public void HandleJoinGameButtonPressed()
        {
            if (_isBusy || _hasActiveSession)
            {
                return;
            }

            ClearJoinCode();
            ClearFeedback();
            ShowView(LobbyView.GuestWaitingRoom);
        }

        public void HandleGuestBackButtonPressed()
        {
            if (_isBusy || _hasActiveSession)
            {
                return;
            }

            ClearJoinCode();
            ClearFeedback();
            ShowView(LobbyView.Lobby);
        }

        public void HandleLobbyBackButtonPressed()
        {
            if (_isBusy || _hasActiveSession)
            {
                return;
            }

            BackToMainRequested?.Invoke();
        }

        public void HandleHostGameButtonPressed()
        {
            if (_isBusy || _hasActiveSession)
            {
                return;
            }

            SetBusy(true, "Creating room...");
            HostRequested?.Invoke();
        }

        public void HandleJoinButtonPressed()
        {
            if (_isBusy || _hasActiveSession || !IsJoinCodeValid)
            {
                return;
            }

            SetBusy(true, "Joining room...");
            JoinRequested?.Invoke(_normalizedJoinCode);
        }

        public void HandleHostReadyButtonPressed()
        {
            Debug.Log($"[LobbyUIController] 🖱️ HandleHostReadyButtonPressed clickeado. LocalRoleAssigned: {_localRoleAssigned}, IsLocalHost: {_isLocalHost}, HostReady: {_hostReady}");
            if (!_localRoleAssigned || !_isLocalHost)
            {
                Debug.LogWarning("[LobbyUIController] ⚠️ Ignorando HostReady: no es host local o rol no asignado.");
                return;
            }

            RequestReadyChange(!_hostReady);
        }

        public void HandleGuestReadyButtonPressed()
        {
            Debug.Log($"[LobbyUIController] 🖱️ HandleGuestReadyButtonPressed clickeado. LocalRoleAssigned: {_localRoleAssigned}, IsLocalHost: {_isLocalHost}, GuestReady: {_guestReady}");
            if (!_localRoleAssigned || _isLocalHost)
            {
                Debug.LogWarning("[LobbyUIController] ⚠️ Ignorando GuestReady: es host local o rol no asignado.");
                return;
            }

            RequestReadyChange(!_guestReady);
        }

        public void HandleStartButtonPressed()
        {
            Debug.Log($"[LobbyUIController] 🎮 HandleStartButtonPressed clickeado. CanStartGame: {CanStartGame}");
            if (!CanStartGame)
            {
                return;
            }

            SetBusy(true, "Starting game...");
            StartGameRequested?.Invoke();
        }

        public void HandleLeaveButtonPressed()
        {
            RequestLeaveSession();
        }

        public void HandleHostBackButtonPressed()
        {
            RequestLeaveSession();
        }

        public void ShowHostWaitingRoom(string roomCode)
        {
            _hasActiveSession = true;
            _localRoleAssigned = true;
            _isLocalHost = true;
            _readyRequestPending = false;

            if (_roomCodeText != null)
            {
                _roomCodeText.text = roomCode ?? string.Empty;
            }

            SetBusy(false);
            ShowView(LobbyView.HostWaitingRoom);
        }

        public void ShowSharedLobbyAsHost()
        {
            ShowSharedLobby(true);
        }

        public void ShowSharedLobbyAsGuest()
        {
            ShowSharedLobby(false);
        }

        public void ShowSharedLobby(bool isLocalHost)
        {
            _hasActiveSession = true;
            _localRoleAssigned = true;
            _isLocalHost = isLocalHost;
            _readyRequestPending = false;
            SetBusy(false);
            ClearFeedback();
            ShowView(LobbyView.SharedLobby);
        }

        public void SetConnectedPlayerCount(int playerCount)
        {
            _connectedPlayerCount = Mathf.Max(0, playerCount);

            if (_connectedPlayerCount < 2)
            {
                _guestReady = false;
                _readyRequestPending = false;
            }

            RefreshControls();
        }

        public void SetReadyStates(bool hostReady, bool guestReady)
        {
            _hostReady = hostReady;
            _guestReady = guestReady;
            _readyRequestPending = false;
            RefreshControls();
        }

        public void SetBusy(bool isBusy)
        {
            SetBusy(isBusy, null);
        }

        public void ShowError(string message)
        {
            _readyRequestPending = false;
            SetBusy(false, message);
        }

        public void NotifySessionLeft()
        {
            ResetLobbyState();
        }

        private void RequestReadyChange(bool requestedReadyState)
        {
            Debug.Log($"[LobbyUIController] 📡 RequestReadyChange a: {requestedReadyState}. HasSession: {_hasActiveSession}, IsBusy: {_isBusy}, Pending: {_readyRequestPending}, PlayerCount: {_connectedPlayerCount}");
            if (!_hasActiveSession || _isBusy || _readyRequestPending || _connectedPlayerCount < 2)
            {
                Debug.LogWarning($"[LobbyUIController] ⚠️ RequestReadyChange bloqueado. PlayerCount < 2 o pendiente.");
                return;
            }

            _readyRequestPending = true;
            RefreshControls();
            ReadyChangeRequested?.Invoke(requestedReadyState);
        }

        private void RequestLeaveSession()
        {
            if (_isBusy)
            {
                return;
            }

            if (!_hasActiveSession)
            {
                ResetLobbyState();
                return;
            }

            SetBusy(true, "Leaving room...");
            LeaveSessionRequested?.Invoke();
        }

        private void ConfigureJoinCodeInput()
        {
            if (_codeInputField == null)
            {
                return;
            }

            _codeInputField.characterLimit = _joinCodeLength;
            _codeInputField.lineType = TMP_InputField.LineType.SingleLine;
            _codeInputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        }

        private void HandleJoinCodeChanged(string rawCode)
        {
            string normalizedCode = NormalizeJoinCode(rawCode);

            if (_codeInputField != null && !string.Equals(rawCode, normalizedCode, StringComparison.Ordinal))
            {
                _codeInputField.SetTextWithoutNotify(normalizedCode);
                _codeInputField.caretPosition = normalizedCode.Length;
            }

            _normalizedJoinCode = normalizedCode;
            RefreshControls();
        }

        private string NormalizeJoinCode(string rawCode)
        {
            if (string.IsNullOrWhiteSpace(rawCode))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(Mathf.Min(rawCode.Length, _joinCodeLength));

            foreach (char character in rawCode)
            {
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(character));

                if (builder.Length == _joinCodeLength)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private bool ValidateJoinCode(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode) || joinCode.Length != _joinCodeLength)
            {
                return false;
            }

            foreach (char character in joinCode)
            {
                if (_allowedJoinCodeCharacters.IndexOf(character) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearJoinCode()
        {
            _normalizedJoinCode = string.Empty;

            if (_codeInputField != null)
            {
                _codeInputField.SetTextWithoutNotify(string.Empty);
            }

            RefreshControls();
        }

        private void ResetLobbyState()
        {
            _hasActiveSession = false;
            _isBusy = false;
            _isLocalHost = false;
            _localRoleAssigned = false;
            _hostReady = false;
            _guestReady = false;
            _readyRequestPending = false;
            _connectedPlayerCount = 0;

            ClearJoinCode();
            ClearFeedback();
            ShowView(LobbyView.Lobby);
            RefreshControls();
        }

        private void SetBusy(bool isBusy, string feedbackMessage)
        {
            _isBusy = isBusy;

            if (feedbackMessage != null && _feedbackText != null)
            {
                _feedbackText.text = feedbackMessage;
            }

            RefreshControls();
        }

        private void ClearFeedback()
        {
            if (_feedbackText != null)
            {
                _feedbackText.text = string.Empty;
            }
        }

        private void ShowView(LobbyView view)
        {
            SetPanelActive(_lobbyPanel, view == LobbyView.Lobby);
            SetPanelActive(_hostWaitingRoomPanel, view == LobbyView.HostWaitingRoom);
            SetPanelActive(_guestWaitingRoomPanel, view == LobbyView.GuestWaitingRoom);
            SetPanelActive(_sharedLobbyPanel, view == LobbyView.SharedLobby);
        }

        private void RefreshControls()
        {
            bool canChooseSession = !_isBusy && !_hasActiveSession;
            SetButtonInteractable(_hostGameButton, canChooseSession);
            SetButtonInteractable(_joinGameButton, canChooseSession);
            SetButtonInteractable(_joinButton, canChooseSession && IsJoinCodeValid);

            bool canChangeReady =
                _hasActiveSession &&
                _localRoleAssigned &&
                _connectedPlayerCount >= 2 &&
                !_isBusy &&
                !_readyRequestPending;

            SetButtonInteractable(_hostReadyButton, canChangeReady && _isLocalHost);
            SetButtonInteractable(_guestReadyButton, canChangeReady && !_isLocalHost);
            SetButtonInteractable(_startButton, CanStartGame);

            if (_startButton != null && _hideStartButtonForGuest && _localRoleAssigned)
            {
                _startButton.gameObject.SetActive(_isLocalHost);
            }

            RefreshReadyPresentation();
        }

        private void RefreshReadyPresentation()
        {
            if (_hostReadyText != null)
            {
                _hostReadyText.text = GetReadyText(_hostReady, _localRoleAssigned && _isLocalHost);
            }

            if (_guestReadyText != null)
            {
                _guestReadyText.text = GetReadyText(_guestReady, _localRoleAssigned && !_isLocalHost);
            }

            if (_hostReadyIndicator != null)
            {
                _hostReadyIndicator.color = _hostReady ? _readyColor : _notReadyColor;
            }

            if (_guestReadyIndicator != null)
            {
                _guestReadyIndicator.color = _guestReady ? _readyColor : _notReadyColor;
            }
        }

        private string GetReadyText(bool isReady, bool isLocalPlayer)
        {
            if (isLocalPlayer)
            {
                return isReady ? _cancelReadyActionText : _readyActionText;
            }

            return isReady ? _readyStatusText : _notReadyStatusText;
        }

        private static void SetPanelActive(GameObject panel, bool isActive)
        {
            if (panel != null)
            {
                panel.SetActive(isActive);
            }
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
            {
                button.interactable = isInteractable;
            }
        }
    }
}
