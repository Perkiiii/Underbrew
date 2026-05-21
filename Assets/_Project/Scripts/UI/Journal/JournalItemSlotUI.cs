using System;
using UnityEngine;
using UnityEngine.UI;

public class JournalItemSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite undiscoveredUnselectedBackground;
    [SerializeField] private Sprite undiscoveredSelectedBackground;
    [SerializeField] private Sprite discoveredUnselectedBackground;
    [SerializeField] private Sprite discoveredSelectedBackground;

    private ItemData itemData;
    private bool isDiscovered;
    private bool isSelected;
    private bool isPlaceholder;
    private Action<JournalItemSlotUI> clickHandler;

    public ItemData ItemData => itemData;
    public bool IsDiscovered => isDiscovered;
    public bool IsPlaceholder => isPlaceholder;

    private void Awake()
    {
        AutoResolveReferences();

        if (button != null)
            button.onClick.AddListener(HandleClicked);

        UpdateVisuals();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void Configure(ItemData item, bool discovered, bool selected, Action<JournalItemSlotUI> onClick)
    {
        itemData = item;
        isPlaceholder = item == null;
        isDiscovered = discovered;
        isSelected = selected;
        clickHandler = onClick;
        UpdateVisuals();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisuals();
    }

    private void HandleClicked()
    {
        clickHandler?.Invoke(this);
    }

    private void UpdateVisuals()
    {
        var hasItem = itemData != null;
        var showItemContent = hasItem && isDiscovered && !isPlaceholder;

        if (backgroundImage != null)
        {
            backgroundImage.sprite = ResolveBackgroundSprite();
            backgroundImage.enabled = backgroundImage.sprite != null;
        }

        if (iconImage != null)
        {
            iconImage.sprite = showItemContent ? itemData.Icon : null;
            iconImage.enabled = showItemContent && iconImage.sprite != null;
        }
    }

    private Sprite ResolveBackgroundSprite()
    {
        if (isPlaceholder)
            return undiscoveredUnselectedBackground;

        if (isDiscovered)
            return isSelected ? discoveredSelectedBackground : discoveredUnselectedBackground;

        return isSelected ? undiscoveredSelectedBackground : undiscoveredUnselectedBackground;
    }

    private void AutoResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (iconImage == null)
            iconImage = transform.Find("Icon") != null ? transform.Find("Icon").GetComponent<Image>() : null;
    }
}
