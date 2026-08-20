using System.Collections.Generic;
using Splime.Player;
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
        [SerializeField, Min(2)] private int _requiredPlayers = 2;

        private readonly HashSet<PlayerLevelNetworkController> _playersInside =
            new HashSet<PlayerLevelNetworkController>();

        private bool _levelCompleted;

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
                player.CompleteLevelForAllPlayers();
                return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_levelCompleted || !HasAuthority)
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

        private void OnTriggerExit(Collider other)
        {
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
