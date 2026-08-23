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
        [Tooltip("Objeto visual que se ocultará al recoger el collectible. " +
                 "Si queda vacío se intentará usar el primer hijo.")]
        [SerializeField] private GameObject _visual;

        private Collider _trigger;

        private readonly NetworkVariable<bool> _collected =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public bool IsCollected =>
            IsSpawned
                ? _collected.Value
                : !_trigger.enabled;

        private void Awake()
        {
            _trigger = GetComponent<Collider>();

            if (_visual == null && transform.childCount > 0)
            {
                _visual = transform.GetChild(0).gameObject;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _collected.OnValueChanged += HandleCollectedChanged;

            ApplyCollectedState(_collected.Value);
        }

        public override void OnNetworkDespawn()
        {
            _collected.OnValueChanged -= HandleCollectedChanged;

            base.OnNetworkDespawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_cosmetic == null || IsCollected)
                return;

            PlayerCosmeticController player =
                other.GetComponentInParent<PlayerCosmeticController>();

            if (player == null)
                return;

            // Sólo el propietario de ese player solicita recogerlo.
            if (IsSpawned && !player.IsOwner)
                return;

            // ─────────────────────────────
            // OFFLINE
            // ─────────────────────────────
            if (!IsSpawned)
            {
                CollectLocal(player);
                return;
            }

            // ─────────────────────────────
            // ONLINE
            // ─────────────────────────────
            if (IsServer)
            {
                TryCollectServer(player.NetworkObjectId);
            }
            else
            {
                RequestCollectRpc(player.NetworkObjectId);
            }
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestCollectRpc(
            ulong playerNetworkObjectId)
        {
            TryCollectServer(playerNetworkObjectId);
        }

        private void TryCollectServer(
            ulong playerNetworkObjectId)
        {
            if (!IsServer ||
                _collected.Value ||
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

            // IMPORTANTE:
            // primero reservamos el collectible.
            // Así un segundo request ya encontrará true.
            _collected.Value = true;

            player.EquipServer(_cosmetic.Id);

            Debug.Log(
                $"[{nameof(CosmeticCollectible)}] " +
                $"{player.gameObject.name} recogió {_cosmetic.Id}.",
                this);
        }

        private void CollectLocal(
            PlayerCosmeticController player)
        {
            if (player == null ||
                _cosmetic == null ||
                IsCollected)
            {
                return;
            }

            player.EquipLocal(_cosmetic.Id);

            ApplyCollectedState(true);
        }

        private void HandleCollectedChanged(
            bool previousValue,
            bool newValue)
        {
            ApplyCollectedState(newValue);
        }

        private void ApplyCollectedState(bool collected)
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