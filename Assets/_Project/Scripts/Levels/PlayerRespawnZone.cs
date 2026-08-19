using Splime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Splime.Levels
{
    /// <summary>
    /// Sends a player that enters this zone back to the spawn point assigned to its role.
    /// In a network session, only the server can request the respawn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerRespawnZone : MonoBehaviour
    {
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

        private void OnTriggerEnter(Collider other)
        {
            if (!HasAuthority)
            {
                return;
            }

            PlayerLevelNetworkController player =
                other.GetComponentInParent<PlayerLevelNetworkController>();

            player?.RespawnAtAssignedSpawn();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
#endif
    }
}
