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
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
        }

        public bool CanBePushedBy(float strength)
        {
            return strength >= _requiredStrength;
        }

        public void TryPush(Vector3 direction, float pusherStrength, float deltaTime)
        {
            if (!CanBePushedBy(pusherStrength)) return;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                return;

            direction.Normalize();

            Vector3 currentVelocity = _rigidbody.linearVelocity;

            Vector3 horizontalVelocity =
                direction * _pushSpeed;

            _rigidbody.linearVelocity =
                new Vector3(
                    horizontalVelocity.x,
                    currentVelocity.y,
                    horizontalVelocity.z);
        }
    }
}
