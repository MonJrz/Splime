using UnityEngine;

namespace Splime.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class InteractionCameraFocus : MonoBehaviour
    {
        [Header("Focus Target")]
        [Tooltip("Objeto que debe enfocar la cámara. Si está vacío, utiliza este Transform.")]
        [SerializeField] private Transform _focusTarget;

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float _duration = 2f;

        [Tooltip("Posición de la cámara respecto al objetivo, en World Space.")]
        [SerializeField] private Vector3 _offset =
            new Vector3(5f, 4f, -5f);

        [Header("Rotation")]
        [Tooltip("Si está activo, utiliza la rotación configurada en lugar de mirar automáticamente al objetivo.")]
        [SerializeField] private bool _useCustomRotation;

        [Tooltip("Rotación de la Interaction Camera en grados.")]
        [SerializeField] private Vector3 _customEulerRotation =
            new Vector3(25f, -45f, 0f);

        public void ShowFocus()
        {
            Transform target =
                _focusTarget != null
                    ? _focusTarget
                    : transform;

            LocalLevelCameraDirector.Instance?.ShowInteractionFocus(
                target,
                _duration,
                _offset,
                _useCustomRotation,
                _customEulerRotation);
        }
    }
}