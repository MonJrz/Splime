using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using Splime.Core;
using Splime.Player;

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

        [Header("OnGUI Debug Controls")]
        [SerializeField] private bool _showDebugGui = true;

        private readonly Dictionary<ulong, GameObject> _spawnedPlayers = new Dictionary<ulong, GameObject>();

        // Estado de inicialización de Unity Gaming Services
        private bool _isInitialized = false;

        // Estado de la sesión actual (Multiplayer Services 2.3.0)
        private ISession _currentSession;
        private string _joinCode = "";
        private string _inputJoinCode = "";
        private bool _isConnecting = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private async void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            // Inicializar Unity Gaming Services al arrancar
            await InitializeServicesAsync();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            SpawnPlayerForClient(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (_spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
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
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
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
            if (_isInitialized) return true;

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
            if (_isConnecting) return;
            _isConnecting = true;

            try
            {
                // 1. Asegurar que UGS y Autenticación estén listos
                bool initialized = await InitializeServicesAsync();
                if (!initialized)
                {
                    Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ No se pudo iniciar el Host porque UGS no está listo.");
                    _isConnecting = false;
                    return;
                }

                Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 Creando Sesión de Relay (MaxPlayers = 2)...");

                // 2. Configurar opciones de sesión con Relay
                var options = new SessionOptions
                {
                    MaxPlayers = 2
                }.WithRelayNetwork();

                // 3. Crear la sesión a través de Multiplayer Services SDK
                _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                _joinCode = _currentSession.Code;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡HOST CREADO EXITOSAMENTE!");
                Debug.Log($"[{nameof(NetworkGameManager)}] 🔑 JOIN CODE: {_joinCode}");
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al crear la sesión: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al iniciar Host: {e.Message}");
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
            if (_isConnecting) return;

            if (string.IsNullOrWhiteSpace(codeToJoin))
            {
                Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ Debes ingresar un Join Code válido para conectarte.");
                return;
            }

            _isConnecting = true;

            try
            {
                // 1. Asegurar UGS y Autenticación
                bool initialized = await InitializeServicesAsync();
                if (!initialized)
                {
                    Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ No se pudo conectar porque UGS no está listo.");
                    _isConnecting = false;
                    return;
                }

                string formattedCode = codeToJoin.Trim().ToUpper();
                Debug.Log($"[{nameof(NetworkGameManager)}] 🌐 Conectándose a la sesión con Join Code: {formattedCode}...");

                // 2. Unirse por código usando Multiplayer Services SDK
                _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(formattedCode);
                _joinCode = _currentSession.Code;

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎉 ¡CONEXIÓN COMO CLIENTE EXITOSA!");
            }
            catch (SessionException e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error al unirse a la sesión: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(NetworkGameManager)}] ❌ Error inesperado al unirse como Cliente: {e.Message}");
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
            Debug.Log($"[{nameof(NetworkGameManager)}] 🚪 Iniciando proceso de desconexión...");

            // 1. Abandonar/Eliminar la Sesión en UGS Multiplayer Services
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.LeaveAsync();
                    Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Sesión de UGS abandonada correctamente.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{nameof(NetworkGameManager)}] ⚠️ No se pudo abandonar la sesión de UGS: {e.Message}");
                }
                finally
                {
                    _currentSession = null;
                }
            }

            // 2. Apagar Netcode for GameObjects
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log($"[{nameof(NetworkGameManager)}] ✅ Netcode for GameObjects apagado.");
            }

            // 3. Limpiar estado local
            _joinCode = "";
            _spawnedPlayers.Clear();
        }

        private void OnGUI()
        {
            if (!_showDebugGui) return;

            GUILayout.BeginArea(new Rect(20, 20, 280, 240));

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label("<b>Splime Multiplayer (Unity Relay)</b>");

                if (_isConnecting)
                {
                    GUILayout.Label("<i>Conectando a la nube...</i>");
                }
                else
                {
                    if (GUILayout.Button("Start Host (P1: Transformador)", GUILayout.Height(35)))
                    {
                        _ = StartHostWithRelayAsync();
                    }

                    GUILayout.Space(10);
                    GUILayout.Label("Unirse como Cliente (P2: Ágil):");
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Code:", GUILayout.Width(45));
                    _inputJoinCode = GUILayout.TextField(_inputJoinCode.ToUpper(), 8);
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Join Session", GUILayout.Height(35)))
                    {
                        _ = JoinSessionWithRelayAsync(_inputJoinCode);
                    }
                }
            }
            else if (NetworkManager.Singleton != null)
            {
                GUILayout.Label($"<b>Modo:</b> {(NetworkManager.Singleton.IsHost ? "Host (Jugador 1)" : "Cliente (Jugador 2)")}");
                
                if (!string.IsNullOrEmpty(_joinCode))
                {
                    GUILayout.Label($"<b>JOIN CODE:</b> <color=yellow>{_joinCode}</color>");
                }

                GUILayout.Label($"<b>Jugadores Conectados:</b> {NetworkManager.Singleton.ConnectedClientsIds.Count}");

                if (GUILayout.Button("Desconectar", GUILayout.Height(30)))
                {
                    _ = DisconnectAsync();
                }
            }

            GUILayout.EndArea();
        }
    }
}
