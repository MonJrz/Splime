using System.Collections.Generic;
using UnityEngine;
using Splime.Player;

namespace Splime.CameraControl
{
    /// <summary>
    /// Cámara cooperativa local 3D isométrico/top-down.
    /// Sigue automáticamente el punto medio entre los 2 Slimes y ajusta el zoom
    /// dinámicamente según la distancia entre ambos jugadores.
    /// No requiere sincronización en red (es 100% local en cada cliente).
    /// </summary>
    public class CooperativeCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private List<Transform> _targets = new List<Transform>();

        [Header("Camera Offset & Rotation")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 12f, -10f);
        [SerializeField] private Vector3 _lookAtOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float _smoothTime = 0.25f;

        [Header("Dynamic Zoom Settings")]
        [SerializeField] private float _minZoom = 12.0f;
        [SerializeField] private float _maxZoom = 25.0f;
        [SerializeField] private float _zoomFactor = 0.8f;

        // Velocity reference for SmoothDamp
        private Vector3 _currentVelocity;

        private void LateUpdate()
        {
            FindTargetsIfMissing();

            if (_targets.Count == 0) return;

            Vector3 centerPoint = GetCenterPoint();
            float distance = GetGreatestDistance();

            // Calcular el zoom dinámico según la distancia entre jugadores
            float targetZoom = Mathf.Clamp(_minZoom + (distance * _zoomFactor), _minZoom, _maxZoom);
            Vector3 desiredPosition = centerPoint + (_cameraOffset.normalized * targetZoom);

            // Transición suave de posición (SmoothDamp)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, _smoothTime);

            // Mantener la cámara apuntando al punto medio reajustado
            transform.LookAt(centerPoint + _lookAtOffset);
        }

        private void FindTargetsIfMissing()
        {
            // Limpiar referencias nulas o destruidas
            _targets.RemoveAll(target => target == null);

            // Si faltan objetivos, buscar Slimes activos en la escena
            if (_targets.Count < 2)
            {
                SlimeMovement[] slimes = FindObjectsByType<SlimeMovement>(FindObjectsSortMode.None);
                foreach (var slime in slimes)
                {
                    if (!_targets.Contains(slime.transform))
                    {
                        _targets.Add(slime.transform);
                    }
                }
            }
        }

        public void SetTargets(Transform player1, Transform player2)
        {
            _targets.Clear();
            if (player1 != null) _targets.Add(player1);
            if (player2 != null) _targets.Add(player2);
        }

        private Vector3 GetCenterPoint()
        {
            if (_targets.Count == 1)
            {
                return _targets[0].position;
            }

            var bounds = new Bounds(_targets[0].position, Vector3.zero);
            for (int i = 0; i < _targets.Count; i++)
            {
                bounds.Encapsulate(_targets[i].position);
            }

            return bounds.center;
        }

        private float GetGreatestDistance()
        {
            if (_targets.Count <= 1) return 0f;

            var bounds = new Bounds(_targets[0].position, Vector3.zero);
            for (int i = 0; i < _targets.Count; i++)
            {
                bounds.Encapsulate(_targets[i].position);
            }

            // Distancia máxima en el plano horizontal (X/Z)
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }
    }
}
