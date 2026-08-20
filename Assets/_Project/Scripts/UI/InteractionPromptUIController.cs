using TMPro;
using UnityEngine;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private float _fontSize = 24f;

        public bool IsVisible => _panel != null && _panel.activeSelf;
        public TMP_Text MessageText => _messageText;

        public float FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                ApplyFontSize();
            }
        }

        private void Awake()
        {
            ApplyFontSize();
            Hide();
        }

        public void Show(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message ?? string.Empty;
                ApplyFontSize();
            }

            if (_panel != null)
            {
                _panel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void ApplyFontSize()
        {
            if (_messageText != null && _fontSize > 0f)
            {
                _messageText.fontSize = _fontSize;
            }
        }
    }
}

