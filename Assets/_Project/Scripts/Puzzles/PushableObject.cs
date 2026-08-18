using UnityEngine;

namespace Splime.Puzzles
{
    [RequireComponent(typeof(Rigidbody))]
    public class PushableObject : MonoBehaviour
    {
        [SerializeField] private float _pushSpeed = 1.5f;
        [SerializeField] private float _requiredStrength = 1f;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        public bool CanBePushedBy(float strength)
        {
            return strength >= _requiredStrength;
        }

        public void TryPush(Vector3 direction, float pusherStrength, float deltaTime)
        {
            if (!CanBePushedBy(pusherStrength)) return;

            Vector3 move = direction * _pushSpeed * deltaTime;
            _rigidbody.MovePosition(_rigidbody.position + move);
        }
    }
}
