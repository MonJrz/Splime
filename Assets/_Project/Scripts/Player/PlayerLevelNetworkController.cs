using System;
using Splime.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Splime.Player
{
    /// <summary>
    /// Coordinates level-related actions that must reach the owning player or every client.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerLevelNetworkController : NetworkBehaviour
    {
        [SerializeField] private SpawnPlayerRole _spawnRole = SpawnPlayerRole.Player1;

        private CharacterController _characterController;
        private NetworkTransform _networkTransform;
        private SlimeMovement _movement;
        private SlimeJump _jump;

        public static event Action LevelCompletedReceived;

        public SpawnPlayerRole SpawnRole => _spawnRole;

        private bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _networkTransform = GetComponent<NetworkTransform>();
            _movement = GetComponent<SlimeMovement>();
            _jump = GetComponent<SlimeJump>();
        }

        public void RespawnAtAssignedSpawn()
        {
            UniversalSpawnPoint spawnPoint = UniversalSpawnPoint.GetPlayerSpawn(_spawnRole);
            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerLevelNetworkController)}] No spawn point is configured for {_spawnRole}.",
                    this);
                return;
            }

            if (!IsNetworkSessionActive)
            {
                ApplyRespawn(spawnPoint.Position, spawnPoint.Rotation);
                return;
            }

            if (!IsSpawned || !IsServer)
            {
                return;
            }

            RespawnOwnerRpc(spawnPoint.Position, spawnPoint.Rotation);
        }

        public void CompleteLevelForAllPlayers()
        {
            if (!IsNetworkSessionActive)
            {
                LevelCompletedReceived?.Invoke();
                return;
            }

            if (!IsSpawned || !IsServer)
            {
                return;
            }

            CompleteLevelRpc();
        }

        [Rpc(SendTo.Owner)]
        private void RespawnOwnerRpc(Vector3 position, Quaternion rotation)
        {
            ApplyRespawn(position, rotation);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CompleteLevelRpc()
        {
            LevelCompletedReceived?.Invoke();
        }

        private void ApplyRespawn(Vector3 position, Quaternion rotation)
        {
            _movement?.ResetMotion();
            _jump?.ResetMotion();

            bool controllerWasEnabled = _characterController != null && _characterController.enabled;
            if (controllerWasEnabled)
            {
                _characterController.enabled = false;
            }

            if (IsSpawned &&
                _networkTransform != null &&
                _networkTransform.CanCommitToTransform)
            {
                _networkTransform.Teleport(position, rotation, transform.localScale);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            if (controllerWasEnabled)
            {
                _characterController.enabled = true;
            }
        }
    }
}
