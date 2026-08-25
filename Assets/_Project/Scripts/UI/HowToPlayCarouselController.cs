using System;
using UnityEngine;
using UnityEngine.UI;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class HowToPlayCarouselController : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Transform _pagesContainer;

        [Header("Page Indicator")]
        [SerializeField] private Transform _indicatorSlotsContainer;
        [SerializeField] private RectTransform _activePageIndicator;

        [Header("Navigation")]
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;

        private int _currentPageIndex;
        private bool _isNavigationExternallyControlled;

        public event Action<int> PreviousPageRequested;
        public event Action<int> NextPageRequested;
        public event Action CloseRequested;

        public int PageCount => _pagesContainer != null ? _pagesContainer.childCount : 0;
        public int CurrentPageIndex => _currentPageIndex;

        private void Awake()
        {
            if (!HasValidConfiguration())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _previousButton.onClick.AddListener(ShowPreviousPage);
            _nextButton.onClick.AddListener(ShowNextPage);
            _closeButton?.onClick.AddListener(HandleCloseButtonPressed);
            ShowPage(0);
        }

        private void OnDisable()
        {
            _previousButton?.onClick.RemoveListener(ShowPreviousPage);
            _nextButton?.onClick.RemoveListener(ShowNextPage);
            _closeButton?.onClick.RemoveListener(HandleCloseButtonPressed);
        }

        public void ShowExternallyControlled(int pageIndex)
        {
            if (PageCount <= 0 || pageIndex < 0 || pageIndex >= PageCount)
            {
                Debug.LogWarning(
                    $"[{nameof(HowToPlayCarouselController)}] Page index {pageIndex} is out of range.",
                    this);
                return;
            }

            _isNavigationExternallyControlled = true;
            gameObject.SetActive(true);
            ShowPage(pageIndex);
        }

        public void HideExternallyControlled()
        {
            _isNavigationExternallyControlled = false;
            gameObject.SetActive(false);
        }

        private void ShowPreviousPage()
        {
            if (_isNavigationExternallyControlled)
            {
                PreviousPageRequested?.Invoke(_currentPageIndex);
                return;
            }

            ShowPage(_currentPageIndex - 1);
        }

        private void ShowNextPage()
        {
            if (_isNavigationExternallyControlled)
            {
                NextPageRequested?.Invoke(_currentPageIndex);
                return;
            }

            ShowPage(_currentPageIndex + 1);
        }

        private void HandleCloseButtonPressed()
        {
            if (_isNavigationExternallyControlled)
            {
                CloseRequested?.Invoke();
            }
        }

        private void ShowPage(int pageIndex)
        {
            _currentPageIndex = WrapIndex(pageIndex, PageCount);

            for (int index = 0; index < PageCount; index++)
            {
                _pagesContainer.GetChild(index).gameObject.SetActive(index == _currentPageIndex);
            }

            Transform indicatorSlot = _indicatorSlotsContainer.GetChild(_currentPageIndex);
            _activePageIndicator.position = indicatorSlot.position;
        }

        private bool HasValidConfiguration()
        {
            if (_pagesContainer == null ||
                _indicatorSlotsContainer == null ||
                _activePageIndicator == null ||
                _previousButton == null ||
                _nextButton == null)
            {
                Debug.LogError(
                    $"[{nameof(HowToPlayCarouselController)}] One or more required references are missing.",
                    this);
                return false;
            }

            if (PageCount == 0 || _indicatorSlotsContainer.childCount != PageCount)
            {
                Debug.LogError(
                    $"[{nameof(HowToPlayCarouselController)}] Pages and indicator slots must have the same non-zero count.",
                    this);
                return false;
            }

            return true;
        }

        private static int WrapIndex(int index, int count)
        {
            return (index % count + count) % count;
        }
    }
}
