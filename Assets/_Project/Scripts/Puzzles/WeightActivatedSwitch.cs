using Splime.Abilities;
using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    [RequireComponent(typeof(Collider))]
    public class WeightActivatedSwitch : MonoBehaviour
    {
        [SerializeField] private UnityEvent _onActivated = new UnityEvent();
        [SerializeField] private UnityEvent _onDeactivated = new UnityEvent();

        private PlayerMetalFormAbility _currentMetalAbility;
        private bool _isActive;

        public bool IsActive => _isActive;

        private void OnTriggerEnter(Collider other)
        {
            var metalAbility = other.GetComponentInParent<PlayerMetalFormAbility>();
            if (metalAbility == null) return;

            _currentMetalAbility = metalAbility;
        }

        private void OnTriggerExit(Collider other)
        {
            var metalAbility = other.GetComponentInParent<PlayerMetalFormAbility>();
            if (metalAbility == null || metalAbility != _currentMetalAbility) return;

            _currentMetalAbility = null;
            SetActive(false);
        }

        private void Update()
        {
            bool shouldBeActive = _currentMetalAbility != null && _currentMetalAbility.IsAbilityActive;
            if (shouldBeActive != _isActive)
            {
                SetActive(shouldBeActive);
            }
        }

        private void SetActive(bool active)
        {
            _isActive = active;

            Debug.Log($"[{nameof(WeightActivatedSwitch)}] {gameObject.name} -> {(active ? "ACTIVADO" : "DESACTIVADO")}", this);

            if (active)
            {
                _onActivated.Invoke();
            }
            else
            {
                _onDeactivated.Invoke();
            }
        }
    }
}
