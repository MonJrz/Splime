using Splime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Splime.Collectibles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkObject))]
    public class CosmeticCollectible : NetworkBehaviour
    {
        [Header("Cosmetic")]
        [SerializeField] private CosmeticDefinition _cosmetic;

        [Header("Presentation")]
        [Tooltip("Raíz visual del pickup que se oculta al recogerlo.")]
        [SerializeField] private GameObject _visual;

        private Collider _trigger;

        // Offline/local.
        private bool _localCollected;

        // Online.
        private readonly NetworkVariable<bool> _networkCollected =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private bool IsNetworkSessionActive =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening;

        public bool IsCollected =>
            IsNetworkSessionActive
                ? _networkCollected.Value
                : _localCollected;

        private void Awake()
        {
            _trigger = GetComponent<Collider>();

            if (_visual == null &&
                transform.childCount > 0)
            {
                _visual =
                    transform.GetChild(0).gameObject;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _networkCollected.OnValueChanged +=
                HandleCollectedChanged;

            ApplyCollectedState(
                _networkCollected.Value);
        }

        public override void OnNetworkDespawn()
        {
            _networkCollected.OnValueChanged -=
                HandleCollectedChanged;

            base.OnNetworkDespawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerCosmeticController player =
                other.GetComponentInParent<PlayerCosmeticController>();

            if (player == null ||
                _cosmetic == null ||
                IsCollected)
            {
                return;
            }

            // ─────────────────────────────
            // OFFLINE
            // ─────────────────────────────
            if (!IsNetworkSessionActive)
            {
                TryCollectLocal(player);
                return;
            }

            // On a network session, the collectible is only collected by the server.
            if (!IsSpawned)
            {
                Debug.LogWarning(
                    $"[{nameof(CosmeticCollectible)}] " +
                    $"{gameObject.name} está en una sesión Netcode " +
                    $"pero su NetworkObject no está spawneado.",
                    this);

                return;
            }

            // Only the server can collect the collectible.
            if (!player.IsOwner)
                return;

            if (IsServer)
            {
                TryCollectServer(
                    player.NetworkObjectId);
            }
            else
            {
                RequestCollectRpc(
                    player.NetworkObjectId);
            }
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestCollectRpc(
            ulong playerNetworkObjectId)
        {
            TryCollectServer(
                playerNetworkObjectId);
        }

        private void TryCollectServer(
            ulong playerNetworkObjectId)
        {
            if (!IsServer ||
                _networkCollected.Value ||
                _cosmetic == null)
            {
                return;
            }

            if (NetworkManager.Singleton == null)
                return;

            if (!NetworkManager.Singleton
                    .SpawnManager
                    .SpawnedObjects
                    .TryGetValue(
                        playerNetworkObjectId,
                        out NetworkObject playerNetworkObject))
            {
                return;
            }

            PlayerCosmeticController player =
                playerNetworkObject
                    .GetComponent<PlayerCosmeticController>();

            if (player == null)
                return;

            // Server collects the collectible and equips it to the player.
            _networkCollected.Value = true;

            player.EquipServer(
                _cosmetic.Id,
                this);

            Debug.Log(
                $"[{nameof(CosmeticCollectible)}] " +
                $"{player.gameObject.name} picked up {_cosmetic.Id}.",
                this);
        }

        private void TryCollectLocal(
            PlayerCosmeticController player)
        {
            if (_localCollected ||
                player == null ||
                _cosmetic == null)
            {
                return;
            }

            _localCollected = true;

            ApplyCollectedState(true);

            Debug.Log(
                $"[{nameof(CosmeticCollectible)}] " +
                $"{player.gameObject.name} picked up {_cosmetic.Id} LOCAL.",
                this);
        }

        /// <summary>
        /// Make the collectible available again. 
        /// Server just needs to call it.
        /// </summary>
        public void ReleaseServer()
        {
            if (!IsServer)
                return;

            if (!_networkCollected.Value)
                return;

            _networkCollected.Value = false;

            Debug.Log(
                $"[{nameof(CosmeticCollectible)}] " +
                $"{gameObject.name} is available again.",
                this);
        }

        private void HandleCollectedChanged(
            bool previousValue,
            bool newValue)
        {
            ApplyCollectedState(newValue);
        }

        private void ApplyCollectedState(
            bool collected)
        {
            if (_visual != null)
            {
                _visual.SetActive(!collected);
            }

            if (_trigger != null)
            {
                _trigger.enabled = !collected;
            }
        }
    }
}