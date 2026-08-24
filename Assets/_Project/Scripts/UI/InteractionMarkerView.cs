using UnityEngine;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class InteractionMarkerView : MonoBehaviour
    {
        private enum MarkerState
        {
            Hidden,
            Attention,
            Interaction
        }

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite _attentionSprite;
        [SerializeField] private Sprite _interactionSprite;

        private Camera _camera;
        private MarkerState _state = MarkerState.Hidden;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            ResolveCamera();
        }

        private void LateUpdate()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                ResolveCamera();
            }

            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }

        public void ShowAttention()
        {
            ShowSprite(_attentionSprite, MarkerState.Attention);
        }

        public void ShowInteraction()
        {
            ShowSprite(_interactionSprite, MarkerState.Interaction);
        }

        public void Hide()
        {
            _state = MarkerState.Hidden;
            gameObject.SetActive(false);
        }

        private void ShowSprite(Sprite sprite, MarkerState state)
        {
            if (_spriteRenderer == null || sprite == null)
            {
                Hide();
                return;
            }

            if (_state == state && gameObject.activeSelf)
            {
                return;
            }

            _spriteRenderer.sprite = sprite;
            _state = state;
            gameObject.SetActive(true);
        }

        private void ResolveCamera()
        {
            _camera = Camera.main;
        }
    }
}
