using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    public class PuzzleMechanismLock : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool _startLocked;

        [Header("Events")]
        [SerializeField] private UnityEvent _onLocked = new UnityEvent();
        [SerializeField] private UnityEvent _onUnlocked = new UnityEvent();

        private bool _isLocked;

        public bool IsLocked => _isLocked;

        private void Awake()
        {
            _isLocked = _startLocked;
        }

        public void ToggleLock()
        {
            SetLocked(!_isLocked);
        }

        public void Lock()
        {
            SetLocked(true);
        }

        public void Unlock()
        {
            SetLocked(false);
        }

        private void SetLocked(bool locked)
        {
            if (_isLocked == locked)
                return;

            _isLocked = locked;

            Debug.Log(
                $"[{nameof(PuzzleMechanismLock)}] " +
                $"{gameObject.name} -> " +
                $"{(locked ? "LOCKED" : "UNLOCKED")}",
                this);

            if (locked)
                _onLocked.Invoke();
            else
                _onUnlocked.Invoke();
        }
    }
}