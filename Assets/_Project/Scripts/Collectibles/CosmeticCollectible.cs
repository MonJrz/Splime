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

        private bool _collected;

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();

            if (!trigger.isTrigger)
            {
                Debug.LogWarning(
                    $"[{nameof(CosmeticCollectible)}] " +
                    $"Collider de '{gameObject.name}' debe usar Is Trigger.",
                    this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerCosmeticController player =
                other.GetComponentInParent<PlayerCosmeticController>();

            if (player == null)
                return;

            if (!player.IsOwner)
                return;

            if (!IsSpawned)
            {
                TryCollectLocal(player);
                return;
            }

            RequestCollectRpc(
                player.NetworkObjectId);
        }

        private void TryCollectLocal(
            PlayerCosmeticController player)
        {
            if (_collected ||
                _cosmetic == null ||
                player == null)
            {
                return;
            }

            _collected = true;

            // Offline no tenemos Server/NetworkVariable.
            // En esta primera versión online es el caso principal.
            Debug.Log(
                $"[{nameof(CosmeticCollectible)}] " +
                $"{player.gameObject.name} recogió {_cosmetic.name}.",
                this);

            gameObject.SetActive(false);
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestCollectRpc(
            ulong playerNetworkObjectId)
        {
            if (!IsServer ||
                _collected ||
                _cosmetic == null)
            {
                return;
            }

            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(
                        playerNetworkObjectId,
                        out NetworkObject playerNetworkObject))
            {
                return;
            }

            PlayerCosmeticController player =
                playerNetworkObject.GetComponent<PlayerCosmeticController>();

            if (player == null)
                return;

            _collected = true;

            player.EquipServer(_cosmetic.Id);

            NetworkObject.Despawn(true);

            Debug.Log(
                $"[{nameof(CosmeticCollectible)}] " +
                $"{player.gameObject.name} recogió {_cosmetic.Id}.",
                this);
        }
    }
}