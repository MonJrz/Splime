using Splime.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace Splime.CameraControl
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineInputAxisController))]
    public sealed class DialogueCameraInputLock : MonoBehaviour
    {
        [SerializeField] private LevelUIController _levelUIController;

        private CinemachineInputAxisController _inputAxisController;
        private bool _isLocked;
        private bool _restoreInputEnabled;

        private void Awake()
        {
            _inputAxisController = GetComponent<CinemachineInputAxisController>();

            if (_levelUIController == null)
            {
                _levelUIController =
                    FindFirstObjectByType<LevelUIController>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            if (_levelUIController == null)
            {
                Debug.LogWarning(
                    $"[{nameof(DialogueCameraInputLock)}] Level UI Controller reference is missing.",
                    this);
                return;
            }

            _levelUIController.ViewChanged += HandleViewChanged;
            ApplyLockState(_levelUIController.CurrentView == LevelUIView.Dialogue);
        }

        private void OnDisable()
        {
            if (_levelUIController != null)
            {
                _levelUIController.ViewChanged -= HandleViewChanged;
            }

            ApplyLockState(false);
        }

        private void HandleViewChanged(LevelUIView view)
        {
            ApplyLockState(view == LevelUIView.Dialogue);
        }

        private void ApplyLockState(bool shouldLock)
        {
            if (_inputAxisController == null || _isLocked == shouldLock)
            {
                return;
            }

            if (shouldLock)
            {
                _restoreInputEnabled = _inputAxisController.enabled;
                _inputAxisController.enabled = false;
                _isLocked = true;
                return;
            }

            _inputAxisController.enabled = _restoreInputEnabled;
            _isLocked = false;
        }
    }
}
