using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;

    public ItemData ItemData { get; private set; }
    private bool warnedMissingReferences;
    private ProcessingStationUI processingOwner;
    private bool dragEnabled;
    private int quantity;
    private GameObject dragIconObject;
    private RectTransform dragRectTransform;
    private Canvas rootCanvas;
    private Func<bool> dragAllowedEvaluator;
    private Action<InventorySlotUI, PointerEventData> endDragHandler;
    private InventorySystem boundInventorySystem;
    private int boundSlotIndex = -1;
    private bool allowInventoryReorder = true;

    public int BoundSlotIndex => boundSlotIndex;

    private void Awake()
    {
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();

        if (iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }

        if (quantityText == null)
        {
            var quantityTransform = transform.Find("Quantity");
            if (quantityTransform != null)
                quantityText = quantityTransform.GetComponent<TMP_Text>();
        }

        Clear();
    }

    private void OnDisable()
    {
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide(this);
    }

    private void OnDestroy()
    {
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide(this);
    }

    public void Initialize(ItemData itemData, int quantity)
    {
        ItemData = itemData;
        this.quantity = quantity;
        WarnIfBindingsMissing();

        if (iconImage != null)
        {
            iconImage.sprite = itemData != null ? itemData.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        UpdateQuantity(quantity);
    }

    public void UpdateQuantity(int quantity)
    {
        this.quantity = quantity;

        if (quantityText != null)
            quantityText.text = quantity.ToString();
    }

    public void Clear()
    {
        ItemData = null;
        quantity = 0;
        WarnIfBindingsMissing();

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (quantityText != null)
            quantityText.text = string.Empty;
    }

    public void BindProcessingDrag(ProcessingStationUI owner, bool enableDrag)
    {
        processingOwner = owner;
        dragEnabled = enableDrag;
        dragAllowedEvaluator = owner != null ? (() => owner.CanInteract) : null;
        endDragHandler = null;

        if (processingOwner != null && rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    public void BindDrag(bool enableDrag, Func<bool> canDragEvaluator = null, Action<InventorySlotUI, PointerEventData> onEndDrag = null)
    {
        processingOwner = null;
        dragEnabled = enableDrag;
        dragAllowedEvaluator = canDragEvaluator;
        endDragHandler = onEndDrag;

        if (dragEnabled && rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    public void BindInventorySlot(InventorySystem inventorySystem, int slotIndex, bool allowReorder = true)
    {
        boundInventorySystem = inventorySystem;
        boundSlotIndex = slotIndex;
        allowInventoryReorder = allowReorder;
    }

    public void SetDragEnabled(bool value)
    {
        dragEnabled = value;
    }

    private void WarnIfBindingsMissing()
    {
        if (warnedMissingReferences)
            return;

        if (slotBackground != null && iconImage != null && quantityText != null)
            return;

        warnedMissingReferences = true;
        Debug.LogWarning($"[InventorySlotUI] Missing UI references on '{name}'. Expected root Image, child 'Icon' Image, and child 'Quantity' TMP_Text.");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipUI.Instance == null)
            return;

        if (ItemData == null)
        {
            TooltipUI.Instance.Hide(this);
            return;
        }

        TooltipUI.Instance.Show(ItemData.ItemName, ItemData.TooltipDescription, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipUI.Instance == null)
            return;

        TooltipUI.Instance.Hide(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag())
            return;

        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide(this);

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        dragIconObject = new GameObject("InventoryDragIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragRectTransform = dragIconObject.GetComponent<RectTransform>();
        var dragIconImage = dragIconObject.GetComponent<Image>();

        dragIconObject.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.root, false);
        dragIconImage.sprite = ItemData.Icon;
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
        dragRectTransform = null;

        endDragHandler?.Invoke(this, eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (boundInventorySystem == null || boundSlotIndex < 0)
            return;

        var draggedSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotUI>() : null;
        if (draggedSlot != null && draggedSlot != this)
        {
            if (!allowInventoryReorder)
                return;

            if (draggedSlot.boundInventorySystem != boundInventorySystem || draggedSlot.boundSlotIndex < 0 || !draggedSlot.allowInventoryReorder)
                return;

            boundInventorySystem.MoveSlotItem(draggedSlot.boundSlotIndex, boundSlotIndex);
            return;
        }

        var processingInputSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ProcessingInputSlotUI>() : null;
        if (processingInputSlot != null)
        {
            processingInputSlot.TryMoveReservedItemToSlot(boundSlotIndex);
            return;
        }

        var brewingInputSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<BrewingInputSlotUI>() : null;
        if (brewingInputSlot != null)
            brewingInputSlot.TryMoveReservedItemToSlot(boundSlotIndex);
    }

    private bool CanDrag()
    {
        if (!dragEnabled)
            return false;

        if (dragAllowedEvaluator != null && !dragAllowedEvaluator.Invoke())
            return false;

        if (dragAllowedEvaluator == null && processingOwner != null && !processingOwner.CanInteract)
            return false;

        if (ItemData == null)
            return false;

        return quantity > 0;
    }
}
