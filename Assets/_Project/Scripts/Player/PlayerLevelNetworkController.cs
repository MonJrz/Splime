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
        private enum LevelOutcome
        {
            None,
            Completed,
            Failed
        }

        [SerializeField] private SpawnPlayerRole _spawnRole = SpawnPlayerRole.Player1;

        private static LevelOutcome _levelOutcome;
        private static bool _outcomeEventRaised;

        private CharacterController _characterController;
        private NetworkTransform _networkTransform;
        private SlimeMovement _movement;
        private SlimeJump _jump;

        public static event Action LevelCompletedReceived;
        public static event Action LevelFailedReceived;
        public static event Action<int> LevelTimerUpdatedReceived;
        public static event Action AvailableContentEndedReceived;

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

        public static void ResetLevelState()
        {
            _levelOutcome = LevelOutcome.None;
            _outcomeEventRaised = false;
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
            if (!CanBroadcastLevelState() || !TryClaimOutcome(LevelOutcome.Completed))
            {
                return;
            }

            if (!IsNetworkSessionActive)
            {
                PublishOutcome(LevelOutcome.Completed);
                return;
            }

            CompleteLevelRpc();
        }

        public void FailLevelForAllPlayers()
        {
            if (!CanBroadcastLevelState() || !TryClaimOutcome(LevelOutcome.Failed))
            {
                return;
            }

            if (!IsNetworkSessionActive)
            {
                PublishOutcome(LevelOutcome.Failed);
                return;
            }

            FailLevelRpc();
        }

        public void SyncLevelTimerForAllPlayers(int remainingSeconds)
        {
            if (!CanBroadcastLevelState() || _levelOutcome != LevelOutcome.None)
            {
                return;
            }

            int clampedSeconds = Mathf.Max(0, remainingSeconds);
            if (!IsNetworkSessionActive)
            {
                LevelTimerUpdatedReceived?.Invoke(clampedSeconds);
                return;
            }

            SyncLevelTimerRpc(clampedSeconds);
        }

        public void ShowAvailableContentEndForAllPlayers()
        {
            if (!CanBroadcastLevelState())
            {
                return;
            }

            if (!IsNetworkSessionActive)
            {
                AvailableContentEndedReceived?.Invoke();
                return;
            }

            ShowAvailableContentEndRpc();
        }

        [Rpc(SendTo.Owner)]
        private void RespawnOwnerRpc(Vector3 position, Quaternion rotation)
        {
            ApplyRespawn(position, rotation);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CompleteLevelRpc()
        {
            PublishOutcome(LevelOutcome.Completed);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void FailLevelRpc()
        {
            PublishOutcome(LevelOutcome.Failed);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SyncLevelTimerRpc(int remainingSeconds)
        {
            if (_levelOutcome == LevelOutcome.None)
            {
                LevelTimerUpdatedReceived?.Invoke(Mathf.Max(0, remainingSeconds));
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ShowAvailableContentEndRpc()
        {
            AvailableContentEndedReceived?.Invoke();
        }

        private bool CanBroadcastLevelState()
        {
            return !IsNetworkSessionActive || (IsSpawned && IsServer);
        }

        private static bool TryClaimOutcome(LevelOutcome outcome)
        {
            if (_levelOutcome != LevelOutcome.None)
            {
                return false;
            }

            _levelOutcome = outcome;
            return true;
        }

        private static void PublishOutcome(LevelOutcome outcome)
        {
            if ((_levelOutcome != LevelOutcome.None && _levelOutcome != outcome) || _outcomeEventRaised)
            {
                return;
            }

            _levelOutcome = outcome;
            _outcomeEventRaised = true;

            if (outcome == LevelOutcome.Completed)
            {
                LevelCompletedReceived?.Invoke();
            }
            else
            {
                LevelFailedReceived?.Invoke();
            }
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
