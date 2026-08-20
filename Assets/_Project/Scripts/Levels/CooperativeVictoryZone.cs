using System.Collections.Generic;
using Splime.Player;
using Splime.UI;
using Unity.Netcode;
using UnityEngine;

namespace Splime.Levels
{
    /// <summary>
    /// Completes the level once the required number of distinct players remain inside the zone.
    /// In a network session, only the server evaluates the condition.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CooperativeVictoryZone : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField, Min(2)] private int _requiredPlayers = 2;

        [Header("Presentation")]
        [SerializeField] private LevelUIController _levelUIController;
        [Tooltip("Message displayed on screen when the local player is inside the victory zone.")]
        [TextArea(2, 4)]
        [SerializeField] private string _promptMessage = "Both players must be in the victory zone to complete the level";

        private readonly HashSet<PlayerLevelNetworkController> _playersInside =
            new HashSet<PlayerLevelNetworkController>();

        private readonly HashSet<Collider> _localCollidersInside =
            new HashSet<Collider>();

        private SlimeInput _localPlayerInside;
        private bool _levelCompleted;

        public string PromptMessage
        {
            get => _promptMessage;
            set => _promptMessage = value;
        }

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

        private void Awake()
        {
            if (_levelUIController == null)
            {
                _levelUIController = FindFirstObjectByType<LevelUIController>(FindObjectsInactive.Include);
            }
        }

        private void Update()
        {
            if (_levelCompleted || _localPlayerInside == null || _localCollidersInside.Count == 0)
            {
                return;
            }

            _levelUIController?.ShowInteractionPrompt(_promptMessage);
        }

        private void FixedUpdate()
        {
            if (_levelCompleted || !HasAuthority)
            {
                return;
            }

            _playersInside.RemoveWhere(player => player == null ||
                (NetworkManager.Singleton != null &&
                 NetworkManager.Singleton.IsListening &&
                 !player.IsSpawned));

            if (_playersInside.Count < _requiredPlayers)
            {
                return;
            }

            foreach (PlayerLevelNetworkController player in _playersInside)
            {
                _levelCompleted = true;
                _levelUIController?.HideInteractionPrompt();
                player.CompleteLevelForAllPlayers();
                return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_levelCompleted)
            {
                return;
            }

            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput != null && slimeInput.IsLocalInputSource)
            {
                _localCollidersInside.Add(other);
                _localPlayerInside = slimeInput;
                _levelUIController?.ShowInteractionPrompt(_promptMessage);
            }

            if (!HasAuthority)
            {
                return;
            }

            PlayerLevelNetworkController player =
                other.GetComponentInParent<PlayerLevelNetworkController>();

            if (player != null)
            {
                _playersInside.Add(player);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_levelCompleted)
            {
                return;
            }

            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput != null && slimeInput.IsLocalInputSource)
            {
                _localCollidersInside.Add(other);
                _localPlayerInside = slimeInput;
                _levelUIController?.ShowInteractionPrompt(_promptMessage);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput != null && slimeInput.IsLocalInputSource)
            {
                _localCollidersInside.Remove(other);
                _localCollidersInside.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

                if (_localCollidersInside.Count == 0)
                {
                    _localPlayerInside = null;
                    _levelUIController?.HideInteractionPrompt();
                }
            }

            if (!HasAuthority)
            {
                return;
            }

            PlayerLevelNetworkController player =
                other.GetComponentInParent<PlayerLevelNetworkController>();

            if (player != null)
            {
                _playersInside.Remove(player);
            }
        }

        private void OnDisable()
        {
            _playersInside.Clear();
            _localCollidersInside.Clear();
            if (_localPlayerInside != null)
            {
                _localPlayerInside = null;
                _levelUIController?.HideInteractionPrompt();
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnValidate()
        {
            _requiredPlayers = Mathf.Max(2, _requiredPlayers);
        }
#endif
    }
}

