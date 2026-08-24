using System.Collections.Generic;
using Splime.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class InteractionPromptTrigger : MonoBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] private bool _hideAfterInteraction = true;

        [Header("World Marker")]
        [SerializeField] private GameObject _markerPrefab;
        [Min(0f)]
        [SerializeField] private float _markerHeight = 0.35f;
        [Min(0f)]
        [SerializeField] private float _attentionRange = 4.5f;

        [Header("Interaction")]
        [SerializeField] private UnityEvent _interactionRequested = new UnityEvent();

        private SlimeInput _localInput;
        private SlimeAnimatorController _localAnimatorController;
        private InteractionMarkerView _markerView;
        private Collider _triggerCollider;
        private Transform _localPlayerTransform;
        private readonly HashSet<Collider> _localCollidersInside = new HashSet<Collider>();
        private bool _isInteractionAvailable = true;
        private float _nextLocalPlayerSearchTime;

        private const float LocalPlayerSearchInterval = 0.5f;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            CreateMarker(_triggerCollider);

            if (!_triggerCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"[{nameof(InteractionPromptTrigger)}] Collider on '{name}' must have Is Trigger enabled.",
                    this);
            }
        }

        private void OnEnable()
        {
            RefreshWorldFeedback();
        }

        private void OnDisable()
        {
            UnbindLocalInput();
            _markerView?.Hide();
        }

        private void Update()
        {
            if (_localInput == null)
            {
                RefreshWorldFeedback();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();

            if (slimeInput == null ||
                !slimeInput.IsLocalInputSource ||
                !_isInteractionAvailable)
            {
                return;
            }

            if (_localInput != slimeInput)
            {
                UnbindLocalInput();
                _localInput = slimeInput;
                _localPlayerTransform = slimeInput.transform;
                _localAnimatorController = other.GetComponentInParent<SlimeAnimatorController>();
                _localInput.OnInteractPressed += HandleInteractionRequested;
            }

            _localCollidersInside.Add(other);
            _markerView?.ShowInteraction();
        }

        private void OnTriggerExit(Collider other)
        {
            SlimeInput slimeInput = other.GetComponentInParent<SlimeInput>();
            if (slimeInput == null || slimeInput != _localInput)
            {
                return;
            }

            _localCollidersInside.Remove(other);
            _localCollidersInside.RemoveWhere(collider =>
                collider == null || !collider.gameObject.activeInHierarchy);

            if (_localCollidersInside.Count > 0)
            {
                return;
            }

            UnbindLocalInput();
            RefreshWorldFeedback();
        }

        private void HandleInteractionRequested()
        {
            _localAnimatorController?.TriggerAction();
            _interactionRequested.Invoke();

            if (_hideAfterInteraction)
            {
                SetInteractionAvailable(false);
            }
        }

        public void SetInteractionAvailable(bool isAvailable)
        {
            _isInteractionAvailable = isAvailable;

            if (!isAvailable)
            {
                UnbindLocalInput();
                _markerView?.Hide();
                return;
            }

            RefreshWorldFeedback();
        }

        private void RefreshWorldFeedback()
        {
            if (!_isInteractionAvailable || _markerView == null)
            {
                _markerView?.Hide();
                return;
            }

            if (_localInput != null)
            {
                _markerView.ShowInteraction();
                return;
            }

            if (IsLocalPlayerWithinAttentionRange())
            {
                _markerView.ShowAttention();
                return;
            }

            _markerView.Hide();
        }

        private bool IsLocalPlayerWithinAttentionRange()
        {
            if (_attentionRange <= 0f || !TryResolveLocalPlayer())
            {
                return false;
            }

            Vector3 offset = _localPlayerTransform.position - _triggerCollider.bounds.center;
            offset.y = 0f;
            return offset.sqrMagnitude <= _attentionRange * _attentionRange;
        }

        private bool TryResolveLocalPlayer()
        {
            if (_localPlayerTransform != null && _localPlayerTransform.gameObject.activeInHierarchy)
            {
                return true;
            }

            if (Time.unscaledTime < _nextLocalPlayerSearchTime)
            {
                return false;
            }

            _nextLocalPlayerSearchTime = Time.unscaledTime + LocalPlayerSearchInterval;

            SlimeInput[] inputs = FindObjectsByType<SlimeInput>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (SlimeInput input in inputs)
            {
                if (input.IsLocalInputSource)
                {
                    _localPlayerTransform = input.transform;
                    return true;
                }
            }

            _localPlayerTransform = null;
            return false;
        }

        private void CreateMarker(Collider triggerCollider)
        {
            if (_markerPrefab == null)
            {
                return;
            }

            GameObject markerInstance = Instantiate(_markerPrefab, transform);
            markerInstance.name = $"{name} Interaction Marker";

            Vector3 markerPosition = triggerCollider.bounds.center;
            markerPosition.y = triggerCollider.bounds.max.y + _markerHeight;
            markerInstance.transform.position = markerPosition;

            _markerView = markerInstance.GetComponent<InteractionMarkerView>();
            if (_markerView == null)
            {
                Debug.LogWarning(
                    $"[{nameof(InteractionPromptTrigger)}] Marker prefab on '{name}' requires an " +
                    $"{nameof(InteractionMarkerView)} component.",
                    this);
                markerInstance.SetActive(false);
                return;
            }

            _markerView.Hide();
        }

        private void UnbindLocalInput()
        {
            if (_localInput != null)
            {
                _localInput.OnInteractPressed -= HandleInteractionRequested;
                _localInput = null;
            }

            _localAnimatorController = null;
            _localCollidersInside.Clear();
        }
    }
}
