using System;
using System.Collections.Generic;
using UnityEngine;

namespace Splime.UI
{
    [Serializable]
    public sealed class UIMessagePage
    {
        [SerializeField] private string _title;
        [SerializeField, TextArea(2, 6)] private string _body;
        [SerializeField] private Sprite _illustration;

        public string Title => _title;
        public string Body => _body;
        public Sprite Illustration => _illustration;
    }

    [CreateAssetMenu(fileName = "UIMessageSequence_", menuName = "Splime/UI/Message Sequence")]
    public sealed class UIMessageSequence : ScriptableObject
    {
        [SerializeField] private bool _canSkip = true;
        [SerializeField] private List<UIMessagePage> _pages = new List<UIMessagePage>();

        public bool CanSkip => _canSkip;
        public IReadOnlyList<UIMessagePage> Pages => _pages;
        public int PageCount => _pages.Count;

        public UIMessagePage GetPage(int index)
        {
            return index >= 0 && index < _pages.Count ? _pages[index] : null;
        }
    }
}
