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

        private bool _hasBeenShown;

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
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((_showOnlyOnce && _hasBeenShown) ||
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
        }
    }
}
