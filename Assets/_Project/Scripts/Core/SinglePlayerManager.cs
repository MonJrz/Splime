using System;
using Splime.CameraControl;
using Splime.Network;
using Splime.Player;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Splime.Core
{
    /// <summary>
    /// Gestor para el Modo de Un Solo Jugador en Splime.
    /// CÓMO USARLO:
    ///   Opción A (recomendada): coloca este componente como un GameObject
    ///   en cada escena de nivel (Level1, Level2, Level3) y asigna los prefabs
    ///   y SlimeData en el Inspector.
    ///   Opción B (automática): si NetworkGameManager existe (viene de Main),
    ///   los prefabs se toman de él automáticamente.
    /// En modo online la lógica de Netcode sigue intacta; este gestor se desactiva.
    /// </summary>
    [DisallowMultipleComponent]
    public class SinglePlayerManager : MonoBehaviour
    {
        public static SinglePlayerManager Instance { get; private set; }

        [Header("Slime Prefabs")]
        [Tooltip("Arrastra Slime_Transformer.prefab")]
        [SerializeField] private GameObject _slimeTransformerPrefab;
        [Tooltip("Arrastra Slime_Agile.prefab")]
        [SerializeField] private GameObject _slimeAgilePrefab;

        [Header("Slime Data Assets")]
        [Tooltip("Arrastra SlimeData_Transformer.asset")]
        [SerializeField] private SlimeData _transformerData;
        [Tooltip("Arrastra SlimeData_Agile.asset")]
        [SerializeField] private SlimeData _agileData;

        [Header("Configuración Inicial")]
        [SerializeField] private SpawnPlayerRole _startingSlime = SpawnPlayerRole.Player1;
        [SerializeField] private bool _autoSpawnIfMissing = true;

        // Referencias a instancias en escena (se llenan automáticamente)
        private GameObject _transformerInstance;
        private GameObject _agileInstance;

        private SpawnPlayerRole _activeRole = SpawnPlayerRole.Player1;
        private CinemachineAutoTargetPlayer _cinemachineTargeter;
        private bool _isInitialized;

        public event Action<SpawnPlayerRole, GameObject> ActiveSlimeChanged;

        public SpawnPlayerRole ActiveRole => _activeRole;
        public GameObject ActiveSlime => _activeRole == SpawnPlayerRole.Player1 ? _transformerInstance : _agileInstance;
        public GameObject InactiveSlime => _activeRole == SpawnPlayerRole.Player1 ? _agileInstance : _transformerInstance;

        public bool IsSinglePlayerActive =>
            NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            SceneManager.sceneLoaded += OnSceneLoadedStatic;
        }

        private static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode)
        {
            // Red activa (online) -> no hacer nada, Netcode gestiona el spawn de jugadores
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                return;
            }

            // Detectar si la escena cargada es un nivel de juego
            bool isLevel = scene.name.StartsWith("Level", StringComparison.OrdinalIgnoreCase) ||
                           FindFirstObjectByType<UniversalSpawnPoint>(FindObjectsInactive.Include) != null ||
                           FindFirstObjectByType<Splime.UI.LevelFlowController>(FindObjectsInactive.Include) != null;

            if (!isLevel)
            {
                if (Instance != null)
                {
                    Instance.PrepareForNewScene();
                }
                return;
            }

            SinglePlayerManager manager = Instance;
            if (manager == null)
            {
                manager = FindFirstObjectByType<SinglePlayerManager>(FindObjectsInactive.Include);
                if (manager == null)
                {
                    GameObject spObj = new GameObject("[Auto] SinglePlayerManager");
                    manager = spObj.AddComponent<SinglePlayerManager>();
                }
            }

            manager.PrepareForNewScene();
            manager.InitializeSinglePlayerSession();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Adoptar referencias configuradas si la instancia persistente no las tiene
                if (_slimeTransformerPrefab != null && Instance._slimeTransformerPrefab == null)
                    Instance._slimeTransformerPrefab = _slimeTransformerPrefab;
                if (_slimeAgilePrefab != null && Instance._slimeAgilePrefab == null)
                    Instance._slimeAgilePrefab = _slimeAgilePrefab;
                if (_transformerData != null && Instance._transformerData == null)
                    Instance._transformerData = _transformerData;
                if (_agileData != null && Instance._agileData == null)
                    Instance._agileData = _agileData;

                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SlimeInput.SwitchCharacterRequested += HandleSwitchCharacterRequested;
        }

        private void OnDisable()
        {
            SlimeInput.SwitchCharacterRequested -= HandleSwitchCharacterRequested;
        }

        private void Start()
        {
            if (!IsSinglePlayerActive)
            {
                enabled = false;
                return;
            }

            if (!_isInitialized)
            {
                InitializeSinglePlayerSession();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INICIALIZACIÓN
        // ─────────────────────────────────────────────────────────────────────

        public void PrepareForNewScene()
        {
            _transformerInstance = null;
            _agileInstance = null;
            _cinemachineTargeter = null;
            _isInitialized = false;
        }

        /// <summary>
        /// Llamado por NetworkGameManager al cargar un nivel sin sesión activa,
        /// inyectando los prefabs que tiene configurados.
        /// </summary>
        public void ConfigureAndInitialize(
            GameObject transformerPrefab,
            GameObject agilePrefab,
            SlimeData transformerData,
            SlimeData agileData)
        {
            if (transformerPrefab != null) _slimeTransformerPrefab = transformerPrefab;
            if (agilePrefab != null) _slimeAgilePrefab = agilePrefab;
            if (transformerData != null) _transformerData = transformerData;
            if (agileData != null) _agileData = agileData;

            InitializeSinglePlayerSession();
        }

        public void InitializeSinglePlayerSession()
        {
            if (!IsSinglePlayerActive) return;
            if (_isInitialized) return;

            ResolveMissingReferences();

            if (!ValidatePrefabs()) return;

            FindOrSpawnSlimes();

            _cinemachineTargeter = FindFirstObjectByType<CinemachineAutoTargetPlayer>(FindObjectsInactive.Include);

            _activeRole = _startingSlime;
            ApplyControlToSlimes();
            UpdateCameraTarget();

            _isInitialized = true;
        }

        private void ResolveMissingReferences()
        {
            // 1. Intentar tomar desde NetworkGameManager si existe en escena/DontDestroyOnLoad
            if (NetworkGameManager.Instance != null)
            {
                if (_slimeTransformerPrefab == null)
                    _slimeTransformerPrefab = NetworkGameManager.Instance.SlimeTransformerPrefab;
                if (_slimeAgilePrefab == null)
                    _slimeAgilePrefab = NetworkGameManager.Instance.SlimeAgilePrefab;
                if (_transformerData == null)
                    _transformerData = NetworkGameManager.Instance.TransformerData;
                if (_agileData == null)
                    _agileData = NetworkGameManager.Instance.AgileData;
            }

            // 2. Carga en tiempo de ejecución desde Resources (funciona en WebGL, standalone y editor)
            if (_slimeTransformerPrefab == null)
                _slimeTransformerPrefab = Resources.Load<GameObject>("SinglePlayer/Slime_Transformer");
            if (_slimeAgilePrefab == null)
                _slimeAgilePrefab = Resources.Load<GameObject>("SinglePlayer/Slime_Agile");
            if (_transformerData == null)
                _transformerData = Resources.Load<SlimeData>("SinglePlayer/SlimeData_Transformer");
            if (_agileData == null)
                _agileData = Resources.Load<SlimeData>("SinglePlayer/SlimeData_Agile");

#if UNITY_EDITOR
            // 3. Fallback Editor: carga directa desde AssetDatabase si aún faltan
            if (_slimeTransformerPrefab == null)
                _slimeTransformerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Players/Slime_Transformer.prefab");
            if (_slimeAgilePrefab == null)
                _slimeAgilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Players/Slime_Agile.prefab");
            if (_transformerData == null)
                _transformerData = AssetDatabase.LoadAssetAtPath<SlimeData>(
                    "Assets/_Project/Player Settings/SlimeData_Transformer.asset");
            if (_agileData == null)
                _agileData = AssetDatabase.LoadAssetAtPath<SlimeData>(
                    "Assets/_Project/Player Settings/SlimeData_Agile.asset");
#endif
        }

        private bool ValidatePrefabs()
        {
            if (_slimeTransformerPrefab == null)
            {
                Debug.LogError($"[{nameof(SinglePlayerManager)}] Falta _slimeTransformerPrefab. " +
                               "Asígnalo en el Inspector del SinglePlayerManager o en NetworkGameManager.", this);
                return false;
            }
            if (_slimeAgilePrefab == null)
            {
                Debug.LogError($"[{nameof(SinglePlayerManager)}] Falta _slimeAgilePrefab. " +
                               "Asígnalo en el Inspector del SinglePlayerManager o en NetworkGameManager.", this);
                return false;
            }
            return true;
        }

        private void FindOrSpawnSlimes()
        {
            // 1. Limpiar o destruir slimes inactivos/placeholders pre-colocados en la escena para evitar conflictos
            SlimeMovement[] existingSlimes = FindObjectsByType<SlimeMovement>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var slime in existingSlimes)
            {
                if (slime != null)
                {
                    // Si ya existe una instancia creada por este manager (termina en _1P) y está activa, mantenerla
                    if (slime.gameObject.name.EndsWith("_1P") && slime.gameObject.activeInHierarchy)
                    {
                        PlayerLevelNetworkController ctrl = slime.GetComponent<PlayerLevelNetworkController>();
                        if (ctrl != null)
                        {
                            if (ctrl.SpawnRole == SpawnPlayerRole.Player1 && _transformerInstance == null)
                                _transformerInstance = slime.gameObject;
                            else if (ctrl.SpawnRole == SpawnPlayerRole.Player2 && _agileInstance == null)
                                _agileInstance = slime.gameObject;
                        }
                        continue;
                    }

                    // Destruir placeholders o slimes residuales de la escena (ej: NetworkObjects inactivos en Level2 y Level3)
                    Destroy(slime.gameObject);
                }
            }

            // 2. Spawnear siempre instancias limpias y frescas desde los prefabs
            if (_autoSpawnIfMissing)
            {
                if (_transformerInstance == null)
                    _transformerInstance = SpawnSlime(SpawnPlayerRole.Player1, _slimeTransformerPrefab, _transformerData);

                if (_agileInstance == null)
                    _agileInstance = SpawnSlime(SpawnPlayerRole.Player2, _slimeAgilePrefab, _agileData);
            }
        }

        private GameObject SpawnSlime(SpawnPlayerRole role, GameObject prefab, SlimeData data)
        {
            // Posición por defecto si no hay SpawnPoint
            Vector3 spawnPos = role == SpawnPlayerRole.Player1
                ? new Vector3(-2f, 1f, 0f)
                : new Vector3(2f, 1f, 0f);
            Quaternion spawnRot = Quaternion.identity;

            // Buscar UniversalSpawnPoint en la escena
            if (UniversalSpawnPoint.TryGetActiveSpawnTransform(role, out Vector3 p, out Quaternion r))
            {
                spawnPos = p;
                spawnRot = r;
            }
            else
            {
                UniversalSpawnPoint sp = UniversalSpawnPoint.GetPlayerSpawn(role);
                if (sp != null)
                {
                    spawnPos = sp.Position;
                    spawnRot = sp.Rotation;
                }
            }

            GameObject instance = Instantiate(prefab, spawnPos, spawnRot);
            instance.name = $"{prefab.name}_1P";

            SlimeMovement movement = instance.GetComponent<SlimeMovement>();
            if (movement != null && data != null) movement.InitializeData(data);

            SlimeJump jump = instance.GetComponent<SlimeJump>();
            if (jump != null && data != null) jump.InitializeData(data);

            SlimeStatsModifier stats = instance.GetComponent<SlimeStatsModifier>();
            if (stats != null && data != null) stats.Initialize(data);

            return instance;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ALTERNANCIA DE SLIME Y CÁMARA
        // ─────────────────────────────────────────────────────────────────────

        private void HandleSwitchCharacterRequested()
        {
            if (!IsSinglePlayerActive) return;
            SwitchActiveSlime();
        }

        public void SwitchActiveSlime()
        {
            _activeRole = _activeRole == SpawnPlayerRole.Player1
                ? SpawnPlayerRole.Player2
                : SpawnPlayerRole.Player1;

            ApplyControlToSlimes();
            UpdateCameraTarget();
            ActiveSlimeChanged?.Invoke(_activeRole, ActiveSlime);
        }

        public void SetActiveSlime(SpawnPlayerRole role)
        {
            if (_activeRole == role) return;
            _activeRole = role;
            ApplyControlToSlimes();
            UpdateCameraTarget();
            ActiveSlimeChanged?.Invoke(_activeRole, ActiveSlime);
        }

        private void ApplyControlToSlimes()
        {
            SetSlimeControlled(_transformerInstance, _activeRole == SpawnPlayerRole.Player1);
            SetSlimeControlled(_agileInstance, _activeRole == SpawnPlayerRole.Player2);
        }

        private static void SetSlimeControlled(GameObject slimeObj, bool isControlled)
        {
            if (slimeObj == null) return;
            SlimeInput input = slimeObj.GetComponent<SlimeInput>();
            input?.SetLocallyControlled(isControlled);
        }

        private void UpdateCameraTarget()
        {
            GameObject target = ActiveSlime;
            if (target == null) return;

            if (_cinemachineTargeter == null)
                _cinemachineTargeter = FindFirstObjectByType<CinemachineAutoTargetPlayer>(FindObjectsInactive.Include);

            if (_cinemachineTargeter != null)
            {
                _cinemachineTargeter.SetTarget(target.transform);
            }
            else
            {
                CinemachineCamera cam = FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
                if (cam != null)
                {
                    cam.Follow = target.transform;
                    cam.LookAt = target.transform;
                }
            }
        }
    }
}
