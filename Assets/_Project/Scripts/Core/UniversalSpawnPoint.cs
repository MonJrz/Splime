using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Splime.Core
{
    /// <summary>
    /// Categoría general del objeto a spawnear.
    /// </summary>
    public enum SpawnCategory
    {
        Player = 0,
        Enemy = 1,
        Item = 2,
        PuzzleElement = 3,
        Checkpoint = 4,
        Custom = 5
    }

    /// <summary>
    /// Puntos de spawn para jugadores de Splime.
    /// </summary>
    public enum SpawnPlayerRole
    {
        Player1 = 0,
        Player2 = 1,
        Player3 = 2,
        Player4 = 3
    }

    /// <summary>
    /// Componente universal para marcar puntos de spawn en cualquier nivel.
    /// Soporta: Jugadores, Enemigos, Puzzles, Items, Checkpoints y Prefabs automáticos.
    /// </summary>
    [SelectionBase]
    public class UniversalSpawnPoint : MonoBehaviour
    {
        public static readonly List<UniversalSpawnPoint> AllSpawnPoints = new List<UniversalSpawnPoint>();

        [Header("Classification")]
        [Tooltip("Categoría del objeto que aparecerá aquí.")]
        [SerializeField] private SpawnCategory _category = SpawnCategory.Player;

        [Tooltip("Para jugadores: indica el rol específico.")]
        [SerializeField] private SpawnPlayerRole _playerRole = SpawnPlayerRole.Player1;

        [Tooltip("Identificador único o tag opcional para buscar por código (ej: 'BossSpawn', 'Key_Red', 'HeavyBox').")]
        [SerializeField] private string _spawnId = string.Empty;

        [Header("Auto-Spawning (Opcional)")]
        [Tooltip("Si se asigna, este prefab puede spawnearse automáticamente al iniciar la escena.")]
        [SerializeField] private GameObject _prefabToSpawn;

        [Tooltip("Si es true, se instanciará el prefab al arrancar la escena (ideal para enemigos o cajas de puzzle).")]
        [SerializeField] private bool _spawnOnStart = false;

        [Tooltip("Si es true y el prefab tiene NetworkObject, se spawneará autoritativamente en el servidor NGO.")]
        [SerializeField] private bool _spawnAsNetworkObject = true;

        public SpawnCategory Category => _category;
        public SpawnPlayerRole PlayerRole => _playerRole;
        public string SpawnId => _spawnId;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        private void OnEnable()
        {
            if (!AllSpawnPoints.Contains(this))
            {
                AllSpawnPoints.Add(this);
            }
        }

        private void OnDisable()
        {
            AllSpawnPoints.Remove(this);
        }

        private void Start()
        {
            if (_spawnOnStart && _prefabToSpawn != null)
            {
                SpawnPrefab();
            }
        }

        /// <summary>
        /// Instancia el prefab asignado en este punto de spawn.
        /// </summary>
        public GameObject SpawnPrefab()
        {
            if (_prefabToSpawn == null) return null;

            // Si es un objeto de red, solo el servidor/host debe spawnearlo
            if (_spawnAsNetworkObject && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (!NetworkManager.Singleton.IsServer)
                {
                    return null; // Los clientes reciben el spawn por red del Host
                }

                GameObject instance = Instantiate(_prefabToSpawn, Position, Rotation);
                NetworkObject netObj = instance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
                return instance;
            }
            else
            {
                // Objeto local o sin red activa
                return Instantiate(_prefabToSpawn, Position, Rotation);
            }
        }

        // ==========================================
        // MÉTODOS ESTÁTICOS DE BÚSQUEDA Y CHECKPOINTS
        // ==========================================

        private static readonly Dictionary<SpawnPlayerRole, (Vector3 position, Quaternion rotation)> _activePlayerSpawns =
            new Dictionary<SpawnPlayerRole, (Vector3, Quaternion)>();

        private static readonly Dictionary<SpawnPlayerRole, int> _playerActiveCheckpointIndices =
            new Dictionary<SpawnPlayerRole, int>();

        private static int _currentActiveCheckpointIndex = -1;

        public static int CurrentActiveCheckpointIndex
        {
            get => _currentActiveCheckpointIndex;
            set => _currentActiveCheckpointIndex = value;
        }

        public static int GetPlayerActiveCheckpointIndex(SpawnPlayerRole role)
        {
            return _playerActiveCheckpointIndices.TryGetValue(role, out int index) ? index : -1;
        }

        public static void SetActivePlayerSpawn(SpawnPlayerRole role, Vector3 position, Quaternion rotation, int checkpointIndex = -1)
        {
            _activePlayerSpawns[role] = (position, rotation);
            if (checkpointIndex >= 0)
            {
                _playerActiveCheckpointIndices[role] = checkpointIndex;
                if (checkpointIndex > _currentActiveCheckpointIndex)
                {
                    _currentActiveCheckpointIndex = checkpointIndex;
                }
            }
        }

        public static void ResetPlayerSpawns()
        {
            _activePlayerSpawns.Clear();
            _playerActiveCheckpointIndices.Clear();
            _currentActiveCheckpointIndex = -1;
        }

        public static bool TryGetActiveSpawnTransform(SpawnPlayerRole role, out Vector3 position, out Quaternion rotation)
        {
            if (_activePlayerSpawns.TryGetValue(role, out var customTransform))
            {
                position = customTransform.position;
                rotation = customTransform.rotation;
                return true;
            }

            UniversalSpawnPoint defaultSpawn = AllSpawnPoints.Find(p => p._category == SpawnCategory.Player && p._playerRole == role);
            if (defaultSpawn != null)
            {
                position = defaultSpawn.Position;
                rotation = defaultSpawn.Rotation;
                return true;
            }

            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        public static UniversalSpawnPoint GetPlayerSpawn(SpawnPlayerRole role)
        {
            return AllSpawnPoints.Find(p => p._category == SpawnCategory.Player && p._playerRole == role);
        }

        public static UniversalSpawnPoint GetById(string spawnId)
        {
            return AllSpawnPoints.Find(p => string.Equals(p._spawnId, spawnId, StringComparison.OrdinalIgnoreCase));
        }

        public static List<UniversalSpawnPoint> GetByCategory(SpawnCategory category)
        {
            return AllSpawnPoints.FindAll(p => p._category == category);
        }

        // ==========================================
        // GIZMOS VISUALES EN EL EDITOR
        // ==========================================

        private void OnDrawGizmos()
        {
            Color gizmoColor = GetGizmoColor();
            Gizmos.color = gizmoColor;

            Vector3 center = transform.position + Vector3.up * 0.5f;
            Gizmos.DrawSphere(center, 0.4f);
            Gizmos.DrawWireSphere(center, 0.45f);

            // Flecha indicando orientación frontal (Forward)
            Gizmos.color = Color.white;
            Vector3 forwardEnd = center + transform.forward * 1.2f;
            Gizmos.DrawLine(center, forwardEnd);
            Gizmos.DrawLine(forwardEnd, forwardEnd - transform.forward * 0.3f + transform.right * 0.2f);
            Gizmos.DrawLine(forwardEnd, forwardEnd - transform.forward * 0.3f - transform.right * 0.2f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1.2f, 1f));
        }

        private Color GetGizmoColor()
        {
            switch (_category)
            {
                case SpawnCategory.Player:
                    return _playerRole == SpawnPlayerRole.Player1
                        ? new Color(1f, 0.5f, 0f, 0.8f)   // Naranja (Transformer)
                        : new Color(0.2f, 0.8f, 1f, 0.8f); // Celeste (Ágil)
                case SpawnCategory.Enemy:
                    return new Color(1f, 0.2f, 0.2f, 0.8f); // Rojo (Enemigos)
                case SpawnCategory.PuzzleElement:
                    return new Color(0.8f, 0.4f, 1f, 0.8f); // Morado (Puzzles)
                case SpawnCategory.Item:
                    return new Color(1f, 0.9f, 0.1f, 0.8f); // Amarillo (Items)
                case SpawnCategory.Checkpoint:
                    return new Color(0.2f, 1f, 0.3f, 0.8f); // Verde (Checkpoints)
                default:
                    return Color.gray;
            }
        }
    }

    /// <summary>
    /// Alias para retrocompatibilidad con PlayerSpawnPoint.
    /// </summary>
    public class PlayerSpawnPoint : UniversalSpawnPoint
    {
    }
}
