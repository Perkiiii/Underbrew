using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BrewingInputSlotUI : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private Image iconImage;

    private BrewingStationUI owner;
    private bool locked;
    private ItemData currentItemData;
    private GameObject dragIconObject;
    private RectTransform dragRectTransform;
    private Canvas rootCanvas;

    private void Awake()
    {
        if (iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }

        if (owner == null)
            owner = GetComponentInParent<BrewingStationUI>(true);

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    public void Initialize(BrewingStationUI brewingOwner, int index)
    {
        owner = brewingOwner;
        slotIndex = index;
        Clear();
    }

    public void SetLocked(bool value)
    {
        locked = value;
    }

    public void SetItem(ItemData itemData)
    {
        currentItemData = itemData;

        if (iconImage != null)
        {
            iconImage.sprite = itemData != null ? itemData.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }
    }

    public void Clear()
    {
        SetItem(null);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (owner == null)
            owner = GetComponentInParent<BrewingStationUI>(true);

        if (locked || owner == null || !owner.CanInteract)
            return;

        var dragSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotUI>() : null;
        if (dragSlot == null || dragSlot.ItemData == null)
            return;

        owner.TrySetInputItem(slotIndex, dragSlot.ItemData, dragSlot.BoundSlotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag())
            return;

        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide(this);

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        dragIconObject = new GameObject("BrewingInputDragIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragRectTransform = dragIconObject.GetComponent<RectTransform>();
        var dragIconImage = dragIconObject.GetComponent<Image>();

        dragIconObject.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform.root, false);
        dragIconImage.sprite = currentItemData.Icon;
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

        if (!CanDrag())
            return;

        if (WasDroppedBackOnInputSlot(eventData))
            return;

        if (WasDroppedOnInventorySlot(eventData))
            return;

        owner.ClearInputItem(slotIndex);
    }

    public void TryMoveReservedItemToSlot(int targetSlotIndex)
    {
        if (owner == null)
            owner = GetComponentInParent<BrewingStationUI>(true);

        if (owner == null)
            return;

        owner.MoveReservedInputToSlot(slotIndex, targetSlotIndex);
    }

    private bool CanDrag()
    {
        if (locked || owner == null || !owner.CanInteract)
            return false;

        return currentItemData != null;
    }

    private bool WasDroppedBackOnInputSlot(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        var raycastTarget = eventData.pointerEnter != null
            ? eventData.pointerEnter.transform
            : eventData.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject.transform
                : null;

        if (raycastTarget == null)
            return false;

        return raycastTarget == transform || raycastTarget.IsChildOf(transform);
    }

    private bool WasDroppedOnInventorySlot(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        var raycastTarget = eventData.pointerEnter != null
            ? eventData.pointerEnter.transform
            : eventData.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject.transform
                : null;

        if (raycastTarget == null)
            return false;

        return raycastTarget.GetComponentInParent<InventorySlotUI>() != null;
    }
}
