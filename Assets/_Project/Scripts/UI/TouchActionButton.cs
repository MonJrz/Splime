using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Splime.UI
{
    public enum TouchButtonAction
    {
        Jump,
        Ability,
        Interact,
        SwitchCharacter,
        Pause,
        Custom
    }

    /// <summary>
    /// Botón táctil optimizado para dispositivos móviles en pantalla.
    /// Responde instantáneamente al presionar (PointerDown) y restaura su estado visual al soltar (PointerUp).
    /// </summary>
    public class TouchActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Configuración de Acción")]
        [SerializeField] private TouchButtonAction _actionType = TouchButtonAction.Jump;

        [Header("Retroalimentación Visual (Opcional)")]
        [SerializeField] private Graphic _targetGraphic;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Vector3 _pressedScale = new Vector3(0.92f, 0.92f, 1f);

        public TouchButtonAction ActionType => _actionType;
        public bool IsPressed { get; private set; }

        public event Action<TouchButtonAction> OnButtonPressed;
        public event Action<TouchButtonAction> OnButtonReleased;

        private Vector3 _originalScale;

        private void Awake()
        {
            _originalScale = transform.localScale;
            if (_targetGraphic == null)
            {
                _targetGraphic = GetComponent<Graphic>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            ApplyVisualState(true);
            OnButtonPressed?.Invoke(_actionType);

            ExecuteAction();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
            ApplyVisualState(false);
            OnButtonReleased?.Invoke(_actionType);
        }

        private void ExecuteAction()
        {
            TouchControlsManager manager = TouchControlsManager.Instance;
            if (manager == null)
            {
                manager = FindFirstObjectByType<TouchControlsManager>();
            }

            if (manager != null)
            {
                manager.HandleVirtualButtonTrigger(_actionType);
            }
            else
            {
                Debug.LogWarning($"[{nameof(TouchActionButton)}] No se encontró TouchControlsManager en la escena para procesar {_actionType}.", this);
            }
        }

        private void OnDisable()
        {
            if (IsPressed)
            {
                IsPressed = false;
                ApplyVisualState(false);
            }
        }

        private void ApplyVisualState(bool pressed)
        {
            transform.localScale = pressed ? Vector3.Scale(_originalScale, _pressedScale) : _originalScale;

            if (_targetGraphic != null)
            {
                _targetGraphic.color = pressed ? _pressedColor : _normalColor;
            }
        }
    }
}
