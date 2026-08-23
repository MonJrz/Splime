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
        [Header("Sockets")]
        [SerializeField] private Transform _headSocket;
        [SerializeField] private Transform _faceSocket;
        [SerializeField] private Transform _neckSocket;

        [Header("Available Cosmetics")]
        [Tooltip("Todas las definiciones que este player puede mostrar.")]
        [SerializeField] private CosmeticDefinition[] _cosmeticDefinitions;

        private readonly Dictionary<CosmeticId, CosmeticDefinition> _definitions =
            new Dictionary<CosmeticId, CosmeticDefinition>();

        private GameObject _equippedHead;
        private GameObject _equippedFace;
        private GameObject _equippedNeck;

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

        public void EquipServer(CosmeticId cosmeticId)
        {
            if (!IsServer)
                return;

            if (!_definitions.TryGetValue(
                    cosmeticId,
                    out CosmeticDefinition definition))
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerCosmeticController)}] " +
                    $"Cosmetic '{cosmeticId}' no está registrado en {gameObject.name}.",
                    this);

                return;
            }

            switch (definition.Slot)
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
        }

        private void HandleHeadChanged(int previousValue, int newValue)
        {
            ApplyCosmetic(
                CosmeticSlot.Head,
                (CosmeticId)newValue);
        }

        private void HandleFaceChanged(int previousValue, int newValue)
        {
            ApplyCosmetic(
                CosmeticSlot.Face,
                (CosmeticId)newValue);
        }

        private void HandleNeckChanged(int previousValue, int newValue)
        {
            ApplyCosmetic(
                CosmeticSlot.Neck,
                (CosmeticId)newValue);
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
                    $"Socket '{slot}' no asignado en {gameObject.name}.",
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
                    socket);

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            SetEquippedObject(slot, instance);
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