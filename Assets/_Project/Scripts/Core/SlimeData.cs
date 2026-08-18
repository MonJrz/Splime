using UnityEngine;

namespace Splime.Core
{
    [CreateAssetMenu(fileName = "SlimeData_", menuName = "Splime/Data/Slime Data")]
    public class SlimeData : ScriptableObject
    {
        [Header("General Settings")]
        [SerializeField] private string _slimeName = "Slime";
        [SerializeField] private Color _slimeColor = Color.green;

        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 6.0f;
        [SerializeField] private float _rotationSpeed = 12.0f;
        
        [Header("Jump Settings")]
        [SerializeField] private float _jumpForce = 8.0f;
        [SerializeField] private float _gravity = -20.0f;
        [SerializeField] private float _groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask _groundLayer = 1; // Default layer

        [Header("Physical Settings")]
        [SerializeField] private float _baseWeight = 1f;
        [SerializeField] private float _baseStrength = 0f;

        // Properties
        public string SlimeName => _slimeName;
        public Color SlimeColor => _slimeColor;
        public float MoveSpeed => _moveSpeed;
        public float RotationSpeed => _rotationSpeed;
        public float JumpForce => _jumpForce;
        public float Gravity => _gravity;
        public float GroundCheckDistance => _groundCheckDistance;
        public LayerMask GroundLayer => _groundLayer;
        public float BaseWeight => _baseWeight;
        public float BaseStrength => _baseStrength;
    }
}
