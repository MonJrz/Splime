using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Splime.Player
{
    /// <summary>
    /// Captura y proporciona las entradas del usuario utilizando Unity Input System.
    /// Garantiza que ÚNICAMENTE el cliente poseedor (IsOwner) lea las entradas en red,
    /// permitiendo también pruebas locales si el objeto no ha sido instanciado en red (!IsSpawned).
    /// Clona el asset de Input para evitar que deshabilitar el mapa de un jugador no-dueño afecte al jugador local.
    /// </summary>
    public class SlimeInput : NetworkBehaviour
    {
        public static event Action<SlimeInput> LocalInputReady;
        public static event Action<bool> PauseStateReceived;
        public static event Action SwitchCharacterRequested;

        [Header("Input Action Asset Reference")]
        [SerializeField] private InputActionAsset _inputActionAsset;

        // Instancia clonada del asset para independencia entre jugadores
        private InputActionAsset _inputAssetInstance;
        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _abilityAction;
        private InputAction _interactAction;
        private InputAction _switchCharacterAction;
        private bool _isInputBlocked;
        private bool _isLocallyControlled = true;

        // Properties for current frame input states
        public Vector2 MoveInput { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool AbilityPressedThisFrame { get; private set; }
        public bool InteractPressedThisFrame { get; private set; }
        public bool SwitchCharacterPressedThisFrame { get; private set; }

        // Events for action triggers
        public event Action OnJumpPressed;
        public event Action OnAbilityPressed;
        public event Action OnInteractPressed;
        public event Action OnSwitchCharacterPressed;

        public bool IsLocalInputSource => !IsSpawned || IsOwner;
        public bool IsInputBlocked => _isInputBlocked;
        public bool IsLocallyControlled => _isLocallyControlled;
        public bool ShouldProcessInput => IsLocalInputSource && !_isInputBlocked && _isLocallyControlled;

        private void Start()
        {
            InitializeInputActions();

            if (IsLocalInputSource)
            {
                LocalInputReady?.Invoke(this);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                enabled = false;
                if (_playerMap != null)
                {
                    _playerMap.Disable();
                }

                return;
            }

            ApplyInputMapState();
            LocalInputReady?.Invoke(this);
        }

        private void InitializeInputActions()
        {
            if (_inputActionAsset == null)
            {
                Debug.LogError($"[{nameof(SlimeInput)}] Input Action Asset not assigned on {gameObject.name}. Assign 'InputSystem_Actions' in the Inspector.", this);
                return;
            }

            // Instanciar una copia independiente del asset para este jugador
            if (_inputAssetInstance == null)
            {
                _inputAssetInstance = Instantiate(_inputActionAsset);
            }

            _playerMap = _inputAssetInstance.FindActionMap("Player", true);

            if (_playerMap != null)
            {
                _moveAction = _playerMap.FindAction("Move", true);
                _jumpAction = _playerMap.FindAction("Jump", true);
                _abilityAction = _playerMap.FindAction("Ability", true);
                _interactAction = _playerMap.FindAction("Interact", true);
                _switchCharacterAction = _playerMap.FindAction("SwitchCharacter", false);

                UnsubscribeEvents();

                if (_jumpAction != null) _jumpAction.performed += HandleJump;
                if (_abilityAction != null) _abilityAction.performed += HandleAbility;
                if (_interactAction != null) _interactAction.performed += HandleInteract;
                if (_switchCharacterAction != null) _switchCharacterAction.performed += HandleSwitchCharacter;

                if (ShouldProcessInput)
                {
                    _playerMap.Enable();
                }
            }
        }

        private void Update()
        {
            if (!ShouldProcessInput) return;

            if (_moveAction != null)
            {
                MoveInput = _moveAction.ReadValue<Vector2>();
            }

            JumpPressedThisFrame = _jumpAction != null && _jumpAction.WasPressedThisFrame();
            AbilityPressedThisFrame = _abilityAction != null && _abilityAction.WasPressedThisFrame();
            InteractPressedThisFrame = _interactAction != null && _interactAction.WasPressedThisFrame();
            SwitchCharacterPressedThisFrame = _switchCharacterAction != null && _switchCharacterAction.WasPressedThisFrame();
        }

        private void HandleJump(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput) return;
            OnJumpPressed?.Invoke();
        }

        private void HandleAbility(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput) return;
            OnAbilityPressed?.Invoke();
        }

        private void HandleInteract(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput) return;
            OnInteractPressed?.Invoke();
        }

        private void HandleSwitchCharacter(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput) return;
            OnSwitchCharacterPressed?.Invoke();
            SwitchCharacterRequested?.Invoke();
        }

        public void SetLocallyControlled(bool isControlled)
        {
            if (_isLocallyControlled == isControlled)
            {
                return;
            }

            _isLocallyControlled = isControlled;
            ClearFrameInput();

            if (!isControlled)
            {
                SlimeMovement movement = GetComponent<SlimeMovement>();
                if (movement != null) movement.ResetMotion();

                SlimeJump jump = GetComponent<SlimeJump>();
                if (jump != null) jump.ResetMotion();
            }

            ApplyInputMapState();
        }

        public void SetInputBlocked(bool isBlocked)
        {
            if (_isInputBlocked == isBlocked)
            {
                return;
            }

            _isInputBlocked = isBlocked;
            ClearFrameInput();
            ApplyInputMapState();
        }

        public void RequestPauseStateForAllPlayers(bool isPaused)
        {
            if (!IsSpawned)
            {
                PauseStateReceived?.Invoke(isPaused);
                return;
            }

            RequestPauseStateRpc(isPaused);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestPauseStateRpc(bool isPaused)
        {
            BroadcastPauseStateRpc(isPaused);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastPauseStateRpc(bool isPaused)
        {
            PauseStateReceived?.Invoke(isPaused);
        }

        private void ApplyInputMapState()
        {
            if (_playerMap == null)
            {
                return;
            }

            if (ShouldProcessInput)
            {
                _playerMap.Enable();
            }
            else
            {
                _playerMap.Disable();
            }
        }

        private void ClearFrameInput()
        {
            MoveInput = Vector2.zero;
            JumpPressedThisFrame = false;
            AbilityPressedThisFrame = false;
            InteractPressedThisFrame = false;
            SwitchCharacterPressedThisFrame = false;
        }

        private void UnsubscribeEvents()
        {
            if (_jumpAction != null) _jumpAction.performed -= HandleJump;
            if (_abilityAction != null) _abilityAction.performed -= HandleAbility;
            if (_interactAction != null) _interactAction.performed -= HandleInteract;
            if (_switchCharacterAction != null) _switchCharacterAction.performed -= HandleSwitchCharacter;
        }

        public override void OnDestroy()
        {
            UnsubscribeEvents();
            if (_playerMap != null)
            {
                _playerMap.Disable();
            }
            if (_inputAssetInstance != null)
            {
                Destroy(_inputAssetInstance);
            }

            base.OnDestroy();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsOwner && _playerMap != null)
            {
                _playerMap.Disable();
            }
        }
    }
}
