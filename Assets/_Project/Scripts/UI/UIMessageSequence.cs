using System;
using System.Collections.Generic;
using UnityEngine;

namespace Splime.UI
{
    public enum UIMessageSpeaker
    {
        Left,
        Right,
        None
    }

    [Serializable]
    public sealed class UIMessageParticipant
    {
        [SerializeField] private string _name;
        [SerializeField] private Sprite _portrait;

        public string Name => _name;
        public Sprite Portrait => _portrait;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_name) || _portrait != null;
    }

    [Serializable]
    public sealed class UIMessagePage
    {
        [SerializeField] private string _title;
        [SerializeField, TextArea(2, 6)] private string _body;
        [SerializeField] private Sprite _illustration;
        [SerializeField] private UIMessageSpeaker _speaker;

        public string Title => _title;
        public string Body => _body;
        public Sprite Illustration => _illustration;
        public UIMessageSpeaker Speaker => _speaker;
    }

    [CreateAssetMenu(fileName = "UIMessageSequence_", menuName = "Splime/UI/Message Sequence")]
    public sealed class UIMessageSequence : ScriptableObject
    {
        [SerializeField] private bool _canSkip = true;
        [SerializeField] private UIMessageParticipant _leftParticipant;
        [SerializeField] private UIMessageParticipant _rightParticipant;
        [SerializeField] private List<UIMessagePage> _pages = new List<UIMessagePage>();

        public bool CanSkip => _canSkip;
        public UIMessageParticipant LeftParticipant => _leftParticipant;
        public UIMessageParticipant RightParticipant => _rightParticipant;
        public IReadOnlyList<UIMessagePage> Pages => _pages;
        public int PageCount => _pages.Count;

        public UIMessagePage GetPage(int index)
        {
            return index >= 0 && index < _pages.Count ? _pages[index] : null;
        }
    }
}
