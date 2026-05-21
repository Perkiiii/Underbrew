using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ProcessingBackpackSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;

    private ProcessingStationUI owner;
    private ItemData itemData;
    private int quantity;
    private bool dragEnabled = true;

    private GameObject dragIconObject;
    private Image dragIconImage;
    private RectTransform dragRectTransform;
    private Canvas rootCanvas;
    private bool warnedMissingReferences;

    public ItemData ItemData => itemData;

    private void Awake()
    {
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();

        if (iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform == null)
                iconTransform = transform.Find("ItemIcon");

            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }

        if (quantityText == null)
        {
            var quantityTransform = transform.Find("Quantity");
            if (quantityTransform == null)
                quantityTransform = transform.Find("QuantityText");

            if (quantityTransform != null)
                quantityText = quantityTransform.GetComponent<TMP_Text>();
        }
    }

    public void Initialize(ProcessingStationUI processingOwner, ItemData item, int amount)
    {
        owner = processingOwner;
        itemData = item;
        quantity = amount;
        WarnIfBindingsMissing();

        if (iconImage != null)
        {
            iconImage.sprite = itemData != null ? itemData.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (quantityText != null)
            quantityText.text = quantity.ToString();

        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void SetDragEnabled(bool value)
    {
        dragEnabled = value;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag())
            return;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        dragIconObject = new GameObject("ProcessingDragIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragRectTransform = dragIconObject.GetComponent<RectTransform>();
        dragIconImage = dragIconObject.GetComponent<Image>();

        dragIconObject.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.root, false);
        dragIconImage.sprite = itemData.Icon;
        dragIconImage.raycastTarget = false;
        dragIconImage.color = new Color(1f, 1f, 1f, 0.9f);

        var sourceRect = iconImage != null ? iconImage.rectTransform : transform as RectTransform;
        if (sourceRect != null)
            dragRectTransform.sizeDelta = sourceRect.rect.size;

        dragRectTransform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragRectTransform == null)
            return;

        dragRectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIconObject != null)
            Destroy(dragIconObject);

        dragIconObject = null;
        dragIconImage = null;
        dragRectTransform = null;
    }

    private bool CanDrag()
    {
        if (!dragEnabled)
            return false;

        if (owner == null || !owner.CanInteract)
            return false;

        if (itemData == null)
            return false;

        return quantity > 0;
    }

    private void WarnIfBindingsMissing()
    {
        if (warnedMissingReferences)
            return;

        if (slotBackground != null && iconImage != null && quantityText != null)
            return;

        warnedMissingReferences = true;
        Debug.LogWarning($"[ProcessingBackpackSlotUI] Missing UI references on '{name}'. Expected root Image, child 'Icon' Image, and child 'Quantity' TMP_Text.");
    }
}
