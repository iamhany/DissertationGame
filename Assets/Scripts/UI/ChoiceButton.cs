using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ChoiceButton : MonoBehaviour
{
    public TMP_Text label;

    private EventChoice          _choice;
    private Action<EventChoice>  _callback;
    private Button               _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    public void Init(EventChoice choice, Action<EventChoice> onSelected)
    {
        _choice   = choice;
        _callback = onSelected;

        if (label != null)
            label.text = choice?.text ?? string.Empty;
    }

    private void OnClick()
    {
        AudioManager.EnsureExists()?.PlayChoiceClick();
        _button.interactable = false;   // prevent double-click
        _callback?.Invoke(_choice);
    }
}
