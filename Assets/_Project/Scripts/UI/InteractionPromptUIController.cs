using TMPro;
using UnityEngine;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _messageText;

        public bool IsVisible => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            Hide();
        }

        public void Show(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message ?? string.Empty;
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
    }
}
