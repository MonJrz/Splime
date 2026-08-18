using Unity.Netcode;
using UnityEngine;
using Splime.Puzzles;

namespace Splime.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class MovingPlatformRider : NetworkBehaviour
    {
        [Header("Platform Detection")]
        [SerializeField] private float _groundCheckDistance = 0.15f; // Distance to check for ground/platform below the player.
        [SerializeField] private float _sphereRadiusFactor = 0.8f; // Factor of the CharacterController's bounds extents to determine the radius of the sphere cast for platform detection.

        private CharacterController _characterController;
        private ModularMover _currentPlatform;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            if (IsSpawned && !IsOwner)
                return;

            DetectPlatformBelow();

            if (_currentPlatform == null)
                return;

            Vector3 platformDelta = _currentPlatform.FrameDelta;

            if (platformDelta.sqrMagnitude <= 0f)
                return;

            _characterController.Move(platformDelta);
        }

        private void DetectPlatformBelow()
        {
            _currentPlatform = null;

            Bounds bounds = _characterController.bounds;

            Vector3 origin = new Vector3(
                bounds.center.x,
                bounds.min.y + 0.05f,
                bounds.center.z
            );
            // Calculate the sphere radius based on the CharacterController's bounds and the specified factor.
            float sphereRadius =
                Mathf.Min(bounds.extents.x, bounds.extents.z)
                * _sphereRadiusFactor;
            // Perform a sphere cast downwards to detect the platform below the player.
            if (Physics.SphereCast(
                    origin,
                    sphereRadius,
                    Vector3.down,
                    out RaycastHit hit,
                    _groundCheckDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                _currentPlatform =
                    hit.collider.GetComponentInParent<ModularMover>();
            }
        }
    }
}