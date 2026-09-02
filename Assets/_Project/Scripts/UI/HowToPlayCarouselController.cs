using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class HowToPlayCarouselController : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Transform _pagesContainer;

        [Header("Availability")]
        [SerializeField] private GameObject[] _singlePlayerOnlyPages;

        [Header("Page Indicator")]
        [SerializeField] private Transform _indicatorSlotsContainer;
        [SerializeField] private RectTransform _activePageIndicator;

        [Header("Navigation")]
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;

        private readonly List<int> _availablePageIndices = new();
        private int _currentPageIndex;
        private bool _isPageAvailabilityInitialized;
        private bool _isNavigationExternallyControlled;

        public event Action<int> PreviousPageRequested;
        public event Action<int> NextPageRequested;
        public event Action CloseRequested;

        public int PageCount
        {
            get
            {
                EnsurePageAvailability();
                return _availablePageIndices.Count;
            }
        }
        public int CurrentPageIndex => _currentPageIndex;
        public bool IsNavigationExternallyControlled =>
            _isNavigationExternallyControlled;

        private void Awake()
        {
            if (!HasValidConfiguration())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            RefreshPageAvailability();
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

        public bool ShowLocally()
        {
            _isNavigationExternallyControlled = false;
            gameObject.SetActive(true);

            if (enabled)
            {
                return true;
            }

            gameObject.SetActive(false);
            return false;
        }

        public void Hide()
        {
            _isNavigationExternallyControlled = false;
            gameObject.SetActive(false);
        }

        public void HideExternallyControlled()
        {
            Hide();
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
            CloseRequested?.Invoke();
        }

        private void ShowPage(int pageIndex)
        {
            EnsurePageAvailability();

            int pageCount = _availablePageIndices.Count;
            if (pageCount == 0)
            {
                return;
            }

            _currentPageIndex = WrapIndex(pageIndex, pageCount);

            for (int index = 0; index < _pagesContainer.childCount; index++)
            {
                _pagesContainer.GetChild(index).gameObject.SetActive(false);
            }

            int sourcePageIndex = _availablePageIndices[_currentPageIndex];
            _pagesContainer.GetChild(sourcePageIndex).gameObject.SetActive(true);

            Transform indicatorSlot = _indicatorSlotsContainer.GetChild(sourcePageIndex);
            _activePageIndicator.position = indicatorSlot.position;
        }

        private void EnsurePageAvailability()
        {
            if (!_isPageAvailabilityInitialized)
            {
                RefreshPageAvailability();
            }
        }

        private void RefreshPageAvailability()
        {
            _isPageAvailabilityInitialized = true;
            _availablePageIndices.Clear();

            if (_pagesContainer == null)
            {
                return;
            }

            bool isMultiplayerSessionActive =
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening;

            for (int index = 0; index < _pagesContainer.childCount; index++)
            {
                GameObject page = _pagesContainer.GetChild(index).gameObject;
                bool isAvailable =
                    !isMultiplayerSessionActive || !IsSinglePlayerOnly(page);

                if (isAvailable)
                {
                    _availablePageIndices.Add(index);
                }

                page.SetActive(false);

                if (_indicatorSlotsContainer != null &&
                    index < _indicatorSlotsContainer.childCount)
                {
                    _indicatorSlotsContainer.GetChild(index).gameObject.SetActive(isAvailable);
                }
            }
        }

        private bool IsSinglePlayerOnly(GameObject page)
        {
            if (_singlePlayerOnlyPages == null)
            {
                return false;
            }

            foreach (GameObject singlePlayerOnlyPage in _singlePlayerOnlyPages)
            {
                if (singlePlayerOnlyPage == page)
                {
                    return true;
                }
            }

            return false;
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

            int totalPageCount = _pagesContainer.childCount;
            if (totalPageCount == 0 ||
                _indicatorSlotsContainer.childCount != totalPageCount)
            {
                Debug.LogError(
                    $"[{nameof(HowToPlayCarouselController)}] Pages and indicator slots must have the same non-zero count.",
                    this);
                return false;
            }

            RefreshPageAvailability();

            if (_availablePageIndices.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(HowToPlayCarouselController)}] At least one page must be available.",
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
