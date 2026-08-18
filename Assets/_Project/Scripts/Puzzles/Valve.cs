using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    public class Valve : MonoBehaviour
    {
        [Header("Optional Lock")]
        [SerializeField] private PuzzleMechanismLock _mechanismLock;

        [Header("Events")]
        [SerializeField] private UnityEvent _onOpened = new UnityEvent();
        [SerializeField] private UnityEvent _onClosed = new UnityEvent();

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        public void Toggle()
        {
            if (_mechanismLock != null &&
                _mechanismLock.IsLocked)
            {
                return;
            }

            _isOpen = !_isOpen;

            if (_isOpen)
                _onOpened.Invoke();
            else
                _onClosed.Invoke();

            Debug.Log(
                $"[{nameof(Valve)}] {gameObject.name} -> " +
                $"{(_isOpen ? "OPEN" : "CLOSED")}",
                this);
        }
    }
}