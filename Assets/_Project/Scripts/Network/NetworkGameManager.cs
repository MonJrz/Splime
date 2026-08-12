using System.Collections.Generic;
using Unity.Netcode;
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
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
                if (playerObj != null && playerObj.GetComponent<NetworkObject>() != null)
                {
                    playerObj.GetComponent<NetworkObject>().Despawn();
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

                Debug.Log($"[{nameof(NetworkGameManager)}] 🎮 Jugador {clientId + 1} ({prefabToSpawn.name}) instanciado exitosamente en {spawnPos} con Ownership para clientId {clientId}.", this);
            }
        }

        private void OnGUI()
        {
            if (!_showDebugGui) return;

            GUILayout.BeginArea(new Rect(20, 20, 240, 160));

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label("<b>Splime Multiplayer NGO</b>");

                if (GUILayout.Button("Start Host (P1: Transformador)", GUILayout.Height(35)))
                {
                    NetworkManager.Singleton.StartHost();
                }

                if (GUILayout.Button("Start Client (P2: Ágil)", GUILayout.Height(35)))
                {
                    NetworkManager.Singleton.StartClient();
                }
            }
            else if (NetworkManager.Singleton != null)
            {
                GUILayout.Label($"<b>Modo:</b> {(NetworkManager.Singleton.IsHost ? "Host (Jugador 1)" : "Cliente (Jugador 2)")}");
                GUILayout.Label($"<b>Jugadores Conectados:</b> {NetworkManager.Singleton.ConnectedClientsIds.Count}");

                if (GUILayout.Button("Desconectar", GUILayout.Height(30)))
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }

            GUILayout.EndArea();
        }
    }
}
