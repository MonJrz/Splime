using System;
using System.Collections.Generic;
using Splime.Core;
using Splime.Player;
using Splime.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Levels
{
    /// <summary>
    /// Modo de activación del checkpoint.
    /// </summary>
    public enum CheckpointActivationMode
    {
        /// <summary>
        /// Cada slime activa el checkpoint de forma independiente: solo cambia el spawn del slime que entra.
        /// </summary>
        IndividualPerPlayer = 0,

        /// <summary>
        /// Cualquier slime que entre activa el checkpoint para ambos slimes simultáneamente.
        /// </summary>
        TeamAnyPlayer = 1,

        /// <summary>
        /// Ambos slimes deben estar dentro de la zona al mismo tiempo para activarlo.
        /// </summary>
        TeamAllPlayers = 2
    }

    /// <summary>
    /// Zona de Checkpoint para niveles de Splime (ej. Level 2).
    /// Actualiza los puntos de respawn/spawn de los jugadores al ser activada.
    /// Soporta modo individual (solo el slime que entra cambia su spawn) o modos de equipo.
    /// Compatible tanto con sesiones en red (Netcode for GameObjects) como en modo local/offline.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [SelectionBase]
    public sealed class CheckpointZone : MonoBehaviour
    {
        [Header("Configuración de Checkpoint")]
        [Tooltip("Índice secuencial o prioridad del checkpoint. Un checkpoint con índice mayor no será sobrescrito por uno menor si el jugador retrocede.")]
        [SerializeField, Min(0)] private int _checkpointIndex = 1;

        [Tooltip("Modo de activación: Individual (solo cambia el spawn del slime que entra), TeamAny (uno cambia para ambos), TeamAll (ambos deben estar dentro).")]
        [SerializeField] private CheckpointActivationMode _activationMode = CheckpointActivationMode.IndividualPerPlayer;

        [Tooltip("Número de jugadores requeridos si el modo es TeamAllPlayers.")]
        [SerializeField, Min(1)] private int _requiredPlayerCount = 2;

        [Header("Puntos de Spawn al Reaparecer")]
        [Tooltip("Transform exacto donde reaparecerá el Jugador 1 (Slime Transformador). Si está vacío, se usará la posición de este Checkpoint + Player1 Offset.")]
        [SerializeField] private Transform _player1SpawnPoint;

        [Tooltip("Transform exacto donde reaparecerá el Jugador 2 (Slime Ágil). Si está vacío, se usará la posición de este Checkpoint + Player2 Offset.")]
        [SerializeField] private Transform _player2SpawnPoint;

        [Tooltip("Desplazamiento local para el Jugador 1 si no se asigna un Transform específico.")]
        [SerializeField] private Vector3 _player1Offset = new Vector3(-1.2f, 0f, 0f);

        [Tooltip("Desplazamiento local para el Jugador 2 si no se asigna un Transform específico.")]
        [SerializeField] private Vector3 _player2Offset = new Vector3(1.2f, 0f, 0f);

        [Header("Feedback Visual y Audio")]
        [Tooltip("Beacon visual opcional que representa el estado de activación del checkpoint.")]
        [SerializeField] private CheckpointBeaconVisual _checkpointBeacon;
        [Tooltip("Luz opcional que cambiará de color o intensidad al activarse.")]
        [SerializeField] private Light _checkpointLight;

        [Tooltip("Color de la luz cuando ningún jugador ha activado el checkpoint.")]
        [SerializeField] private Color _inactiveLightColor = new Color(0.137f, 0.565f, 0.537f, 1f); // Cian/Teal

        [Tooltip("Color de la luz cuando al menos un jugador ha activado el checkpoint.")]
        [SerializeField] private Color _partialActiveLightColor = new Color(0.9f, 0.85f, 0.2f, 1f); // Amarillo cálido

        [Tooltip("Color de la luz cuando todos los jugadores han activado el checkpoint.")]
        [SerializeField] private Color _fullActiveLightColor = new Color(0.2f, 1f, 0.4f, 1f); // Verde brillante

        [Tooltip("Sistema de partículas que se emitirá al activar el checkpoint.")]
        [SerializeField] private ParticleSystem _activationVfx;

        [Tooltip("Sonido que se reproducirá al activarse.")]
        [SerializeField] private AudioClip _activationSfx;

        [Tooltip("AudioSource para reproducir el sonido. Si está vacío, buscará o creará uno.")]
        [SerializeField] private AudioSource _audioSource;

        [Header("UI Feedback")]
        [Tooltip("Controlador de interfaz de nivel. Si no se asigna, se buscará automáticamente en la escena.")]
        [SerializeField] private LevelUIController _levelUIController;

        [Tooltip("Duración en segundos del banner de Checkpoint en pantalla.")]
        [SerializeField, Min(0.5f)] private float _uiDisplayDuration = 2f;

        [Header("Eventos")]
        [Tooltip("Evento disparado al activarse este checkpoint para cualquier jugador.")]
        [SerializeField] private UnityEvent _onCheckpointActivated;

        private readonly HashSet<PlayerLevelNetworkController> _playersInside = new HashSet<PlayerLevelNetworkController>();
        private readonly HashSet<SpawnPlayerRole> _activatedRoles = new HashSet<SpawnPlayerRole>();

        public int CheckpointIndex => _checkpointIndex;
        public CheckpointActivationMode ActivationMode => _activationMode;

        public bool IsFullyActivated => _activatedRoles.Count >= 2;
        public bool IsRoleActivated(SpawnPlayerRole role) => _activatedRoles.Contains(role);

        private bool HasAuthority
        {
            get
            {
                NetworkManager networkManager = NetworkManager.Singleton;
                return networkManager == null ||
                       !networkManager.IsListening ||
                       networkManager.IsServer;
            }
        }

        private bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private void Awake()
        {
            if (_levelUIController == null)
            {
                _levelUIController = FindFirstObjectByType<LevelUIController>(FindObjectsInactive.Include);
            }

            if (_audioSource == null && _activationSfx != null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0.5f;
                }
            }

            if (_checkpointLight != null)
            {
                _checkpointLight.color = _inactiveLightColor;
            }

            RefreshCheckpointVisual();
        }

        private void OnEnable()
        {
            PlayerLevelNetworkController.CheckpointActivatedReceived += HandleNetworkCheckpointActivated;
        }

        private void OnDisable()
        {
            PlayerLevelNetworkController.CheckpointActivatedReceived -= HandleNetworkCheckpointActivated;
            _playersInside.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerLevelNetworkController player = other.GetComponentInParent<PlayerLevelNetworkController>();
            if (player == null)
            {
                return;
            }

            if (!_playersInside.Contains(player))
            {
                _playersInside.Add(player);
            }

            EvaluateActivation(player);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerLevelNetworkController player = other.GetComponentInParent<PlayerLevelNetworkController>();
            if (player != null)
            {
                _playersInside.Remove(player);
            }
        }

        private void EvaluateActivation(PlayerLevelNetworkController triggeringPlayer)
        {
            // Limpiar referencias nulas
            _playersInside.RemoveWhere(p => p == null);

            // Solo el servidor en red o localmente decide la activación
            if (!HasAuthority)
            {
                return;
            }

            SpawnPlayerRole role = triggeringPlayer.SpawnRole;

            // En modo individual: verificar si este jugador específico ya tiene un checkpoint igual o superior
            if (_activationMode == CheckpointActivationMode.IndividualPerPlayer)
            {
                int currentRoleIndex = UniversalSpawnPoint.GetPlayerActiveCheckpointIndex(role);
                if (_checkpointIndex <= currentRoleIndex && _activatedRoles.Contains(role))
                {
                    return; // Este jugador ya registró este checkpoint o uno más avanzado
                }

                ActivateForPlayer(triggeringPlayer);
                return;
            }

            // En modo TeamAllPlayers: verificar que ambos estén dentro
            if (_activationMode == CheckpointActivationMode.TeamAllPlayers)
            {
                if (_playersInside.Count < _requiredPlayerCount)
                {
                    return;
                }

                if (_activatedRoles.Contains(SpawnPlayerRole.Player1) && _activatedRoles.Contains(SpawnPlayerRole.Player2))
                {
                    return;
                }

                ActivateForTeam(triggeringPlayer);
                return;
            }

            // En modo TeamAnyPlayer: cualquiera activa para ambos
            if (_activationMode == CheckpointActivationMode.TeamAnyPlayer)
            {
                if (_activatedRoles.Contains(SpawnPlayerRole.Player1) && _activatedRoles.Contains(SpawnPlayerRole.Player2))
                {
                    return;
                }

                if (_checkpointIndex < UniversalSpawnPoint.CurrentActiveCheckpointIndex)
                {
                    return;
                }

                ActivateForTeam(triggeringPlayer);
            }
        }

        /// <summary>
        /// Activa el checkpoint ÚNICAMENTE para el jugador que entró (Modo Individual).
        /// </summary>
        public void ActivateForPlayer(PlayerLevelNetworkController player)
        {
            SpawnPlayerRole role = player.SpawnRole;
            _activatedRoles.Add(role);

            var (spawnPos, spawnRot) = GetSpawnForRole(role);
            UniversalSpawnPoint.SetActivePlayerSpawn(role, spawnPos, spawnRot, _checkpointIndex);

            // Sincronizar por red
            if (IsNetworkSessionActive)
            {
                player.BroadcastCheckpointActivated(_checkpointIndex, role);
            }

            PlayFeedback(role);
        }

        /// <summary>
        /// Activa el checkpoint para TODOS los jugadores del equipo (Modo Team).
        /// </summary>
        public void ActivateForTeam(PlayerLevelNetworkController triggeringPlayer = null)
        {
            _activatedRoles.Add(SpawnPlayerRole.Player1);
            _activatedRoles.Add(SpawnPlayerRole.Player2);

            var (p1Pos, p1Rot) = GetSpawnForRole(SpawnPlayerRole.Player1);
            var (p2Pos, p2Rot) = GetSpawnForRole(SpawnPlayerRole.Player2);

            UniversalSpawnPoint.SetActivePlayerSpawn(SpawnPlayerRole.Player1, p1Pos, p1Rot, _checkpointIndex);
            UniversalSpawnPoint.SetActivePlayerSpawn(SpawnPlayerRole.Player2, p2Pos, p2Rot, _checkpointIndex);

            if (IsNetworkSessionActive)
            {
                if (triggeringPlayer != null)
                {
                    triggeringPlayer.BroadcastCheckpointActivated(_checkpointIndex, SpawnPlayerRole.Player1);
                    triggeringPlayer.BroadcastCheckpointActivated(_checkpointIndex, SpawnPlayerRole.Player2);
                }
                else
                {
                    PlayerLevelNetworkController anyPlayer = FindFirstObjectByType<PlayerLevelNetworkController>();
                    anyPlayer?.BroadcastCheckpointActivated(_checkpointIndex, SpawnPlayerRole.Player1);
                }
            }

            PlayFeedback(SpawnPlayerRole.Player1);
            PlayFeedback(SpawnPlayerRole.Player2);
        }

        private void HandleNetworkCheckpointActivated(int activatedIndex, SpawnPlayerRole role)
        {
            if (activatedIndex == _checkpointIndex)
            {
                _activatedRoles.Add(role);

                var (spawnPos, spawnRot) = GetSpawnForRole(role);
                UniversalSpawnPoint.SetActivePlayerSpawn(role, spawnPos, spawnRot, _checkpointIndex);

                PlayFeedback(role);
            }
        }

        private void RefreshCheckpointVisual()
        {
            Color stateColor;

            if (_activatedRoles.Count >= 2)
            {
                stateColor = _fullActiveLightColor;
            }
            else if (_activatedRoles.Count > 0)
            {
                stateColor = _partialActiveLightColor;
            }
            else
            {
                stateColor = _inactiveLightColor;
            }

            if (_checkpointLight != null)
            {
                _checkpointLight.color = stateColor;
            }

            _checkpointBeacon?.SetColor(stateColor);
        }

        private void PlayFeedback(SpawnPlayerRole activatedRole)
        {
            // 1. Feedback visual del estado
            RefreshCheckpointVisual();

            // 2. Partículas
            if (_activationVfx != null)
            {
                _activationVfx.Play();
            }

            // 3. Audio
            if (_audioSource != null && _activationSfx != null)
            {
                _audioSource.PlayOneShot(_activationSfx);
            }

            // 4. UI Popup (solo para el jugador local o en solitario)
            if (_levelUIController == null)
            {
                _levelUIController = FindFirstObjectByType<LevelUIController>(FindObjectsInactive.Include);
            }

            _levelUIController?.ShowCheckpoint(_uiDisplayDuration);

            // 5. UnityEvent
            _onCheckpointActivated?.Invoke();
        }

        public (Vector3 position, Quaternion rotation) GetSpawnForRole(SpawnPlayerRole role)
        {
            if (role == SpawnPlayerRole.Player1)
            {
                Vector3 pos = _player1SpawnPoint != null ? _player1SpawnPoint.position : transform.TransformPoint(_player1Offset);
                Quaternion rot = _player1SpawnPoint != null ? _player1SpawnPoint.rotation : transform.rotation;
                return (pos, rot);
            }
            else
            {
                Vector3 pos = _player2SpawnPoint != null ? _player2SpawnPoint.position : transform.TransformPoint(_player2Offset);
                Quaternion rot = _player2SpawnPoint != null ? _player2SpawnPoint.rotation : transform.rotation;
                return (pos, rot);
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            _checkpointLight = GetComponentInChildren<Light>();
            _activationVfx = GetComponentInChildren<ParticleSystem>();
        }

        private void OnDrawGizmos()
        {
            // Zona del trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = IsFullyActivated
                    ? new Color(0.2f, 1f, 0.4f, 0.25f)
                    : (_activatedRoles.Count > 0
                        ? new Color(0.9f, 0.85f, 0.2f, 0.22f)
                        : new Color(0.14f, 0.56f, 0.54f, 0.2f));

                if (col is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.DrawWireCube(box.center, box.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
                }
            }

            // Dibujar punto de spawn para Jugador 1 (Naranja / Transformer)
            Vector3 p1Pos = _player1SpawnPoint != null ? _player1SpawnPoint.position : transform.TransformPoint(_player1Offset);
            Quaternion p1Rot = _player1SpawnPoint != null ? _player1SpawnPoint.rotation : transform.rotation;
            bool p1Active = _activatedRoles.Contains(SpawnPlayerRole.Player1);
            DrawPlayerSpawnGizmo(p1Pos, p1Rot, p1Active ? new Color(1f, 0.6f, 0f, 1f) : new Color(1f, 0.6f, 0f, 0.4f), "P1 (Transformer)");

            // Dibujar punto de spawn para Jugador 2 (Celeste / Agile)
            Vector3 p2Pos = _player2SpawnPoint != null ? _player2SpawnPoint.position : transform.TransformPoint(_player2Offset);
            Quaternion p2Rot = _player2SpawnPoint != null ? _player2SpawnPoint.rotation : transform.rotation;
            bool p2Active = _activatedRoles.Contains(SpawnPlayerRole.Player2);
            DrawPlayerSpawnGizmo(p2Pos, p2Rot, p2Active ? new Color(0.2f, 0.8f, 1f, 1f) : new Color(0.2f, 0.8f, 1f, 0.4f), "P2 (Agile)");

            // Líneas de conexión desde el centro del checkpoint hacia los spawns
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, p1Pos);
            Gizmos.DrawLine(transform.position, p2Pos);
        }

        private void DrawPlayerSpawnGizmo(Vector3 pos, Quaternion rot, Color color, string label)
        {
            Gizmos.color = color;
            Vector3 center = pos + Vector3.up * 0.4f;
            Gizmos.DrawSphere(center, 0.35f);
            Gizmos.DrawWireSphere(center, 0.4f);

            // Flecha de orientación
            Vector3 forward = rot * Vector3.forward;
            Vector3 forwardEnd = center + forward * 0.9f;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(center, forwardEnd);
            Gizmos.DrawLine(forwardEnd, forwardEnd - forward * 0.25f + (rot * Vector3.right) * 0.15f);
            Gizmos.DrawLine(forwardEnd, forwardEnd - forward * 0.25f - (rot * Vector3.right) * 0.15f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(center + Vector3.up * 0.45f, $"[{label}] Checkpoint #{_checkpointIndex}");
#endif
        }
#endif
    }
}
