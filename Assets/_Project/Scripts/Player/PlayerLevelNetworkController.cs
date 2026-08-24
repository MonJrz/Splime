using System;
using System.Collections.Generic;
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
        private const int InactiveDialoguePage = -1;

        private enum LevelOutcome
        {
            None,
            Completed,
            Failed
        }

        [SerializeField] private SpawnPlayerRole _spawnRole = SpawnPlayerRole.Player1;

        private static LevelOutcome _levelOutcome;
        private static bool _outcomeEventRaised;
        private static readonly HashSet<ulong> _sharedDialogueReadyClients = new();
        private static int _sharedDialoguePageIndex = InactiveDialoguePage;
        private static int _sharedDialoguePageCount;
        private static bool _sharedDialogueCompleted;

        private CharacterController _characterController;
        private NetworkTransform _networkTransform;
        private SlimeMovement _movement;
        private SlimeJump _jump;

        public static event Action LevelCompletedReceived;
        public static event Action LevelFailedReceived;
        public static event Action<int> LevelTimerUpdatedReceived;
        public static event Action AvailableContentEndedReceived;
        public static event Action<int, SpawnPlayerRole> CheckpointActivatedReceived;
        public static event Action<int> SharedDialoguePageChangedReceived;
        public static event Action SharedDialogueCompletedReceived;

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
            _sharedDialogueReadyClients.Clear();
            _sharedDialoguePageIndex = InactiveDialoguePage;
            _sharedDialoguePageCount = 0;
            _sharedDialogueCompleted = false;
            UniversalSpawnPoint.ResetPlayerSpawns();
        }

        public void RespawnAtAssignedSpawn()
        {
            Vector3 spawnPos;
            Quaternion spawnRot;

            if (UniversalSpawnPoint.TryGetActiveSpawnTransform(_spawnRole, out Vector3 activePos, out Quaternion activeRot))
            {
                spawnPos = activePos;
                spawnRot = activeRot;
            }
            else
            {
                UniversalSpawnPoint spawnPoint = UniversalSpawnPoint.GetPlayerSpawn(_spawnRole);
                if (spawnPoint == null)
                {
                    Debug.LogError(
                        $"[{nameof(PlayerLevelNetworkController)}] No spawn point is configured for {_spawnRole}.",
                        this);
                    return;
                }

                spawnPos = spawnPoint.Position;
                spawnRot = spawnPoint.Rotation;
            }

            if (!IsNetworkSessionActive)
            {
                ApplyRespawn(spawnPos, spawnRot);
                return;
            }

            if (!IsSpawned || !IsServer)
            {
                return;
            }

            RespawnOwnerRpc(spawnPos, spawnRot);
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

        public void BroadcastCheckpointActivated(int checkpointIndex, SpawnPlayerRole role)
        {
            if (!CanBroadcastLevelState())
            {
                return;
            }

            if (!IsNetworkSessionActive)
            {
                CheckpointActivatedReceived?.Invoke(checkpointIndex, role);
                return;
            }

            BroadcastCheckpointActivatedRpc(checkpointIndex, role);
        }

        public bool MarkSharedDialogueReady(int pageCount)
        {
            if (pageCount <= 0)
            {
                return false;
            }

            if (!IsNetworkSessionActive)
            {
                _sharedDialoguePageCount = pageCount;
                _sharedDialoguePageIndex = 0;
                _sharedDialogueCompleted = false;
                SharedDialoguePageChangedReceived?.Invoke(0);
                return true;
            }

            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            if (IsServer)
            {
                RegisterSharedDialogueReady(NetworkManager.Singleton.LocalClientId, pageCount);
            }
            else
            {
                MarkSharedDialogueReadyRpc(pageCount);
            }

            return true;
        }

        public bool RequestSharedDialogueAdvance(int expectedPageIndex)
        {
            if (!IsNetworkSessionActive)
            {
                TryAdvanceSharedDialogue(expectedPageIndex);
                return true;
            }

            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            if (IsServer)
            {
                TryAdvanceSharedDialogue(expectedPageIndex);
            }
            else
            {
                RequestSharedDialogueAdvanceRpc(expectedPageIndex);
            }

            return true;
        }

        public bool RequestSharedDialogueSkip()
        {
            if (!IsNetworkSessionActive)
            {
                CompleteSharedDialogue();
                return true;
            }

            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            if (IsServer)
            {
                CompleteSharedDialogue();
            }
            else
            {
                RequestSharedDialogueSkipRpc();
            }

            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastCheckpointActivatedRpc(int checkpointIndex, SpawnPlayerRole role)
        {
            CheckpointActivatedReceived?.Invoke(checkpointIndex, role);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void MarkSharedDialogueReadyRpc(
            int pageCount,
            RpcParams rpcParams = default)
        {
            RegisterSharedDialogueReady(rpcParams.Receive.SenderClientId, pageCount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestSharedDialogueAdvanceRpc(int expectedPageIndex)
        {
            TryAdvanceSharedDialogue(expectedPageIndex);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestSharedDialogueSkipRpc()
        {
            CompleteSharedDialogue();
        }

        [Rpc(SendTo.NotServer)]
        private void BroadcastSharedDialoguePageRpc(int pageIndex)
        {
            SharedDialoguePageChangedReceived?.Invoke(pageIndex);
        }

        [Rpc(SendTo.NotServer)]
        private void BroadcastSharedDialogueCompletedRpc()
        {
            SharedDialogueCompletedReceived?.Invoke();
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

        private void RegisterSharedDialogueReady(ulong clientId, int pageCount)
        {
            if (!IsServer ||
                pageCount <= 0 ||
                _sharedDialogueCompleted ||
                _sharedDialoguePageIndex >= 0)
            {
                return;
            }

            if (_sharedDialoguePageCount == 0)
            {
                _sharedDialoguePageCount = pageCount;
            }
            else if (_sharedDialoguePageCount != pageCount)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerLevelNetworkController)}] Clients reported different dialogue page counts.",
                    this);
                return;
            }

            _sharedDialogueReadyClients.Add(clientId);

            if (!AreAllConnectedClientsReadyForDialogue())
            {
                return;
            }

            _sharedDialoguePageIndex = 0;
            PublishSharedDialoguePage(0);
        }

        private static bool AreAllConnectedClientsReadyForDialogue()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return true;
            }

            if (networkManager.ConnectedClientsIds.Count == 0)
            {
                return false;
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (!_sharedDialogueReadyClients.Contains(clientId))
                {
                    return false;
                }
            }

            return true;
        }

        private void TryAdvanceSharedDialogue(int expectedPageIndex)
        {
            if (_sharedDialogueCompleted ||
                _sharedDialoguePageIndex < 0 ||
                _sharedDialoguePageIndex != expectedPageIndex)
            {
                return;
            }

            int nextPageIndex = _sharedDialoguePageIndex + 1;
            if (nextPageIndex >= _sharedDialoguePageCount)
            {
                CompleteSharedDialogue();
                return;
            }

            _sharedDialoguePageIndex = nextPageIndex;
            PublishSharedDialoguePage(nextPageIndex);
        }

        private void CompleteSharedDialogue()
        {
            if (_sharedDialogueCompleted || _sharedDialoguePageIndex < 0)
            {
                return;
            }

            _sharedDialogueCompleted = true;
            _sharedDialoguePageIndex = InactiveDialoguePage;
            SharedDialogueCompletedReceived?.Invoke();

            if (IsNetworkSessionActive)
            {
                BroadcastSharedDialogueCompletedRpc();
            }
        }

        private void PublishSharedDialoguePage(int pageIndex)
        {
            SharedDialoguePageChangedReceived?.Invoke(pageIndex);

            if (IsNetworkSessionActive)
            {
                BroadcastSharedDialoguePageRpc(pageIndex);
            }
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
