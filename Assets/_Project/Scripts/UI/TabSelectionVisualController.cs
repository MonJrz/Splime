using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Splime.UI
{
    [DisallowMultipleComponent]
    public sealed class TabSelectionVisualController : MonoBehaviour
    {
        [Serializable]
        private sealed class Tab
        {
            [SerializeField] private Button _button;
            [SerializeField] private GameObject _content;

            public Button Button => _button;
            public GameObject Content => _content;
            public Vector3 BaseScale { get; set; }
        }

        [Header("Tabs")]
        [SerializeField] private Tab[] _tabs;

        [Header("Selection Feedback")]
        [SerializeField, Min(1f)] private float _selectedScaleMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.12f;

        private UnityAction[] _selectionHandlers;
        private Coroutine _scaleAnimation;

        private void Awake()
        {
            if (!HasValidConfiguration())
            {
                enabled = false;
                return;
            }

            _selectionHandlers = new UnityAction[_tabs.Length];

            for (int index = 0; index < _tabs.Length; index++)
            {
                int tabIndex = index;
                _tabs[index].BaseScale = _tabs[index].Button.transform.localScale;
                _selectionHandlers[index] = () => SelectTab(tabIndex, true);
            }
        }

        private void OnEnable()
        {
            for (int index = 0; index < _tabs.Length; index++)
            {
                _tabs[index].Button.onClick.AddListener(_selectionHandlers[index]);
            }

            SelectTab(FindActiveTabIndex(), false);
        }

        private void OnDisable()
        {
            StopScaleAnimation();

            if (_selectionHandlers == null)
            {
                return;
            }

            for (int index = 0; index < _tabs.Length; index++)
            {
                _tabs[index].Button.onClick.RemoveListener(_selectionHandlers[index]);
            }
        }

        private void SelectTab(int selectedIndex, bool animate)
        {
            StopScaleAnimation();

            if (!animate || _transitionDuration <= 0f)
            {
                ApplyScales(selectedIndex);
                return;
            }

            _scaleAnimation = StartCoroutine(AnimateScales(selectedIndex));
        }

        private IEnumerator AnimateScales(int selectedIndex)
        {
            var initialScales = new Vector3[_tabs.Length];

            for (int index = 0; index < _tabs.Length; index++)
            {
                initialScales[index] = _tabs[index].Button.transform.localScale;
            }

            float elapsedTime = 0f;

            while (elapsedTime < _transitionDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, elapsedTime / _transitionDuration);

                for (int index = 0; index < _tabs.Length; index++)
                {
                    Transform tabTransform = _tabs[index].Button.transform;
                    Vector3 targetScale = GetTargetScale(index, selectedIndex);
                    tabTransform.localScale = Vector3.LerpUnclamped(
                        initialScales[index],
                        targetScale,
                        progress);
                }

                yield return null;
            }

            ApplyScales(selectedIndex);
            _scaleAnimation = null;
        }

        private void ApplyScales(int selectedIndex)
        {
            for (int index = 0; index < _tabs.Length; index++)
            {
                _tabs[index].Button.transform.localScale = GetTargetScale(index, selectedIndex);
            }
        }

        private Vector3 GetTargetScale(int tabIndex, int selectedIndex)
        {
            float multiplier = tabIndex == selectedIndex ? _selectedScaleMultiplier : 1f;
            return _tabs[tabIndex].BaseScale * multiplier;
        }

        private int FindActiveTabIndex()
        {
            for (int index = 0; index < _tabs.Length; index++)
            {
                if (_tabs[index].Content.activeSelf)
                {
                    return index;
                }
            }

            return 0;
        }

        private void StopScaleAnimation()
        {
            if (_scaleAnimation == null)
            {
                return;
            }

            StopCoroutine(_scaleAnimation);
            _scaleAnimation = null;
        }

        private bool HasValidConfiguration()
        {
            if (_tabs == null || _tabs.Length == 0)
            {
                Debug.LogError(
                    $"[{nameof(TabSelectionVisualController)}] At least one tab is required.",
                    this);
                return false;
            }

            for (int index = 0; index < _tabs.Length; index++)
            {
                Tab tab = _tabs[index];

                if (tab == null || tab.Button == null || tab.Content == null)
                {
                    Debug.LogError(
                        $"[{nameof(TabSelectionVisualController)}] Tab {index} has missing references.",
                        this);
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            _selectedScaleMultiplier = Mathf.Max(1f, _selectedScaleMultiplier);
            _transitionDuration = Mathf.Max(0f, _transitionDuration);
        }
    }
}
