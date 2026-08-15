using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Splime.Core;
using Splime.Player;
using Splime.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
                EnsureTransportOptimized();
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
            Debug.Log($"[{nameof(NetworkGameManager)}] 🟢 OnClientConnected disparado. ClientId: {clientId}, IsServer: {NetworkManager.Singleton?.IsServer}, IsLevelScene: {IsLevelSceneActive()}");

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
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
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

            RefreshLobbyUI();
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (_spawnedPlayers.ContainsKey(clientId))
            {
                return;
            }

            GameObject prefabToSpawn = (clientId == 0) ? _slimeTransformerPrefab : _slimeAgilePrefab;
            SlimeData dataToAssign = (clientId == 0) ? _transformerData : _agileData;
            SpawnPlayerRole targetRole = (clientId == 0) ? SpawnPlayerRole.Player1 : SpawnPlayerRole.Player2;

            // 1. Obtener la posición y rotación desde UniversalSpawnPoint en la escena actual (si existen)
            Vector3 spawnPos = (clientId == 0) ? _player1SpawnPosition : _player2SpawnPosition;
            Quaternion spawnRot = Quaternion.identity;

            UniversalSpawnPoint spawnPoint = UniversalSpawnPoint.GetPlayerSpawn(targetRole);

            if (spawnPoint != null)
            {
                spawnPos = spawnPoint.Position;
                spawnRot = spawnPoint.Rotation;
                Debug.Log($"[{nameof(NetworkGameManager)}] 📍 Usando UniversalSpawnPoint de la escena para {targetRole}: {spawnPos}, Rot: {spawnRot.eulerAngles}");
            }
            else
            {
                Debug.Log($"[{nameof(NetworkGameManager)}] ℹ️ No se encontró UniversalSpawnPoint para {targetRole}, usando posición por defecto: {spawnPos}");
            }

            if (prefabToSpawn == null)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] Prefab de jugador no asignado en el Inspector para clientId {clientId}.", this);
                return;
            }

            Debug.Log($"[{nameof(NetworkGameManager)}] 🚀 Intentando SpawnPlayerForClient para ClientId: {clientId}, Prefab: {prefabToSpawn.name}, Pos: {spawnPos}");
            GameObject playerInstance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
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
            if (_isConnecting || _currentSession != null)
            {
                return;
            }

            _isConnecting = true;

            try
            {
                bool initialized = await InitializeServicesAsync();
                if (!initialized)
                {
                    ShowLobbyError("No se pudo conectar con los servicios de Unity.");
                    return;
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 Creando Sesión de Relay (MaxPlayers = 2)...");

                EnsureTransportOptimized();

                // Forzar protocolo WSS para compatibilidad WebGL ↔ Windows.
                // WSS es el único protocolo soportado por navegadores (WebGL).
                // Usar el mismo protocolo en Desktop garantiza la conexión cruzada.
                var options = new SessionOptions
                {
                    MaxPlayers = 2
                }.WithRelayNetwork()
                 .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.WSS });

                ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);
                SetCurrentSession(session);

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡HOST CREADO EXITOSAMENTE!");
                Debug.Log($"[{nameof(NetworkGameManager)}] 🔑 JOIN CODE: {_joinCode}");
                _lobbyUIController?.ShowHostWaitingRoom(_joinCode);
                RefreshLobbyUI();
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al crear la sesión: {e.Message}\nStack: {e.StackTrace}");
                ShowLobbyError("No se pudo crear la sala. Intenta nuevamente.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al iniciar Host: {e}\nStack: {e.StackTrace}");
                ShowLobbyError("Ocurrió un error al crear la sala.");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>
        /// Paso 5: Se une a una sesión multiplayer existente en Relay mediante su Join Code.
        /// </summary>
        public async Task JoinSessionWithRelayAsync(string codeToJoin)
        {
            if (_isConnecting || _currentSession != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(codeToJoin))
            {
                Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ Debes ingresar un Join Code válido para conectarte.");
                ShowLobbyError("Ingresa un código de sala válido.");
                return;
            }

            _isConnecting = true;

            try
            {
                bool initialized = await InitializeServicesAsync();
                if (!initialized)
                {
                    ShowLobbyError("No se pudo conectar con los servicios de Unity.");
                    return;
                }

                string formattedCode = codeToJoin.Trim().ToUpper();
                Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 Conectándose a la sesión con Join Code: {formattedCode}...");

                EnsureTransportOptimized();

                // Forzar protocolo WSS al unirse: necesario para que el cliente Desktop
                // se comunique correctamente con un Host WebGL (que solo soporta WSS).
                var joinOptions = new JoinSessionOptions()
                    .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.WSS });

                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(formattedCode, joinOptions);
                if (session == null)
                {
                    Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ JoinSessionByCodeAsync retornó null para el código: {formattedCode}");
                    ShowLobbyError("No se pudo conectar a la sala (sesión nula).");
                    return;
                }

                SetCurrentSession(session);

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡CONEXIÓN COMO CLIENTE EXITOSA!");
                _lobbyUIController?.ShowSharedLobbyAsGuest();
                RefreshLobbyUI();
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al unirse a la sesión: {e.Message}\nStack: {e.StackTrace}");
                ShowLobbyError("No se pudo entrar a la sala. Revisa el código.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al unirse como Cliente: {e}\nStack: {e.StackTrace}");
                ShowLobbyError("Ocurrió un error al entrar a la sala.");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>
        /// Paso 9: Realiza la desconexión ordenada cerrando la sesión de UGS en la nube y apagando NGO.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_isConnecting)
            {
                return;
            }

            _isConnecting = true;
            Debug.Log($"[{nameof(NetworkGameManager)}] 🚪 Iniciando proceso de desconexión...");

            ISession session = _currentSession;
            _currentSession = null;
            UnsubscribeFromSessionEvents(session);
            UnsubscribeFromNetworkSceneEvents();

            if (session != null)
            {
                try
                {
                    await session.LeaveAsync();
                    Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Sesión de UGS abandonada correctamente.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ No se pudo abandonar la sesión de UGS: {e.Message}");
                }
            }

            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Netcode for GameObjects apagado.");
            }

            _joinCode = string.Empty;
            _spawnedPlayers.Clear();
            _isConnecting = false;
            _lobbyUIController?.NotifySessionLeft();
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
                Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ HandleReadyChangeRequested: No hay sesión activa.");
                ShowLobbyError("No hay una sala activa.");
                return;
            }

            Debug.Log($"[{nameof(NetworkGameManager)}] 🔄 Solicitando cambio de Ready a: {isReady} por jugador ID: {session.CurrentPlayer?.Id} (IsHost: {session.IsHost})");

            session.CurrentPlayer.Properties.TryGetValue(ReadyPropertyKey, out PlayerProperty previousValue);
            session.CurrentPlayer.SetProperty(
                ReadyPropertyKey,
                new PlayerProperty(isReady ? ReadyValue : string.Empty, VisibilityPropertyOptions.Public));

            try
            {
                await session.SaveCurrentPlayerDataAsync();
                Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Ready guardado exitosamente en UGS. Refrescando UI...");
                RefreshLobbyUI();
            }
            catch (Exception e)
            {
                session.CurrentPlayer.SetProperty(ReadyPropertyKey, previousValue);
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ No se pudo actualizar Ready: {e.Message}", this);
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

        private void HandleBackToMainRequested()
        {
            if (!string.IsNullOrWhiteSpace(_mainMenuSceneName))
            {
                SceneManager.LoadScene(_mainMenuSceneName);
            }
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
            _currentSession.PlayerJoined += OnSessionPlayerChanged;
            _currentSession.PlayerHasLeft += OnSessionPlayerChanged;
            _currentSession.PlayerPropertiesChanged += OnSessionChanged;
            _currentSession.RemovedFromSession += OnSessionEnded;
            _currentSession.Deleted += OnSessionEnded;
            SubscribeToNetworkSceneEvents();
        }

        private void UnsubscribeFromSessionEvents(ISession session)
        {
            if (session == null)
            {
                return;
            }

            session.Changed -= OnSessionChanged;
            session.PlayerJoined -= OnSessionPlayerChanged;
            session.PlayerHasLeft -= OnSessionPlayerChanged;
            session.PlayerPropertiesChanged -= OnSessionChanged;
            session.RemovedFromSession -= OnSessionEnded;
            session.Deleted -= OnSessionEnded;
        }

        private void OnSessionChanged()
        {
            Debug.Log($"[{nameof(NetworkGameManager)}] 🔄 OnSessionChanged recibido de UGS.");
            RefreshLobbyUI();
        }

        private void OnSessionPlayerChanged(string playerId)
        {
            Debug.Log($"[{nameof(NetworkGameManager)}] 👤 OnSessionPlayerChanged recibido para PlayerId: {playerId}. PlayerCount: {_currentSession?.PlayerCount}");
            RefreshLobbyUI();
        }

        private void OnSessionEnded()
        {
            ISession endedSession = _currentSession;
            _currentSession = null;
            UnsubscribeFromSessionEvents(endedSession);
            UnsubscribeFromNetworkSceneEvents();
            _joinCode = string.Empty;
            _lobbyUIController?.NotifySessionLeft();
        }

        private void RefreshLobbyUI()
        {
            if (_lobbyUIController == null || _currentSession == null)
            {
                return;
            }

            int playerCount = _currentSession.PlayerCount;

            if (_currentSession.IsHost && playerCount < 2)
            {
                _lobbyUIController.ShowHostWaitingRoom(_joinCode);
            }
            else
            {
                _lobbyUIController.ShowSharedLobby(_currentSession.IsHost);
            }

            GetReadyStates(out bool hostReady, out bool guestReady);
            Debug.Log($"[{nameof(NetworkGameManager)}] 📊 RefreshLobbyUI | PlayerCount: {playerCount} | HostReady: {hostReady} | GuestReady: {guestReady} | IsLocalHost: {_currentSession.IsHost}");
            _lobbyUIController.SetConnectedPlayerCount(playerCount);
            _lobbyUIController.SetReadyStates(hostReady, guestReady);
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

            if (_currentSession == null || _currentSession.Players == null)
            {
                return;
            }

            foreach (IReadOnlyPlayer player in _currentSession.Players)
            {
                if (player == null)
                {
                    continue;
                }

                bool isReady = false;
                string propVal = "(null)";

                if (player.Properties != null && player.Properties.TryGetValue(ReadyPropertyKey, out PlayerProperty property))
                {
                    propVal = property?.Value ?? "(null)";
                    isReady = propVal == ReadyValue;
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] 🔍 Player in Session: Id={player.Id} | IsHost={player.Id == _currentSession.Host} | ReadyKey={propVal} | IsReady={isReady}");

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
            Debug.Log($"[{nameof(NetworkGameManager)}] 🎬 OnNetworkSceneLoadCompleted para escena: '{sceneName}'. IsServer: {NetworkManager.Singleton?.IsServer}, IsLevel: {IsLevelSceneActive()}");

            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsServer ||
                !IsLevelSceneActive())
            {
                return;
            }

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                Debug.Log($"[{nameof(NetworkGameManager)}] 👥 Spawneando jugador para ClientId: {clientId}");
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

        /// <summary>
        /// Garantiza que el UnityTransport del NetworkManager esté optimizado para WebSockets y WebGL.
        /// Aumenta MaxPacketQueueSize y ajusta Timeouts para evitar que la cola se sature (cola llena)
        /// y se pierda la conexión con Relay en el navegador.
        /// </summary>
        private static void EnsureTransportOptimized()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                return;
            }

            if (!transport.UseWebSockets)
            {
                transport.UseWebSockets = true;
                Debug.Log("[NetworkGameManager] 🔌 UseWebSockets activado en UnityTransport.");
            }

            // Aumentar la cola de paquetes de 128 a 512 para evitar 'Receive queue is full' en WebGL
            if (transport.MaxPacketQueueSize < 512)
            {
                transport.MaxPacketQueueSize = 512;
            }

            // Aumentar los timeouts para que la conexión a través de Relay/WSS no se caiga por latencia
            if (transport.HeartbeatTimeoutMS < 1000)
            {
                transport.HeartbeatTimeoutMS = 1000;
            }

            if (transport.DisconnectTimeoutMS < 30000)
            {
                transport.DisconnectTimeoutMS = 30000;
            }
        }
    }
}
