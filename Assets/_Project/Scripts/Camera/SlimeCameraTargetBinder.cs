using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using Splime.Player;

namespace Splime.CameraControl
{
    /// <summary>
    /// Component attached to Slime player prefabs.
    /// Informs the scene CinemachineCamera to follow this Slime if it is the local player (IsOwner or offline).
    /// </summary>
    [DisallowMultipleComponent]
    public class SlimeCameraTargetBinder : NetworkBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("Transform específico al que debe seguir la cámara. Si se deja vacío, sigue a este GameObject.")]
        [SerializeField] private Transform _targetOverride;

        private SlimeInput _slimeInput;

        private void Awake()
        {
            _slimeInput = GetComponent<SlimeInput>();
        }

        private void Start()
        {
            // Pruebas offline / unijugador
            if (!IsSpawned)
            {
                bool isLocalInput = _slimeInput == null || _slimeInput.IsLocalInputSource;
                if (isLocalInput)
                {
                    BindToActiveCamera();
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                BindToActiveCamera();
            }
        }

        /// <summary>
        /// Busca la CinemachineCamera en la escena y asigna Follow y LookAt a este Slime.
        /// </summary>
        public void BindToActiveCamera()
        {
            CinemachineCamera cinemachineCam = FindAnyObjectByType<CinemachineCamera>();

            if (cinemachineCam != null)
            {
                Transform targetTransform = _targetOverride != null ? _targetOverride : transform;
                cinemachineCam.Follow = targetTransform;
                cinemachineCam.LookAt = targetTransform;
                Debug.Log($"[{nameof(SlimeCameraTargetBinder)}] 🎥 CinemachineCamera '{cinemachineCam.gameObject.name}' vinculada a '{gameObject.name}' (IsOwner: {IsOwner}).", this);
            }
        }
    }
}
