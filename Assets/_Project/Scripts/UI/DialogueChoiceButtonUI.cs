using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueChoiceButtonUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Button button;
    [SerializeField] private Graphic selectedIndicator;
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color unselectedTextColor = new(0.85f, 0.85f, 0.85f, 1f);

    private Action<int> onSelected;

    public int VisibleChoiceIndex { get; private set; }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Initialize(DialogueChoiceViewModel choice, Action<int> onChoiceSelected)
    {
        if (choice == null)
            return;

        VisibleChoiceIndex = choice.VisibleChoiceIndex;
        onSelected = onChoiceSelected;

        if (labelText != null)
            labelText.text = choice.ChoiceText;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.gameObject.SetActive(selected);

        if (labelText != null)
            labelText.color = selected ? selectedTextColor : unselectedTextColor;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void HandleClicked()
    {
        onSelected?.Invoke(VisibleChoiceIndex);
    }
}
