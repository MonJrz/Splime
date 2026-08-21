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
            Application.runInBackground = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            LobbyUIController configuredLobbyUI = _lobbyUIController;
            _lobbyUIController = null;
            BindLobbyUI(configuredLobbyUI);
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.LogLevel = LogLevel.Developer;
                EnsureTransportOptimized();

                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
                NetworkManager.Singleton.OnClientStarted += OnClientStarted;
                NetworkManager.Singleton.OnClientStopped += OnClientStopped;
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;
                NetworkManager.Singleton.OnServerStopped += OnServerStopped;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🔌 NetworkManager callbacks registrados. LogLevel: {NetworkManager.Singleton.LogLevel}");
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
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
                NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
                NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
            }

            Instance = null;
        }

        private void OnClientStarted()
        {
            Debug.Log($"[{nameof(NetworkGameManager)}] 🚀 Netcode OnClientStarted. IsListening: {NetworkManager.Singleton?.IsListening}, IsClient: {NetworkManager.Singleton?.IsClient}, IsConnectedClient: {NetworkManager.Singleton?.IsConnectedClient}, LocalClientId: {NetworkManager.Singleton?.LocalClientId}");
            RefreshLobbyUI();
        }

        private void OnClientStopped(bool wasHost)
        {
            string disconnectReason = NetworkManager.Singleton?.DisconnectReason ?? "N/A";
            Debug.Log($"[{nameof(NetworkGameManager)}] 🛑 Netcode OnClientStopped. WasHost: {wasHost}, DisconnectReason: '{disconnectReason}'");

            // Fix 2: Si el cliente se detuvo sin haber llegado a conectarse (IsConnectedClient nunca fue true)
            // y estamos en el flujo de Join de un Guest, significa que el handshake Relay/WSS falló.
            // Iniciamos recuperación inmediatamente en lugar de quedarnos bloqueados en "Synchronizing".
            if (!wasHost &&
                !_isCleaningUp &&
                _sessionRole == SessionRole.Guest &&
                (_sessionFlowState == SessionFlowState.Synchronizing || _sessionFlowState == SessionFlowState.JoiningGuest))
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Handshake Relay/NGO falló para el cliente (razón: '{disconnectReason}'). Iniciando recuperación.", this);
                _ = RecoverFromConnectionFailureAsync("Could not connect to the host. Please try again.");
            }
        }

        private void OnServerStarted()
        {
            Debug.Log($"[{nameof(NetworkGameManager)}] 🚀 Netcode OnServerStarted. IsListening: {NetworkManager.Singleton?.IsListening}, IsServer: {NetworkManager.Singleton?.IsServer}, ConnectedClients: {NetworkManager.Singleton?.ConnectedClientsIds.Count}");
            RefreshLobbyUI();
        }

        private void OnServerStopped(bool wasHost)
        {
            Debug.Log($"[{nameof(NetworkGameManager)}] 🛑 Netcode OnServerStopped. WasHost: {wasHost}");
        }

        private void OnTransportFailure()
        {
            Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Netcode OnTransportFailure disparado! DisconnectReason: '{NetworkManager.Singleton?.DisconnectReason}'", this);
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[{nameof(NetworkGameManager)}] 🟢 OnClientConnected disparado. ClientId: {clientId}, LocalClientId: {NetworkManager.Singleton?.LocalClientId}, IsServer: {NetworkManager.Singleton?.IsServer}, IsConnectedClient: {NetworkManager.Singleton?.IsConnectedClient}, TotalClients: {NetworkManager.Singleton?.ConnectedClientsIds.Count}, IsLevelScene: {IsLevelSceneActive()}");

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
            string disconnectReason = networkManager != null ? networkManager.DisconnectReason : "N/A";
            Debug.LogWarning($"[{nameof(NetworkGameManager)}] 🔴 OnClientDisconnected. ClientId: {clientId}, LocalClientId: {networkManager?.LocalClientId}, DisconnectReason: '{disconnectReason}', IsServer: {networkManager?.IsServer}");

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
                
                var initOptions = new InitializationOptions();
                // Generar un perfil único por pestaña / instancia para evitar colisión de tokens en IndexedDB en WebGL
                string profile = "Player_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                initOptions.SetOption("com.unity.services.core.profile", profile);

                await UnityServices.InitializeAsync(initOptions);

                // Si ya tiene token cacheado de una sesión anterior, no re-autenticamos
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Autenticado con perfil '{profile}'. PlayerID: {AuthenticationService.Instance.PlayerId}");
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

                // El SDK de Unity Multiplayer gestiona internamente el UnityTransport durante CreateSessionAsync.
                // NO llamar a EnsureTransportOptimized() aquí — modificar el transport en este punto
                // puede resetear la configuración interna del SDK y causar que el handshake WSS falle.
                // UseWebSockets y UseEncryption se configuran en el Inspector del UnityTransport.
                var options = new SessionOptions
                {
                    MaxPlayers = 2
                }.WithRelayNetwork();

                ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);

                Debug.Log($"[{nameof(NetworkGameManager)}] 📥 CreateSessionAsync completado. SessionId: {session?.Id}, Code: {session?.Code}, Host: {session?.Host}, CurrentPlayer: {session?.CurrentPlayer?.Id}, NetworkState: {session?.Network?.State}, SessionState: {session?.State}");

                if (operationVersion != _sessionOperationVersion)
                {
                    Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ Operación obsoleta (version {operationVersion} vs {_sessionOperationVersion}). Cerrando sesión.");
                    await CloseSessionSafelyAsync(session);
                    return;
                }

                SetCurrentSession(session);
                _sessionFlowState = SessionFlowState.HostWaiting;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡HOST CREADO EXITOSAMENTE! 🔑 JOIN CODE: {_joinCode} | Network.State: {_currentSession?.Network?.State} | NM.IsListening: {NetworkManager.Singleton?.IsListening}");
                _lobbyUIController?.ShowHostWaitingRoom(_joinCode);
                RefreshLobbyUI();
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al crear la sesión (SessionException): {e.Message}\n{e}", this);
                await HandleConnectionAttemptFailedAsync(
                    operationVersion,
                    SessionRole.Host,
                    "Could not create the room. Please try again.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al iniciar Host: {e}", this);
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
                Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ Ignorando Join: IsConnecting={_isConnecting}, IsCleaningUp={_isCleaningUp}, SessionExists={_currentSession != null}");
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

                // NO llamar a EnsureTransportOptimized() aquí — el SDK de Unity Multiplayer
                // gestiona internamente el UnityTransport durante JoinSessionByCodeAsync.
                // Modificar el transport en este punto puede corromper la configuración interna
                // del SDK y causar que el handshake WSS falle silenciosamente.
                var joinOptions = new JoinSessionOptions();

                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(formattedCode, joinOptions);
                if (session == null)
                {
                    Debug.LogError(
                        $"[{nameof(NetworkGameManager)}] ❌ JoinSessionByCodeAsync retornó null " +
                        $"para el código: {formattedCode}",
                        this);
                    await HandleConnectionAttemptFailedAsync(
                        operationVersion,
                        SessionRole.Guest,
                        "Could not join the room. Please try again.");
                    return;
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] 📥 JoinSessionByCodeAsync completado. SessionId: {session.Id}, Code: {session.Code}, Host: {session.Host}, CurrentPlayer: {session.CurrentPlayer?.Id}, NetworkState: {session.Network?.State}, SessionState: {session.State}");

                if (operationVersion != _sessionOperationVersion)
                {
                    Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ Operación obsoleta (version {operationVersion} vs {_sessionOperationVersion}). Cerrando sesión.");
                    await CloseSessionSafelyAsync(session);
                    return;
                }

                // Fix 3: Verificar que el NetworkManager esté realmente escuchando tras el Join.
                // Si no está escuchando, el handshake WSS falló durante la Task y hay que recuperarse.
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                {
                    Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ NetworkManager NO está escuchando tras JoinSessionByCodeAsync. " +
                                   $"IsListening: {NetworkManager.Singleton?.IsListening}, NetworkState: {session.Network?.State}. " +
                                   $"El handshake WSS/Relay falló durante la Task. Iniciando recuperación.");
                    await CloseSessionSafelyAsync(session);
                    await HandleConnectionAttemptFailedAsync(
                        operationVersion,
                        SessionRole.Guest,
                        "Could not connect to the host. Please try again.");
                    return;
                }

                SetCurrentSession(session);
                _sessionFlowState = SessionFlowState.Synchronizing;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡CONEXIÓN COMO CLIENTE EXITOSA! Network.State: {_currentSession?.Network?.State} | NM.IsListening: {NetworkManager.Singleton?.IsListening}, NM.IsConnectedClient: {NetworkManager.Singleton?.IsConnectedClient}");
                RefreshLobbyUI();
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al unirse a la sesión (SessionException): {e.Message}\n{e}", this);
                await HandleConnectionAttemptFailedAsync(
                    operationVersion,
                    SessionRole.Guest,
                    "Could not join the room. Check the code and try again.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al unirse como Cliente: {e}", this);
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

            NetworkManager networkManager = NetworkManager.Singleton;
            bool networkIsListening = networkManager != null && networkManager.IsListening;

            if (_currentSession == null &&
                _sessionRole == SessionRole.None &&
                !networkIsListening)
            {
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
            if (session == null ||
                session.State == SessionState.None ||
                session.State == SessionState.Disconnected ||
                session.State == SessionState.Deleted)
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
            Debug.Log($"[{nameof(NetworkGameManager)}] 🔄 OnSessionChanged recibido de UGS.");
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
            Debug.Log($"[{nameof(NetworkGameManager)}] 👤 OnSessionPlayerJoined recibido para PlayerId: {playerId}. PlayerCount: {_currentSession?.PlayerCount}");
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
            NetworkManager nm = NetworkManager.Singleton;
            Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 OnNetworkStateChanged recibido: {state}. NM.IsListening: {nm?.IsListening}, NM.IsClient: {nm?.IsClient}, NM.IsConnectedClient: {nm?.IsConnectedClient}, NM.IsServer: {nm?.IsServer}, TotalClients: {nm?.ConnectedClientsIds.Count}");

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
            Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ OnNetworkStartFailed recibido: {error}", this);

            if (!_isCleaningUp)
            {
                _ = RecoverFromConnectionFailureAsync(
                    $"Could not establish the network connection ({error}). Please try again.");
            }
        }

        private void OnSessionEnded()
        {
            Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ OnSessionEnded recibido de UGS.");
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
            Debug.Log(
                $"[{nameof(NetworkGameManager)}] 📊 RefreshLobbyUI | " +
                $"PlayerCount: {playerCount} | NetworkReady: {networkReady} | " +
                $"HostReady: {hostReady} | GuestReady: {guestReady} | " +
                $"Role: {_sessionRole} | FlowState: {_sessionFlowState}");
            _lobbyUIController.SetConnectedPlayerCount(networkReady ? playerCount : 0);
            _lobbyUIController.SetReadyStates(hostReady, guestReady);
        }

        private bool IsSharedLobbyNetworkReady()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            bool sessionExists = _currentSession != null;
            NetworkState netState = _currentSession != null ? _currentSession.Network.State : NetworkState.Stopped;
            bool netStateStarted = netState == NetworkState.Started;
            bool nmExists = networkManager != null;
            bool nmListening = nmExists && networkManager.IsListening;
            int ugsPlayerCount = _currentSession != null ? _currentSession.PlayerCount : 0;
            bool ugsPlayersOk = ugsPlayerCount >= 2;

            if (!sessionExists || !netStateStarted || !nmExists || !nmListening || !ugsPlayersOk)
            {
                Debug.Log($"[{nameof(NetworkGameManager)}] ⏳ IsSharedLobbyNetworkReady -> FALSE. SessionExists: {sessionExists}, Network.State: {netState}, NM.IsListening: {nmListening}, UGSPlayerCount: {ugsPlayerCount}");
                return false;
            }

            if (_sessionRole == SessionRole.Host)
            {
                int connectedCount = networkManager.ConnectedClientsIds.Count;
                bool isServerReady = networkManager.IsServer && connectedCount >= 2;
                Debug.Log($"[{nameof(NetworkGameManager)}] ⏳ [Host] IsSharedLobbyNetworkReady -> {isServerReady}. IsServer: {networkManager.IsServer}, ConnectedClients: {connectedCount}");
                return isServerReady;
            }

            bool isGuestReady = _sessionRole == SessionRole.Guest &&
                   networkManager.IsClient &&
                   networkManager.IsConnectedClient;
            Debug.Log($"[{nameof(NetworkGameManager)}] ⏳ [Guest] IsSharedLobbyNetworkReady -> {isGuestReady}. IsClient: {networkManager.IsClient}, IsConnectedClient: {networkManager.IsConnectedClient}");
            return isGuestReady;
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

            if (!transport.UseEncryption)
            {
                transport.UseEncryption = true;
                Debug.Log("[NetworkGameManager] 🔒 UseEncryption activado en UnityTransport para soporte WSS.");
            }

            // Aumentar la cola de paquetes de 128 a 512 para evitar 'Receive queue is full' en WebGL
            if (transport.MaxPacketQueueSize < 512)
            {
                transport.MaxPacketQueueSize = 512;
            }

            // Aumentar los timeouts para que la conexión a través de Relay/WSS no se caiga por latencia o inactividad (5 minutos = 300000 ms)
            if (transport.HeartbeatTimeoutMS < 1000)
            {
                transport.HeartbeatTimeoutMS = 1000;
            }

            if (transport.DisconnectTimeoutMS < 300000)
            {
                transport.DisconnectTimeoutMS = 300000;
            }

            Debug.Log($"[{nameof(NetworkGameManager)}] ⚙️ UnityTransport configurado: Protocol={transport.Protocol}, UseWebSockets={transport.UseWebSockets}, UseEncryption={transport.UseEncryption}, MaxPacketQueueSize={transport.MaxPacketQueueSize}, HeartbeatTimeoutMS={transport.HeartbeatTimeoutMS}, DisconnectTimeoutMS={transport.DisconnectTimeoutMS}");
        }
    }
}
