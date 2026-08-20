using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using Splime.Player;

namespace Splime.CameraControl
{
    /// <summary>
    /// Component attached to a CinemachineCamera (e.g. FreeLook Camera).
    /// Automatically searches for and targets the local player Slime in the scene.
    /// Works seamlessly in both Netcode multiplayer (targets IsOwner) and offline/editor testing.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [DisallowMultipleComponent]
    public class CinemachineAutoTargetPlayer : MonoBehaviour
    {
        private CinemachineCamera _cinemachineCam;

        private void Awake()
        {
            _cinemachineCam = GetComponent<CinemachineCamera>();
        }

        private void Start()
        {
            TryTargetLocalPlayer();
        }

        private void Update()
        {
            // If the camera loses its target (e.g. player respawned, destroyed, or just spawned), re-acquire it.
            if (_cinemachineCam != null && _cinemachineCam.Follow == null)
            {
                TryTargetLocalPlayer();
            }
        }

        public void TryTargetLocalPlayer()
        {
            if (_cinemachineCam == null) return;

            Transform targetTransform = FindLocalPlayerTransform();

            if (targetTransform != null)
            {
                _cinemachineCam.Follow = targetTransform;
                _cinemachineCam.LookAt = targetTransform;
                Debug.Log($"[{nameof(CinemachineAutoTargetPlayer)}] 🎯 Cámara vinculada exitosamente al jugador local: '{targetTransform.gameObject.name}'.", this);
            }
        }

        private Transform FindLocalPlayerTransform()
        {
            SlimeMovement[] slimes = FindObjectsByType<SlimeMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (slimes == null || slimes.Length == 0)
            {
                return null;
            }

            bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            foreach (var slime in slimes)
            {
                if (slime == null) continue;

                if (isNetworkActive)
                {
                    NetworkObject netObj = slime.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsOwner)
                    {
                        return slime.transform;
                    }
                }
                else
                {
                    // In offline / local testing mode, pick the local input source or the first active slime
                    SlimeInput input = slime.GetComponent<SlimeInput>();
                    if (input == null || input.IsLocalInputSource)
                    {
                        return slime.transform;
                    }
                }
            }

            // Fallback for offline testing if no specific owner found
            return slimes[0].transform;
        }

        public void SetTarget(Transform target)
        {
            if (_cinemachineCam != null && target != null)
            {
                _cinemachineCam.Follow = target;
                _cinemachineCam.LookAt = target;
            }
        }
    }
}
