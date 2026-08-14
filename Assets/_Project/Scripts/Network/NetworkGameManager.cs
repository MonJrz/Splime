using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Splime.Core;
using Splime.Player;
using Splime.UI;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splime.Network
{
    /// <summary>
    /// Gestor de Red Central para Splime.
    /// Hereda de MonoBehaviour para evitar la restricción de Netcode que prohíbe
    /// combinar NetworkBehaviour con NetworkManager en el mismo GameObject.
    /// Asigna autoritativamente los roles y puntos de Spawn a los 2 jugadores:
    /// - Jugador 1 (Host / Client 0): Slime Transformador (Slime 1)
    /// - Jugador 2 (Client 1): Slime Ágil (Slime 2)
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        private enum SessionRole
        {
            None,
            Host,
            Guest
        }

        private enum SessionFlowState
        {
            Idle,
            CreatingHost,
            HostWaiting,
            JoiningGuest,
            Synchronizing,
            SharedLobby,
            Leaving,
            Recovering
        }

        private const string ReadyPropertyKey = "ready";
        private const string ReadyValue = "1";

        public static NetworkGameManager Instance { get; private set; }

        [Header("Player Prefabs")]
        [SerializeField] private GameObject _slimeTransformerPrefab;
        [SerializeField] private GameObject _slimeAgilePrefab;

        [Header("Slime Data Assets")]
        [SerializeField] private SlimeData _transformerData;
        [SerializeField] private SlimeData _agileData;

        [Header("Spawn Locations")]
        [SerializeField] private Vector3 _player1SpawnPosition = new Vector3(-2f, 1f, 0f);
        [SerializeField] private Vector3 _player2SpawnPosition = new Vector3(2f, 1f, 0f);

        [Header("Lobby Flow")]
        [SerializeField] private LobbyUIController _lobbyUIController;
        [SerializeField] private string _mainMenuSceneName = "Main";
        [SerializeField] private string _gameplaySceneName = "SceneTest";

        private readonly Dictionary<ulong, GameObject> _spawnedPlayers = new Dictionary<ulong, GameObject>();
        private Task<bool> _servicesInitializationTask;
        private ISession _currentSession;
        private string _joinCode = string.Empty;
        private bool _isInitialized;
        private bool _isConnecting;
        private bool _isCleaningUp;
        private int _sessionOperationVersion;
        private Task _cleanupTask;
        private SessionRole _sessionRole;
        private SessionFlowState _sessionFlowState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            LobbyUIController configuredLobbyUI = _lobbyUIController;
            _lobbyUIController = null;
            BindLobbyUI(configuredLobbyUI);
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            _ = InitializeServicesAsync();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindLobbyUI();
            UnsubscribeFromSessionEvents(_currentSession);
            UnsubscribeFromNetworkSceneEvents();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            Instance = null;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                IsLevelSceneActive())
            {
                SpawnPlayerForClient(clientId);
            }

            RefreshLobbyUI();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager != null &&
                networkManager.IsServer &&
                _spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
            {
                if (playerObj != null)
                {
                    NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }
                }
                _spawnedPlayers.Remove(clientId);
            }

            if (_isCleaningUp || IsLevelSceneActive())
            {
                return;
            }

            if (networkManager != null && networkManager.IsServer)
            {
                bool wasInSharedLobby = _sessionFlowState == SessionFlowState.SharedLobby;
                RefreshLobbyUI();

                if (wasInSharedLobby && _currentSession != null)
                {
                    _lobbyUIController?.ShowHostWaitingRoomWithWarning(
                        _joinCode,
                        "The guest disconnected. You can wait for them to join again.");
                }

                return;
            }

            if (_currentSession != null)
            {
                _ = RecoverFromConnectionFailureAsync(
                    "Connection to the host was lost. Please try joining again.");
            }
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (_spawnedPlayers.ContainsKey(clientId))
            {
                return;
            }

            GameObject prefabToSpawn = (clientId == 0) ? _slimeTransformerPrefab : _slimeAgilePrefab;
            Vector3 spawnPos = (clientId == 0) ? _player1SpawnPosition : _player2SpawnPosition;
            SlimeData dataToAssign = (clientId == 0) ? _transformerData : _agileData;

            if (prefabToSpawn == null)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] Prefab de jugador no asignado en el Inspector para clientId {clientId}.", this);
                return;
            }

            GameObject playerInstance = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.SpawnWithOwnership(clientId);
                _spawnedPlayers[clientId] = playerInstance;

                SlimeMovement movement = playerInstance.GetComponent<SlimeMovement>();
                if (movement != null && dataToAssign != null)
                {
                    movement.InitializeData(dataToAssign);
                }

                SlimeJump jump = playerInstance.GetComponent<SlimeJump>();
                if (jump != null && dataToAssign != null)
                {
                    jump.InitializeData(dataToAssign);
                }

                // Inicializar el SlimeStatsModifier con los datos base del Slime
                // Debe inicializarse DESPUÉS de Movement y Jump para que estén listos cuando lean de él
                SlimeStatsModifier statsModifier = playerInstance.GetComponent<SlimeStatsModifier>();
                if (statsModifier != null && dataToAssign != null)
                {
                    statsModifier.Initialize(dataToAssign);
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎮 Jugador {clientId + 1} ({prefabToSpawn.name}) instanciado exitosamente en {spawnPos} con Ownership para clientId {clientId}.", this);
            }
        }

        /// <summary>
        /// Inicializa Unity Gaming Services y autentica al jugador de forma anónima.
        /// Si el jugador ya tiene un token cacheado, reutiliza la sesión sin volver a autenticar.
        /// Garantiza que los servicios solo se inicializan una vez por sesión de juego.
        /// </summary>
        private async Task<bool> InitializeServicesAsync()
        {
            if (_isInitialized)
            {
                return true;
            }

            _servicesInitializationTask ??= InitializeServicesInternalAsync();
            bool initialized = await _servicesInitializationTask;

            if (!initialized)
            {
                _servicesInitializationTask = null;
            }

            return initialized;
        }

        private async Task<bool> InitializeServicesInternalAsync()
        {
            try
            {
                Debug.Log($"[{nameof(NetworkGameManager)}] 🔄 Inicializando Unity Gaming Services...");
                await UnityServices.InitializeAsync();

                // Si ya tiene token cacheado de una sesión anterior, no re-autenticamos
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Autenticado. PlayerID: {AuthenticationService.Instance.PlayerId}");
                _isInitialized = true;
                return true;
            }
            catch (AuthenticationException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error de autenticación: {e.Message}");
                return false;
            }
            catch (RequestFailedException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al inicializar UGS (posiblemente Project ID no vinculado): {e.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado en InitializeServices: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Paso 3 & 4: Crea una sesión multiplayer con Unity Relay para 2 jugadores como Host.
        /// Obtiene y almacena el Join Code generado por la nube.
        /// </summary>
        public async Task StartHostWithRelayAsync()
        {
            if (_isConnecting || _isCleaningUp || _currentSession != null)
            {
                return;
            }

            int operationVersion = ++_sessionOperationVersion;
            _isConnecting = true;
            _sessionRole = SessionRole.Host;
            _sessionFlowState = SessionFlowState.CreatingHost;

            try
            {
                bool initialized = await InitializeServicesAsync();
                if (!initialized)
                {
                    await HandleConnectionAttemptFailedAsync(
                        operationVersion,
                        SessionRole.Host,
                        "Could not connect to Unity services. Please try again.");
                    return;
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 Creando Sesión de Relay (MaxPlayers = 2)...");

                var options = new SessionOptions
                {
                    MaxPlayers = 2
                }.WithRelayNetwork();

                ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);

                if (operationVersion != _sessionOperationVersion)
                {
                    await CloseSessionSafelyAsync(session);
                    return;
                }

                SetCurrentSession(session);
                _sessionFlowState = SessionFlowState.HostWaiting;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡HOST CREADO EXITOSAMENTE!");
                Debug.Log($"[{nameof(NetworkGameManager)}] 🔑 JOIN CODE: {_joinCode}");
                _lobbyUIController?.ShowHostWaitingRoom(_joinCode);
                RefreshLobbyUI();
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al crear la sesión: {e.Message}");
                await HandleConnectionAttemptFailedAsync(
                    operationVersion,
                    SessionRole.Host,
                    "Could not create the room. Please try again.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al iniciar Host: {e.Message}");
                await HandleConnectionAttemptFailedAsync(
                    operationVersion,
                    SessionRole.Host,
                    "An unexpected connection error occurred. Please try again.");
            }
            finally
            {
                if (operationVersion == _sessionOperationVersion)
                {
                    _isConnecting = false;
                }
            }
        }

        /// <summary>
        /// Paso 5: Se une a una sesión multiplayer existente en Relay mediante su Join Code.
        /// </summary>
        public async Task JoinSessionWithRelayAsync(string codeToJoin)
        {
            if (_isConnecting || _isCleaningUp || _currentSession != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(codeToJoin))
            {
                Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ Debes ingresar un Join Code válido para conectarte.");
                ShowLobbyError("Ingresa un código de sala válido.");
                return;
            }

            int operationVersion = ++_sessionOperationVersion;
            _isConnecting = true;
            _sessionRole = SessionRole.Guest;
            _sessionFlowState = SessionFlowState.JoiningGuest;

            try
            {
                bool initialized = await InitializeServicesAsync();
                if (!initialized)
                {
                    await HandleConnectionAttemptFailedAsync(
                        operationVersion,
                        SessionRole.Guest,
                        "Could not connect to Unity services. Please try again.");
                    return;
                }

                string formattedCode = codeToJoin.Trim().ToUpper();
                Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 Conectándose a la sesión con Join Code: {formattedCode}...");

                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(formattedCode);

                if (operationVersion != _sessionOperationVersion)
                {
                    await CloseSessionSafelyAsync(session);
                    return;
                }

                SetCurrentSession(session);
                _sessionFlowState = SessionFlowState.Synchronizing;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡CONEXIÓN COMO CLIENTE EXITOSA!");
                RefreshLobbyUI();
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al unirse a la sesión: {e.Message}");
                await HandleConnectionAttemptFailedAsync(
                    operationVersion,
                    SessionRole.Guest,
                    "Could not join the room. Check the code and try again.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al unirse como Cliente: {e.Message}");
                await HandleConnectionAttemptFailedAsync(
                    operationVersion,
                    SessionRole.Guest,
                    "An unexpected connection error occurred. Please try again.");
            }
            finally
            {
                if (operationVersion == _sessionOperationVersion)
                {
                    _isConnecting = false;
                }
            }
        }

        /// <summary>
        /// Paso 9: Realiza la desconexión ordenada cerrando la sesión de UGS en la nube y apagando NGO.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_cleanupTask != null)
            {
                await _cleanupTask;
                return;
            }

            _cleanupTask = DisconnectInternalAsync();

            try
            {
                await _cleanupTask;
            }
            finally
            {
                _cleanupTask = null;
            }
        }

        private async Task DisconnectInternalAsync()
        {
            _isCleaningUp = true;
            _isConnecting = false;
            _sessionFlowState = SessionFlowState.Leaving;
            ++_sessionOperationVersion;
            Debug.Log($"[{nameof(NetworkGameManager)}] 🚪 Iniciando proceso de desconexión...");

            ISession session = _currentSession;
            _currentSession = null;
            UnsubscribeFromSessionEvents(session);
            UnsubscribeFromNetworkSceneEvents();

            try
            {
                await CloseSessionSafelyAsync(session);
            }
            finally
            {
                ShutdownNetwork();
                ResetSessionRuntimeState();
                _lobbyUIController?.NotifySessionLeft();
                _isCleaningUp = false;
            }
        }

        private async Task HandleConnectionAttemptFailedAsync(
            int operationVersion,
            SessionRole attemptedRole,
            string message)
        {
            if (operationVersion != _sessionOperationVersion)
            {
                return;
            }

            _isCleaningUp = true;
            _isConnecting = false;
            _sessionFlowState = SessionFlowState.Recovering;
            ++_sessionOperationVersion;

            ISession session = _currentSession;
            _currentSession = null;
            UnsubscribeFromSessionEvents(session);
            UnsubscribeFromNetworkSceneEvents();

            try
            {
                await CloseSessionSafelyAsync(session);
            }
            finally
            {
                ShutdownNetwork();
                ResetSessionRuntimeState();

                if (attemptedRole == SessionRole.Guest)
                {
                    _lobbyUIController?.ShowGuestWaitingRoomWithWarning(message);
                }
                else
                {
                    _lobbyUIController?.ShowLobbyWithWarning(message);
                }

                _isCleaningUp = false;
            }
        }

        private async Task RecoverFromConnectionFailureAsync(string message)
        {
            if (_isCleaningUp)
            {
                return;
            }

            SessionRole disconnectedRole = _sessionRole;
            _sessionFlowState = SessionFlowState.Recovering;
            await DisconnectAsync();

            if (_lobbyUIController == null)
            {
                return;
            }

            if (disconnectedRole == SessionRole.Guest)
            {
                _lobbyUIController.ShowGuestWaitingRoomWithWarning(message);
            }
            else
            {
                _lobbyUIController.ShowLobbyWithWarning(message);
            }
        }

        private static async Task CloseSessionSafelyAsync(ISession session)
        {
            if (session == null)
            {
                return;
            }

            try
            {
                if (session.IsHost)
                {
                    await session.AsHost().DeleteAsync();
                }
                else
                {
                    await session.LeaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[{nameof(NetworkGameManager)}] ⚠️ No se pudo cerrar la sesión de UGS: {e.Message}");
            }
        }

        private static void ShutdownNetwork()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
                Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Netcode for GameObjects apagado.");
            }
        }

        private void ResetSessionRuntimeState()
        {
            _joinCode = string.Empty;
            _spawnedPlayers.Clear();
            _sessionRole = SessionRole.None;
            _sessionFlowState = SessionFlowState.Idle;
        }

        public bool TryLoadLevelScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[{nameof(NetworkGameManager)}] La escena '{sceneName}' no existe o no está en Build Settings.",
                    this);
                return false;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                SceneManager.LoadScene(sceneName);
                return true;
            }

            if (!networkManager.IsServer)
            {
                Debug.LogWarning(
                    $"[{nameof(NetworkGameManager)}] Solo el host puede cambiar la escena de nivel.",
                    this);
                return false;
            }

            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError(
                    $"[{nameof(NetworkGameManager)}] No se pudo cargar '{sceneName}' ({status}).",
                    this);
                return false;
            }

            DespawnPlayersForSceneChange();
            return true;
        }

        private void BindLobbyUI(LobbyUIController lobbyUIController)
        {
            if (_lobbyUIController == lobbyUIController)
            {
                return;
            }

            UnbindLobbyUI();
            _lobbyUIController = lobbyUIController;

            if (_lobbyUIController == null)
            {
                return;
            }

            _lobbyUIController.HostRequested += HandleHostRequested;
            _lobbyUIController.JoinRequested += HandleJoinRequested;
            _lobbyUIController.ReadyChangeRequested += HandleReadyChangeRequested;
            _lobbyUIController.StartGameRequested += HandleStartGameRequested;
            _lobbyUIController.LeaveSessionRequested += HandleLeaveSessionRequested;
            _lobbyUIController.BackToMainRequested += HandleBackToMainRequested;

            RefreshLobbyUI();
        }

        private void UnbindLobbyUI()
        {
            if (_lobbyUIController == null)
            {
                return;
            }

            _lobbyUIController.HostRequested -= HandleHostRequested;
            _lobbyUIController.JoinRequested -= HandleJoinRequested;
            _lobbyUIController.ReadyChangeRequested -= HandleReadyChangeRequested;
            _lobbyUIController.StartGameRequested -= HandleStartGameRequested;
            _lobbyUIController.LeaveSessionRequested -= HandleLeaveSessionRequested;
            _lobbyUIController.BackToMainRequested -= HandleBackToMainRequested;
            _lobbyUIController = null;
        }

        private void HandleHostRequested()
        {
            _ = StartHostWithRelayAsync();
        }

        private void HandleJoinRequested(string joinCode)
        {
            _ = JoinSessionWithRelayAsync(joinCode);
        }

        private async void HandleReadyChangeRequested(bool isReady)
        {
            ISession session = _currentSession;

            if (session == null)
            {
                ShowLobbyError("No hay una sala activa.");
                return;
            }

            session.CurrentPlayer.Properties.TryGetValue(ReadyPropertyKey, out PlayerProperty previousValue);
            session.CurrentPlayer.SetProperty(
                ReadyPropertyKey,
                new PlayerProperty(isReady ? ReadyValue : string.Empty, VisibilityPropertyOptions.Public));

            try
            {
                await session.SaveCurrentPlayerDataAsync();
                RefreshLobbyUI();
            }
            catch (Exception e)
            {
                session.CurrentPlayer.SetProperty(ReadyPropertyKey, previousValue);
                Debug.LogError($"[{nameof(NetworkGameManager)}] No se pudo actualizar Ready: {e.Message}", this);
                ShowLobbyError("No se pudo actualizar tu estado.");
            }
        }

        private void HandleStartGameRequested()
        {
            if (_currentSession == null || !_currentSession.IsHost || !ArePlayersReady())
            {
                ShowLobbyError("Ambos jugadores deben estar listos.");
                return;
            }

            if (!TryLoadLevelScene(_gameplaySceneName))
            {
                ShowLobbyError("No se pudo iniciar la partida.");
            }
        }

        private void HandleLeaveSessionRequested()
        {
            _ = DisconnectAsync();
        }

        private async void HandleBackToMainRequested()
        {
            if (string.IsNullOrWhiteSpace(_mainMenuSceneName))
            {
                return;
            }

            await DisconnectAsync();
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        private void SetCurrentSession(ISession session)
        {
            UnsubscribeFromSessionEvents(_currentSession);
            _currentSession = session;
            _joinCode = session?.Code ?? string.Empty;

            if (_currentSession == null)
            {
                return;
            }

            _currentSession.Changed += OnSessionChanged;
            _currentSession.StateChanged += OnSessionStateChanged;
            _currentSession.PlayerJoined += OnSessionPlayerJoined;
            _currentSession.PlayerHasLeft += OnSessionPlayerHasLeft;
            _currentSession.PlayerPropertiesChanged += OnSessionChanged;
            _currentSession.SessionHostChanged += OnSessionHostChanged;
            _currentSession.RemovedFromSession += OnSessionEnded;
            _currentSession.Deleted += OnSessionEnded;
            _currentSession.Network.StateChanged += OnNetworkStateChanged;
            _currentSession.Network.StartFailed += OnNetworkStartFailed;
            SubscribeToNetworkSceneEvents();
        }

        private void UnsubscribeFromSessionEvents(ISession session)
        {
            if (session == null)
            {
                return;
            }

            session.Changed -= OnSessionChanged;
            session.StateChanged -= OnSessionStateChanged;
            session.PlayerJoined -= OnSessionPlayerJoined;
            session.PlayerHasLeft -= OnSessionPlayerHasLeft;
            session.PlayerPropertiesChanged -= OnSessionChanged;
            session.SessionHostChanged -= OnSessionHostChanged;
            session.RemovedFromSession -= OnSessionEnded;
            session.Deleted -= OnSessionEnded;
            session.Network.StateChanged -= OnNetworkStateChanged;
            session.Network.StartFailed -= OnNetworkStartFailed;
        }

        private void OnSessionChanged()
        {
            RefreshLobbyUI();
        }

        private void OnSessionStateChanged(SessionState state)
        {
            if (_isCleaningUp)
            {
                return;
            }

            if (state == SessionState.Connected)
            {
                RefreshLobbyUI();
                return;
            }

            _ = RecoverFromConnectionFailureAsync(
                "The online session ended. Please try again.");
        }

        private void OnSessionPlayerJoined(string playerId)
        {
            RefreshLobbyUI();
        }

        private void OnSessionPlayerHasLeft(string playerId)
        {
            if (_isCleaningUp)
            {
                return;
            }

            if (_sessionRole == SessionRole.Guest)
            {
                _ = RecoverFromConnectionFailureAsync(
                    "The host left the room. Please try joining another room.");
                return;
            }

            RefreshLobbyUI();

            if (_sessionRole == SessionRole.Host && _currentSession != null)
            {
                _lobbyUIController?.ShowHostWaitingRoomWithWarning(
                    _joinCode,
                    "The guest left the room. You can wait for them to join again.");
            }
        }

        private void OnSessionHostChanged(string newHostId)
        {
            if (!_isCleaningUp && _sessionRole == SessionRole.Guest)
            {
                _ = RecoverFromConnectionFailureAsync(
                    "The host left the room. Please try joining another room.");
            }
        }

        private void OnNetworkStateChanged(NetworkState state)
        {
            if (_isCleaningUp)
            {
                return;
            }

            if (state == NetworkState.Started)
            {
                RefreshLobbyUI();
            }
            else if (state == NetworkState.Stopped && _currentSession != null)
            {
                _ = RecoverFromConnectionFailureAsync(
                    "The network connection was interrupted. Please try again.");
            }
        }

        private void OnNetworkStartFailed(SessionError error)
        {
            if (!_isCleaningUp)
            {
                _ = RecoverFromConnectionFailureAsync(
                    $"Could not establish the network connection ({error}). Please try again.");
            }
        }

        private void OnSessionEnded()
        {
            if (!_isCleaningUp)
            {
                _ = RecoverFromConnectionFailureAsync(
                    "The online session ended. Please try again.");
            }
        }

        private void RefreshLobbyUI()
        {
            if (_lobbyUIController == null || _currentSession == null || _isCleaningUp)
            {
                return;
            }

            int playerCount = _currentSession.PlayerCount;
            bool networkReady = IsSharedLobbyNetworkReady();

            if (networkReady)
            {
                _sessionFlowState = SessionFlowState.SharedLobby;
                _lobbyUIController.ShowSharedLobby(_sessionRole == SessionRole.Host);
            }
            else if (_sessionRole == SessionRole.Host)
            {
                _sessionFlowState = playerCount >= 2
                    ? SessionFlowState.Synchronizing
                    : SessionFlowState.HostWaiting;
                _lobbyUIController.ShowHostWaitingRoom(_joinCode);

                if (playerCount >= 2)
                {
                    _lobbyUIController.ShowConnectionProgress("Synchronizing player...");
                }
            }
            else if (_sessionRole == SessionRole.Guest)
            {
                _sessionFlowState = SessionFlowState.Synchronizing;
                _lobbyUIController.ShowConnectionProgress("Synchronizing lobby...");
            }

            GetReadyStates(out bool hostReady, out bool guestReady);
            _lobbyUIController.SetConnectedPlayerCount(networkReady ? playerCount : 0);
            _lobbyUIController.SetReadyStates(hostReady, guestReady);
        }

        private bool IsSharedLobbyNetworkReady()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (_currentSession == null ||
                _currentSession.Network.State != NetworkState.Started ||
                networkManager == null ||
                !networkManager.IsListening ||
                _currentSession.PlayerCount < 2)
            {
                return false;
            }

            if (_sessionRole == SessionRole.Host)
            {
                return networkManager.IsServer && networkManager.ConnectedClientsIds.Count >= 2;
            }

            return _sessionRole == SessionRole.Guest &&
                   networkManager.IsClient &&
                   networkManager.IsConnectedClient;
        }

        private bool ArePlayersReady()
        {
            if (_currentSession == null || _currentSession.PlayerCount < 2)
            {
                return false;
            }

            GetReadyStates(out bool hostReady, out bool guestReady);
            return hostReady && guestReady;
        }

        private void GetReadyStates(out bool hostReady, out bool guestReady)
        {
            hostReady = false;
            guestReady = false;

            if (_currentSession == null)
            {
                return;
            }

            foreach (IReadOnlyPlayer player in _currentSession.Players)
            {
                bool isReady =
                    player.Properties.TryGetValue(ReadyPropertyKey, out PlayerProperty property) &&
                    property.Value == ReadyValue;

                if (player.Id == _currentSession.Host)
                {
                    hostReady = isReady;
                }
                else
                {
                    guestReady = isReady;
                }
            }
        }

        private void SubscribeToNetworkSceneEvents()
        {
            UnsubscribeFromNetworkSceneEvents();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnNetworkSceneLoadCompleted;
            }
        }

        private void UnsubscribeFromNetworkSceneEvents()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnNetworkSceneLoadCompleted;
            }
        }

        private void OnNetworkSceneLoadCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsServer ||
                !IsLevelSceneActive())
            {
                return;
            }

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnPlayerForClient(clientId);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            DestroyDuplicateNetworkManagers();
            BindLobbyUI(FindFirstObjectByType<LobbyUIController>());
        }

        private static void DestroyDuplicateNetworkManagers()
        {
            NetworkManager singleton = NetworkManager.Singleton;
            if (singleton == null)
            {
                return;
            }

            NetworkManager[] networkManagers = FindObjectsByType<NetworkManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (NetworkManager networkManager in networkManagers)
            {
                if (networkManager != singleton)
                {
                    Destroy(networkManager.gameObject);
                }
            }
        }

        private bool IsLevelSceneActive()
        {
            return FindFirstObjectByType<LevelUIController>(FindObjectsInactive.Include) != null;
        }

        private void DespawnPlayersForSceneChange()
        {
            foreach (GameObject playerObject in _spawnedPlayers.Values)
            {
                if (playerObject == null)
                {
                    continue;
                }

                NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
            }

            _spawnedPlayers.Clear();
        }

        private void ShowLobbyError(string message)
        {
            _lobbyUIController?.ShowError(message);
        }
    }
}
