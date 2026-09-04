using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

namespace Splime.UI
{
    /// <summary>
    /// Zona táctil en pantalla (generalmente la mitad derecha) para rotar la cámara libremente
    /// mediante arrastre táctil, sin interferir con el joystick de movimiento ni con los botones de acción.
    /// </summary>
    public class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Sensibilidad")]
        [Tooltip("Sensibilidad horizontal de la cámara.")]
        [SerializeField] private float _sensitivityX = 0.15f;

        [Tooltip("Sensibilidad vertical de la cámara.")]
        [SerializeField] private float _sensitivityY = 0.12f;

        [Tooltip("Invertir el eje Y.")]
        [SerializeField] private bool _invertY = true;

        [Header("Referencias")]
        [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;

        private int _activePointerId = -1;
        private Vector2 _lastPointerPosition;

        private void Awake()
        {
            if (_orbitalFollow == null)
            {
                FindOrbitalFollow();
            }
        }

        private void OnEnable()
        {
            if (_orbitalFollow == null)
            {
                FindOrbitalFollow();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId == -1)
            {
                _activePointerId = eventData.pointerId;
                _lastPointerPosition = eventData.position;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            if (_orbitalFollow == null)
            {
                FindOrbitalFollow();
            }

            if (_orbitalFollow == null) return;

            Vector2 delta = eventData.delta;

            // Fallback para dispositivos donde eventData.delta pueda reportar 0
            if (delta.sqrMagnitude < 0.001f)
            {
                delta = eventData.position - _lastPointerPosition;
            }
            _lastPointerPosition = eventData.position;

            float xMovement = delta.x * _sensitivityX;
            float yMovement = delta.y * _sensitivityY * (_invertY ? -1f : 1f);

            _orbitalFollow.HorizontalAxis.Value += xMovement;
            _orbitalFollow.VerticalAxis.Value += yMovement;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _activePointerId)
            {
                _activePointerId = -1;
            }
        }

        private void FindOrbitalFollow()
        {
            var freeLookCam = GameObject.Find("FreeLook Camera");
            if (freeLookCam != null)
            {
                _orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
            }

            if (_orbitalFollow == null)
            {
                _orbitalFollow = FindFirstObjectByType<CinemachineOrbitalFollow>();
            }
        }
    }
}
