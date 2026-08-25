using System.Collections;
using Unity.Cinemachine;
using Splime.Player;
using UnityEngine;

namespace Splime.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class LocalLevelCameraDirector : MonoBehaviour
    {
        public static LocalLevelCameraDirector Instance { get; private set; }

        [Header("Cameras")]
        [SerializeField] private CinemachineCamera _gameplayCamera;
        [SerializeField] private CinemachineCamera _overviewCamera;
        [SerializeField] private CinemachineCamera _interactionCamera;

        [Header("Priorities")]
        [SerializeField] private int _gameplayPriority = 10;
        [SerializeField] private int _overviewPriority = 30;
        [SerializeField] private int _interactionPriority = 40;

        [Header("Level Intro")]
        [SerializeField] private bool _playOverviewOnStart = true;
        [SerializeField, Min(0f)] private float _overviewDuration = 4f;

        [Header("Interaction Focus")]
        [SerializeField, Min(0f)] private float _defaultFocusDuration = 2f;

        [Tooltip(
            "Offset desde el objeto observado. " +
            "Se interpreta en World Space.")]
        [SerializeField] private Vector3 _defaultFocusOffset =
            new Vector3(5f, 4f, -5f);

        private SlimeInput _localInput;
        private Coroutine _overviewRoutine;
        private Coroutine _focusRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            ApplyBasePriorities();
        }

        private void OnEnable()
        {
            SlimeInput.LocalInputReady += HandleLocalInputReady;

            FindLocalInput();

            if (_playOverviewOnStart && _localInput != null)
            {
                StartOverview();
            }
        }

        private void OnDisable()
        {
            SlimeInput.LocalInputReady -= HandleLocalInputReady;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ApplyBasePriorities()
        {
            if (_gameplayCamera != null)
                _gameplayCamera.Priority = _gameplayPriority;

            if (_overviewCamera != null)
                _overviewCamera.Priority = 0;

            if (_interactionCamera != null)
                _interactionCamera.Priority = 0;
        }

        private void HandleLocalInputReady(SlimeInput input)
        {
            if (input == null || !input.IsLocalInputSource)
                return;

            _localInput = input;

            if (_playOverviewOnStart &&
                _overviewRoutine == null)
            {
                StartOverview();
            }
        }

        private void FindLocalInput()
        {
            SlimeInput[] inputs =
                FindObjectsByType<SlimeInput>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (SlimeInput input in inputs)
            {
                if (input.IsLocalInputSource)
                {
                    _localInput = input;
                    return;
                }
            }
        }

        // ─────────────────────────────
        // LEVEL OVERVIEW
        // ─────────────────────────────

        public void StartOverview()
        {
            if (_overviewCamera == null)
                return;

            if (_overviewRoutine != null)
                StopCoroutine(_overviewRoutine);

            _overviewRoutine =
                StartCoroutine(OverviewRoutine());
        }

        private IEnumerator OverviewRoutine()
        {
            _overviewCamera.Priority = _overviewPriority;

            yield return new WaitForSecondsRealtime(
                _overviewDuration);

            _overviewCamera.Priority = 0;

            _overviewRoutine = null;
        }

        // ─────────────────────────────
        // INTERACTION FOCUS
        // ─────────────────────────────

        public void ShowInteractionFocus(
            Transform target)
        {
            ShowInteractionFocus(
                target,
                _defaultFocusDuration,
                _defaultFocusOffset);
        }

        public void ShowInteractionFocus(
            Transform target,
            float duration,
            Vector3 offset)
        {
            if (target == null ||
                _interactionCamera == null)
            {
                return;
            }

            if (_focusRoutine != null)
            {
                StopCoroutine(_focusRoutine);
            }

            _focusRoutine =
                StartCoroutine(
                    InteractionFocusRoutine(
                        target,
                        duration,
                        offset));
        }

        private IEnumerator InteractionFocusRoutine(
            Transform target,
            float duration,
            Vector3 offset)
        {
            Vector3 targetPosition = target.position;

            _interactionCamera.transform.position =
                targetPosition + offset;

            Vector3 lookDirection =
                targetPosition -
                _interactionCamera.transform.position;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                _interactionCamera.transform.rotation =
                    Quaternion.LookRotation(
                        lookDirection.normalized,
                        Vector3.up);
            }

            _interactionCamera.Priority =
                _interactionPriority;

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0f, duration));

            _interactionCamera.Priority = 0;

            _focusRoutine = null;
        }
    }
}