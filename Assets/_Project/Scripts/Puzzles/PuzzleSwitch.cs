using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    public enum PuzzleSwitchMode
    {
        ToggleMover,
        MechanismLock
    }

    public class PuzzleSwitch : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private PuzzleSwitchMode _mode;

        [Header("Toggle Mover Mode")]
        [SerializeField] private ModularMover _targetMover;

        [Header("Mechanism Lock Mode")]
        [SerializeField] private PuzzleMechanismLock _mechanismLock;

        [Header("Feedback Events")]
        [SerializeField] private UnityEvent _onStateA = new UnityEvent();
        [SerializeField] private UnityEvent _onStateB = new UnityEvent();

        private bool _state;

        public bool State => _state;

        public void Interact()
        {
            switch (_mode)
            {
                case PuzzleSwitchMode.ToggleMover:
                    ToggleMover();
                    break;

                case PuzzleSwitchMode.MechanismLock:
                    ToggleMechanismLock();
                    break;
            }
        }

        private void ToggleMover()
        {
            if (_targetMover == null)
            {
                Debug.LogWarning(
                    $"[{nameof(PuzzleSwitch)}] No ModularMover assigned on {gameObject.name}.",
                    this);
                return;
            }

            _state = !_state;

            _targetMover.TogglePosition();

            ApplyFeedback();
        }

        private void ToggleMechanismLock()
        {
            if (_mechanismLock == null)
            {
                Debug.LogWarning(
                    $"[{nameof(PuzzleSwitch)}] No PuzzleMechanismLock assigned on {gameObject.name}.",
                    this);
                return;
            }

            _mechanismLock.ToggleLock();

            _state = _mechanismLock.IsLocked;

            ApplyFeedback();
        }

        private void ApplyFeedback()
        {
            if (_state)
                _onStateB.Invoke();
            else
                _onStateA.Invoke();
        }
    }
}