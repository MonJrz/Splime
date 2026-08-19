using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour, IPointerEnterHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        button.onClick.AddListener(() =>
        {
            if (MenuAudioManager.Instance != null)
                MenuAudioManager.Instance.PlayClick();
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable) return;

        if (MenuAudioManager.Instance != null)
            MenuAudioManager.Instance.PlayHover();
    }
}