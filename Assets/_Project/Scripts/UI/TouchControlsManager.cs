using System.Collections.Generic;
using Splime.Core;
using Splime.Player;
using UnityEngine;

namespace Splime.UI
{
    /// <summary>
    /// Administrador de controles táctiles en pantalla para móviles / WebGL.
    /// Detecta la plataforma, activa la UI táctil si corresponde y redirige el Joystick y botones
    /// hacia el SlimeInput local activo.
    /// </summary>
    public class TouchControlsManager : MonoBehaviour
    {
        public static TouchControlsManager Instance { get; private set; }

        [Header("Referencias de UI Táctil")]
        [Tooltip("El contenedor padre (Canvas o Panel) que contiene los controles táctiles.")]
        [SerializeField] private GameObject _touchControlsRoot;

        [Tooltip("Referencia al Joystick (Fixed, Floating, Dynamic o Variable).")]
        [SerializeField] private Joystick _movementJoystick;

        [Tooltip("Botón opcional para alternar personaje en SinglePlayer (se oculta si no aplica).")]
        [SerializeField] private GameObject _switchCharacterButtonRoot;

        [Header("Configuración de Plataforma")]
        [Tooltip("Si es true, simula que estás en un celular mientras pruebas en Unity Editor.")]
        [SerializeField] private bool _simulateMobileInEditor = true;

        [Tooltip("Si es true, oculta automáticamente los controles si se detecta que el juego corre en PC.")]
        [SerializeField] private bool _autoDetectPlatform = true;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = true;

        private SlimeInput _currentLocalInput;

        public bool IsTouchActive => !_autoDetectPlatform || DeviceDetector.IsMobile(_simulateMobileInEditor);
        public SlimeInput CurrentLocalInput => _currentLocalInput;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_touchControlsRoot == null)
            {
                _touchControlsRoot = gameObject;
            }

            if (_movementJoystick == null)
            {
                _movementJoystick = GetComponentInChildren<Joystick>(true);
            }

            DetermineVisibility();
        }

        private void OnEnable()
        {
            DetermineVisibility();

            SlimeInput.LocalInputReady -= HandleLocalInputReady;
            SlimeInput.LocalInputReady += HandleLocalInputReady;

            if (SinglePlayerManager.Instance != null)
            {
                SinglePlayerManager.Instance.ActiveSlimeChanged -= HandleActiveSlimeChanged;
                SinglePlayerManager.Instance.ActiveSlimeChanged += HandleActiveSlimeChanged;
            }
        }

        private void OnDisable()
        {
            SlimeInput.LocalInputReady -= HandleLocalInputReady;

            if (SinglePlayerManager.Instance != null)
            {
                SinglePlayerManager.Instance.ActiveSlimeChanged -= HandleActiveSlimeChanged;
            }
        }

        private void Start()
        {
            DetermineVisibility();
            FindActiveLocalPlayer();
            UpdateSwitchButtonVisibility();

            if (SinglePlayerManager.Instance != null)
            {
                SinglePlayerManager.Instance.ActiveSlimeChanged -= HandleActiveSlimeChanged;
                SinglePlayerManager.Instance.ActiveSlimeChanged += HandleActiveSlimeChanged;
            }
        }

        private void Update()
        {
            if (!IsTouchActive) return;

            FindActiveLocalPlayer();

            // Enviar dirección del joystick al SlimeInput local
            if (_currentLocalInput != null && _movementJoystick != null)
            {
                _currentLocalInput.SetVirtualMoveInput(_movementJoystick.Direction);
            }
        }

        /// <summary>
        /// Determina si los controles táctiles deben ser visibles según el dispositivo.
        /// </summary>
        public void DetermineVisibility()
        {
            bool shouldBeActive = IsTouchActive;

            if (_touchControlsRoot != null)
            {
                _touchControlsRoot.SetActive(shouldBeActive);
            }
        }

        /// <summary>
        /// Recibe y reenvía la pulsación de los botones táctiles al SlimeInput activo o interfaz de nivel.
        /// </summary>
        public void HandleVirtualButtonTrigger(TouchButtonAction action)
        {
            if (action == TouchButtonAction.Pause)
            {
                if (_showDebugLogs)
                {
                    Debug.Log($"[{nameof(TouchControlsManager)}] ✅ Acción ejecutada: 'Pause'");
                }

                var levelUI = FindFirstObjectByType<LevelUIController>();
                if (levelUI != null)
                {
                    levelUI.HandlePauseButtonPressed();
                }
                return;
            }

            FindActiveLocalPlayer();

            if (_currentLocalInput == null)
            {
                if (_showDebugLogs)
                {
                    Debug.LogWarning($"[{nameof(TouchControlsManager)}] ⚠️ No se encontró SlimeInput activo para procesar la acción: {action}");
                }
                return;
            }

            if (_showDebugLogs)
            {
                Debug.Log($"[{nameof(TouchControlsManager)}] ✅ Acción ejecutada: '{action}' en '{_currentLocalInput.gameObject.name}'");
            }

            switch (action)
            {
                case TouchButtonAction.Jump:
                    _currentLocalInput.TriggerVirtualJump();
                    break;
                case TouchButtonAction.Ability:
                    _currentLocalInput.TriggerVirtualAbility();
                    break;
                case TouchButtonAction.Interact:
                    _currentLocalInput.TriggerVirtualInteract();
                    break;
                case TouchButtonAction.SwitchCharacter:
                    _currentLocalInput.TriggerVirtualSwitchCharacter();
                    break;
            }
        }

        private void HandleLocalInputReady(SlimeInput input)
        {
            if (input != null && input.IsLocallyControlled)
            {
                _currentLocalInput = input;
                UpdateSwitchButtonVisibility();
            }
        }

        private void HandleActiveSlimeChanged(SpawnPlayerRole role, GameObject activeSlime)
        {
            if (activeSlime != null)
            {
                SlimeInput input = activeSlime.GetComponent<SlimeInput>();
                if (input != null)
                {
                    _currentLocalInput = input;
                }
            }
            UpdateSwitchButtonVisibility();
        }

        public void FindActiveLocalPlayer()
        {
            // 1. Intentar desde SinglePlayerManager
            if (SinglePlayerManager.Instance != null)
            {
                GameObject activeSlime = SinglePlayerManager.Instance.ActiveSlime;
                if (activeSlime != null)
                {
                    SlimeInput spInput = activeSlime.GetComponent<SlimeInput>();
                    if (spInput != null)
                    {
                        _currentLocalInput = spInput;
                        return;
                    }
                }
            }

            // 2. Buscar SlimeInput marcado como IsLocallyControlled
            SlimeInput[] allInputs = FindObjectsByType<SlimeInput>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var input in allInputs)
            {
                if (input != null && input.gameObject.activeInHierarchy && input.IsLocallyControlled)
                {
                    _currentLocalInput = input;
                    return;
                }
            }

            // 3. Fallback: cualquier SlimeInput activo
            foreach (var input in allInputs)
            {
                if (input != null && input.gameObject.activeInHierarchy)
                {
                    _currentLocalInput = input;
                    return;
                }
            }
        }

        private void UpdateSwitchButtonVisibility()
        {
            if (_switchCharacterButtonRoot != null)
            {
                bool isSinglePlayer = SinglePlayerManager.Instance != null && SinglePlayerManager.Instance.IsSinglePlayerActive;
                _switchCharacterButtonRoot.SetActive(isSinglePlayer);
            }
        }

        /// <summary>
        /// Permite forzar o alternar el estado de los controles en tiempo de ejecución.
        /// </summary>
        public void SetTouchControlsActive(bool active)
        {
            if (_touchControlsRoot != null)
            {
                _touchControlsRoot.SetActive(active);
            }
        }
    }
}
