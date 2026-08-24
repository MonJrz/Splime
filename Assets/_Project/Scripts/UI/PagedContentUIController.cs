using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class PagedContentUIController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private TMP_Text _pageIndicatorText;
        [SerializeField] private Image _illustrationImage;

        [Header("Optional Dialogue Participants")]
        [SerializeField] private TMP_Text _leftParticipantTitleText;
        [SerializeField] private TMP_Text _rightParticipantTitleText;
        [SerializeField] private Image _leftParticipantImage;
        [SerializeField] private Image _rightParticipantImage;

        [Header("Actions")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _skipButton;
        [SerializeField] private TMP_Text _nextButtonText;
        [SerializeField] private string _nextActionText = "Continue";
        [SerializeField] private string _finishActionText = "Close";

        private UIMessageSequence _activeSequence;
        private int _currentPageIndex;

        public event Action Opened;
        public event Action Completed;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            Hide();
        }

        private void OnEnable()
        {
            _nextButton?.onClick.AddListener(HandleNextButtonPressed);
            _skipButton?.onClick.AddListener(HandleSkipButtonPressed);
        }

        private void OnDisable()
        {
            _nextButton?.onClick.RemoveListener(HandleNextButtonPressed);
            _skipButton?.onClick.RemoveListener(HandleSkipButtonPressed);
        }

        public void Show(UIMessageSequence sequence)
        {
            if (sequence == null || sequence.PageCount == 0)
            {
                Debug.LogWarning($"[{nameof(PagedContentUIController)}] The requested sequence has no pages.", this);
                return;
            }

            _activeSequence = sequence;
            _currentPageIndex = 0;
            SetPanelActive(true);
            RefreshParticipants();
            RefreshPage();
            Opened?.Invoke();
        }

        public void Hide()
        {
            _activeSequence = null;
            _currentPageIndex = 0;
            SetPanelActive(false);
        }

        public void HandleNextButtonPressed()
        {
            if (_activeSequence == null)
            {
                return;
            }

            if (_currentPageIndex + 1 >= _activeSequence.PageCount)
            {
                CompleteSequence();
                return;
            }

            _currentPageIndex++;
            RefreshPage();
        }

        public void HandleSkipButtonPressed()
        {
            if (_activeSequence != null && _activeSequence.CanSkip)
            {
                CompleteSequence();
            }
        }

        private void RefreshPage()
        {
            UIMessagePage page = _activeSequence?.GetPage(_currentPageIndex);
            if (page == null)
            {
                CompleteSequence();
                return;
            }

            SetText(_titleText, page.Title);
            SetText(_bodyText, page.Body);

            if (_pageIndicatorText != null)
            {
                _pageIndicatorText.text = $"{_currentPageIndex + 1}/{_activeSequence.PageCount}";
            }

            if (_illustrationImage != null)
            {
                _illustrationImage.sprite = page.Illustration;
                _illustrationImage.gameObject.SetActive(page.Illustration != null);
            }

            RefreshParticipantVisibility(page.Speaker);

            bool isLastPage = _currentPageIndex + 1 >= _activeSequence.PageCount;
            SetText(_nextButtonText, isLastPage ? _finishActionText : _nextActionText);

            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(_activeSequence.CanSkip && !isLastPage);
            }
        }

        private void CompleteSequence()
        {
            Hide();
            Completed?.Invoke();
        }

        private void RefreshParticipants()
        {
            SetParticipant(
                _leftParticipantTitleText,
                _leftParticipantImage,
                _activeSequence?.LeftParticipant);
            SetParticipant(
                _rightParticipantTitleText,
                _rightParticipantImage,
                _activeSequence?.RightParticipant);
        }

        private void RefreshParticipantVisibility(UIMessageSpeaker speaker)
        {
            SetParticipantVisible(
                _leftParticipantTitleText,
                _leftParticipantImage,
                speaker == UIMessageSpeaker.Left);
            SetParticipantVisible(
                _rightParticipantTitleText,
                _rightParticipantImage,
                speaker == UIMessageSpeaker.Right);
        }

        private static void SetParticipant(
            TMP_Text titleText,
            Image portraitImage,
            UIMessageParticipant participant)
        {
            bool isConfigured = participant != null && participant.IsConfigured;

            if (titleText != null)
            {
                if (string.IsNullOrWhiteSpace(titleText.text) && isConfigured)
                {
                    titleText.text = participant.Name ?? string.Empty;
                }

                titleText.gameObject.SetActive(
                    isConfigured && !string.IsNullOrWhiteSpace(titleText.text));
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = isConfigured ? participant.Portrait : null;
                portraitImage.preserveAspect = true;
                portraitImage.gameObject.SetActive(isConfigured && participant.Portrait != null);
            }
        }

        private static void SetParticipantVisible(
            TMP_Text titleText,
            Image portraitImage,
            bool isVisible)
        {
            if (titleText != null)
            {
                titleText.gameObject.SetActive(
                    isVisible && !string.IsNullOrWhiteSpace(titleText.text));
            }

            if (portraitImage != null)
            {
                portraitImage.gameObject.SetActive(isVisible && portraitImage.sprite != null);
            }
        }

        private void SetPanelActive(bool isActive)
        {
            if (_panel != null)
            {
                _panel.SetActive(isActive);
            }
        }

        private static void SetText(TMP_Text textComponent, string value)
        {
            if (textComponent != null)
            {
                textComponent.text = value ?? string.Empty;
            }
        }
    }
}
