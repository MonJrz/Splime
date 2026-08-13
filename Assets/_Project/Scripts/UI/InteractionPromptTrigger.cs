using Splime.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class InteractionPromptTrigger : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] private LevelUIController _levelUIController;
        [SerializeField] private string _message = "Press E to interact";
        [SerializeField] private bool _hideAfterInteraction = true;

        [Header("Interaction")]
        [SerializeField] private UnityEvent _interactionRequested = new UnityEvent();

        private SlimeInput _localInput;

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
                    $"[{nameof(InteractionPromptTrigger)}] Collider on '{name}' must have Is Trigger enabled.",
                    this);
            }
        }

        private void OnDisable()
        {
            UnbindLocalInput();
            _levelUIController?.HideInteractionPrompt();
        }

        private void OnTriggerEnter(Collider other)
        {
            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput == null || !slimeInput.IsLocalInputSource)
            {
                return;
            }

            UnbindLocalInput();
            _localInput = slimeInput;
            _localInput.OnInteractPressed += HandleInteractionRequested;
            _levelUIController?.ShowInteractionPrompt(_message);
        }

        private void OnTriggerExit(Collider other)
        {
            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput == null || slimeInput != _localInput)
            {
                return;
            }

            UnbindLocalInput();
            _levelUIController?.HideInteractionPrompt();
        }

        private void HandleInteractionRequested()
        {
            _interactionRequested.Invoke();

            if (_hideAfterInteraction)
            {
                _levelUIController?.HideInteractionPrompt();
            }
        }

        private void UnbindLocalInput()
        {
            if (_localInput != null)
            {
                _localInput.OnInteractPressed -= HandleInteractionRequested;
                _localInput = null;
            }
        }
    }
}
