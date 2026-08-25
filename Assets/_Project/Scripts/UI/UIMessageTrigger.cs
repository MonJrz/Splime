using Splime.Player;
using UnityEngine;

namespace Splime.UI
{
    public enum UIMessagePresentation
    {
        Dialogue,
        Tutorial
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class UIMessageTrigger : MonoBehaviour
    {
        [SerializeField] private LevelUIController _levelUIController;
        [SerializeField] private UIMessageSequence _messageSequence;
        [SerializeField] private UIMessagePresentation _presentation;
        [SerializeField] private bool _showOnlyOnce = true;

        [Header("Optional Movement Completion")]
        [SerializeField] private Transform _completionTarget;
        [SerializeField, Min(0.01f)] private float _completionDistance = 0.05f;

        private bool _hasBeenShown;
        private bool _isCompleted;
        private Vector2 _completionStartPosition;

        private void Awake()
        {
            if (_levelUIController == null)
            {
                _levelUIController = FindFirstObjectByType<LevelUIController>(FindObjectsInactive.Include);
            }

            Collider triggerCollider = GetComponent<Collider>();
            if (!triggerCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"[{nameof(UIMessageTrigger)}] Collider on '{name}' must have Is Trigger enabled.",
                    this);
            }

            if (_completionTarget != null)
            {
                _completionStartPosition = GetHorizontalPosition(_completionTarget);
            }
        }

        private void Update()
        {
            if (_isCompleted || _completionTarget == null)
            {
                return;
            }

            float completionDistance = Mathf.Max(0.01f, _completionDistance);
            Vector2 displacement =
                GetHorizontalPosition(_completionTarget) - _completionStartPosition;

            if (displacement.sqrMagnitude < completionDistance * completionDistance)
            {
                return;
            }

            CompleteTutorialTask();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCompleted ||
                (_showOnlyOnce && _hasBeenShown) ||
                _levelUIController == null ||
                _levelUIController.CurrentView != LevelUIView.Gameplay ||
                _messageSequence == null ||
                _messageSequence.PageCount == 0)
            {
                return;
            }

            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput == null || !slimeInput.IsLocalInputSource)
            {
                return;
            }

            _hasBeenShown = true;

            if (_presentation == UIMessagePresentation.Dialogue)
            {
                _levelUIController.ShowDialogue(_messageSequence);
            }
            else
            {
                _levelUIController.ShowTutorial(_messageSequence);
            }
        }

        public void ResetTrigger()
        {
            _hasBeenShown = false;
            _isCompleted = false;

            if (_completionTarget != null)
            {
                _completionStartPosition = GetHorizontalPosition(_completionTarget);
            }
        }

        private void CompleteTutorialTask()
        {
            _isCompleted = true;
            _hasBeenShown = true;
            _levelUIController?.DismissTutorial(_messageSequence);
        }

        private static Vector2 GetHorizontalPosition(Transform target)
        {
            Vector3 position = target.position;
            return new Vector2(position.x, position.z);
        }
    }
}
