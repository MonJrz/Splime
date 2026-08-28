using System.Collections.Generic;
using Splime.Collectibles;
using Unity.Netcode;
using UnityEngine;

namespace Splime.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerCosmeticController : NetworkBehaviour
    {
        private int _headOcclusionCount;

        [Header("Sockets")]
        [SerializeField] private Transform _headSocket;
        [SerializeField] private Transform _faceSocket;
        [SerializeField] private Transform _neckSocket;

        [Header("Available Cosmetics")]
        [Tooltip("Todas las definiciones que este player puede mostrar.")]
        [SerializeField] private CosmeticDefinition[] _cosmeticDefinitions;

        private readonly Dictionary<CosmeticId, CosmeticDefinition> _definitions =
            new Dictionary<CosmeticId, CosmeticDefinition>();

        // Equipped visual cosmetic instances
        private GameObject _equippedHead;
        private GameObject _equippedFace;
        private GameObject _equippedNeck;

        // Original pickup currently assigned to each slot
        private CosmeticCollectible _headSource;
        private CosmeticCollectible _faceSource;
        private CosmeticCollectible _neckSource;

        private readonly NetworkVariable<int> _headCosmetic =
            new NetworkVariable<int>(
                (int)CosmeticId.None,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _faceCosmetic =
            new NetworkVariable<int>(
                (int)CosmeticId.None,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _neckCosmetic =
            new NetworkVariable<int>(
                (int)CosmeticId.None,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private void Awake()
        {
            BuildDefinitionLookup();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _headCosmetic.OnValueChanged += HandleHeadChanged;
            _faceCosmetic.OnValueChanged += HandleFaceChanged;
            _neckCosmetic.OnValueChanged += HandleNeckChanged;

            // Important: Apply the current values to ensure the visuals are correct on spawn.
            ApplyCosmetic(
                CosmeticSlot.Head,
                (CosmeticId)_headCosmetic.Value);

            ApplyCosmetic(
                CosmeticSlot.Face,
                (CosmeticId)_faceCosmetic.Value);

            ApplyCosmetic(
                CosmeticSlot.Neck,
                (CosmeticId)_neckCosmetic.Value);
        }

        public override void OnNetworkDespawn()
        {
            _headCosmetic.OnValueChanged -= HandleHeadChanged;
            _faceCosmetic.OnValueChanged -= HandleFaceChanged;
            _neckCosmetic.OnValueChanged -= HandleNeckChanged;

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Onlythe server has a collectible.
        /// source is the original pickup instance in the scene.
        /// </summary>
        public void EquipServer(CosmeticId cosmeticId, CosmeticCollectible source)
        {
            if (!IsServer)
                return;

            if (!_definitions.TryGetValue(
                    cosmeticId,
                    out CosmeticDefinition definition))
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerCosmeticController)}] " +
                    $"Cosmetic '{cosmeticId}' not registered in {gameObject.name}.",
                    this);

                return;
            }

            CosmeticSlot slot = definition.Slot;

            // If the player already has a cosmetic in this slot, we need to release it.
            CosmeticCollectible previousSource =
                GetSource(slot);

            if (previousSource != null &&
                previousSource != source)
            {
                previousSource.ReleaseServer();
            }

            SetSource(slot, source);

            switch (slot)
            {
                case CosmeticSlot.Head:
                    _headCosmetic.Value = (int)cosmeticId;
                    break;

                case CosmeticSlot.Face:
                    _faceCosmetic.Value = (int)cosmeticId;
                    break;

                case CosmeticSlot.Neck:
                    _neckCosmetic.Value = (int)cosmeticId;
                    break;
            }

            // Apply the cosmetic locally for immediate feedback.
            ApplyCosmetic(slot, cosmeticId);
        }

        private void HandleHeadChanged(int previousValue, int newValue)
        {
            if (IsServer)
                return;

            ApplyCosmetic(
                CosmeticSlot.Head,
                (CosmeticId)newValue);
        }

        private void HandleFaceChanged(int previousValue, int newValue)
        {
            if (IsServer)
                return;

            ApplyCosmetic(
                CosmeticSlot.Face,
                (CosmeticId)newValue);
        }

        private void HandleNeckChanged(int previousValue, int newValue)
        {
            if (IsServer)
                return;

            ApplyCosmetic(
                CosmeticSlot.Neck,
                (CosmeticId)newValue);
        }

        public void PushHeadOcclusion()
        {
            _headOcclusionCount++;
            RefreshHeadVisibility();
        }

        public void PopHeadOcclusion()
        {
            _headOcclusionCount =
                Mathf.Max(0, _headOcclusionCount - 1);

            RefreshHeadVisibility();
        }

        private void RefreshHeadVisibility()
        {
            if (_equippedHead != null)
            {
                _equippedHead.SetActive(
                    _headOcclusionCount == 0);
            }
        }

        private void ApplyCosmetic(
            CosmeticSlot slot,
            CosmeticId cosmeticId)
        {
            Transform socket = GetSocket(slot);
            if (socket == null)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerCosmeticController)}] " +
                    $"Socket '{slot}' not assigned in {gameObject.name}.",
                    this);

                return;
            }

            ClearSlot(slot);

            if (cosmeticId == CosmeticId.None)
                return;

            if (!_definitions.TryGetValue(
                    cosmeticId,
                    out CosmeticDefinition definition))
            {
                return;
            }

            if (definition.Prefab == null)
                return;

            GameObject instance =
                Instantiate(
                    definition.Prefab,
                    socket, false);

            SetEquippedObject(slot, instance);

            if (slot == CosmeticSlot.Head)
            {
                RefreshHeadVisibility();
            }
        }

        private Transform GetSocket(CosmeticSlot slot)
        {
            return slot switch
            {
                CosmeticSlot.Head => _headSocket,
                CosmeticSlot.Face => _faceSocket,
                CosmeticSlot.Neck => _neckSocket,
                _ => null
            };
        }

        private CosmeticCollectible GetSource(
            CosmeticSlot slot)
        {
            return slot switch
            {
                CosmeticSlot.Head => _headSource,
                CosmeticSlot.Face => _faceSource,
                CosmeticSlot.Neck => _neckSource,
                _ => null
            };
        }

        private void SetSource(
            CosmeticSlot slot,
            CosmeticCollectible source)
        {
            switch (slot)
            {
                case CosmeticSlot.Head:
                    _headSource = source;
                    break;

                case CosmeticSlot.Face:
                    _faceSource = source;
                    break;

                case CosmeticSlot.Neck:
                    _neckSource = source;
                    break;
            }
        }


        private void ClearSlot(CosmeticSlot slot)
        {
            GameObject current = slot switch
            {
                CosmeticSlot.Head => _equippedHead,
                CosmeticSlot.Face => _equippedFace,
                CosmeticSlot.Neck => _equippedNeck,
                _ => null
            };

            if (current != null)
            {
                Destroy(current);
            }

            SetEquippedObject(slot, null);
        }

        private void SetEquippedObject(
            CosmeticSlot slot,
            GameObject instance)
        {
            switch (slot)
            {
                case CosmeticSlot.Head:
                    _equippedHead = instance;
                    break;

                case CosmeticSlot.Face:
                    _equippedFace = instance;
                    break;

                case CosmeticSlot.Neck:
                    _equippedNeck = instance;
                    break;
            }
        }

        private void BuildDefinitionLookup()
        {
            _definitions.Clear();

            if (_cosmeticDefinitions == null)
                return;

            foreach (CosmeticDefinition definition in _cosmeticDefinitions)
            {
                if (definition == null ||
                    definition.Id == CosmeticId.None)
                {
                    continue;
                }

                _definitions[definition.Id] = definition;
            }
        }
    }
}