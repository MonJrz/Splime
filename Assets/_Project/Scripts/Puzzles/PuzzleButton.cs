using UnityEngine;
using UnityEngine.Events;

namespace Splime.Puzzles
{
    public class PuzzleButton : MonoBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] private bool _oneShot = true;

        [Header("Optional Lock")]
        [SerializeField] private PuzzleMechanismLock _mechanismLock;

        [Header("Events")]
        [SerializeField] private UnityEvent _onPressed = new UnityEvent();

        private bool _hasBeenPressed;

        public bool HasBeenPressed => _hasBeenPressed;

        public void Press()
        {
            if (_mechanismLock != null &&
                _mechanismLock.IsLocked)
            {
                Debug.Log(
                    $"[{nameof(PuzzleButton)}] {gameObject.name} bloqueado.",
                    this);

                return;
            }

            if (_oneShot && _hasBeenPressed)
                return;

            _hasBeenPressed = true;

            Debug.Log(
                $"[{nameof(PuzzleButton)}] {gameObject.name} PRESSED",
                this);

            _onPressed.Invoke();
        }

        public void ResetButton()
        {
            _hasBeenPressed = false;
        }
    }
}