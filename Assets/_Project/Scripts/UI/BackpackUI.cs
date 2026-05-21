using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Underbrew.Core;

public class BackpackUI : MonoBehaviour
{
    [SerializeField] private GameObject backpackWindow;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private bool lockMovementWhileOpen;
    [SerializeField] private bool requireUnlockFlag;
    [SerializeField] private string unlockFlagKey = "feature.backpack.unlocked";
    [SerializeField] private bool debugUiInputLogs;

    private readonly List<InventorySlotUI> fixedSlots = new();

    private InventorySystem inventorySystem;
    private Player player;
    private bool isOpen;
    private bool warnedMissingBindings;
    private bool warnedInsufficientSlots;
    private CanvasGroup backpackWindowCanvasGroup;
    private bool useCanvasGroupVisibility;
    private static int modalLockCount;
    private static Player modalLockPlayer;
    private static int lastEscapeCloseFrame = -1;
    private static int lastModalEscapeConsumeFrame = -1;
    private static BackpackUI cachedInstance;
    private static bool endingUiSuppressed;

    public bool IsOpen => isOpen;
    public bool IsUnlocked => EvaluateUnlocked();
    public static bool IsModalLocked => modalLockCount > 0;
    public static int ModalLockCount => modalLockCount;
    public static bool WasClosedByEscapeThisFrame => lastEscapeCloseFrame == Time.frameCount;
    public static bool WasModalEscapeConsumedThisFrame => lastModalEscapeConsumeFrame == Time.frameCount;
    public static bool IsAnyOpen => cachedInstance != null && cachedInstance.isOpen;
    public static bool IsEndingUiSuppressed => endingUiSuppressed;
    public static void CloseAnyOpen()
    {
        if (cachedInstance != null)
            cachedInstance.SetOpen(false);
    }

    public static void SetEndingUiSuppressed(bool value)
    {
        endingUiSuppressed = value;

        if (endingUiSuppressed)
            CloseAnyOpen();
    }

    public static void AcquireModalLock()
    {
        modalLockCount++;
        Debug.Log($"[BackpackUI] AcquireModalLock -> count={modalLockCount} frame={Time.frameCount}");
        ApplyModalGameplayInputState();
    }

    public static void ReleaseModalLock()
    {
        if (modalLockCount <= 0)
            return;

        modalLockCount--;
        Debug.Log($"[BackpackUI] ReleaseModalLock -> count={modalLockCount} frame={Time.frameCount}");
        ApplyModalGameplayInputState();
    }

    public static void MarkModalEscapeConsumedThisFrame()
    {
        lastModalEscapeConsumeFrame = Time.frameCount;
    }

    private static void ApplyModalGameplayInputState()
    {
        if (modalLockPlayer == null)
            modalLockPlayer = FindFirstObjectByType<Player>();

        if (modalLockPlayer == null || modalLockPlayer.input == null)
            return;

        if (IsModalLocked)
            modalLockPlayer.input.Player.Disable();
        else
            modalLockPlayer.input.Player.Enable();
    }

    private void Awake()
    {
        cachedInstance = this;

        inventorySystem = FindFirstObjectByType<InventorySystem>();
        player = FindFirstObjectByType<Player>();

        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged += HandleInventoryChanged;

        if (GameStateFlags.Instance != null)
            GameStateFlags.Instance.OnFlagChanged += HandleFlagChanged;

        CacheFixedSlots();
        SetupWindowVisibilityMode();
        RebuildGridFromInventory();
        ApplyWindowVisibility(false);
        isOpen = false;
        ApplyUnlockState();
    }

    private void OnDisable()
    {
        if (lockMovementWhileOpen)
            SetMovementInputEnabled(true);

        isOpen = false;
    }

    private void OnDestroy()
    {
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= HandleInventoryChanged;

        if (GameStateFlags.Instance != null)
            GameStateFlags.Instance.OnFlagChanged -= HandleFlagChanged;
    }

    private void Update()
    {
        if (!EvaluateUnlocked())
        {
            if (isOpen)
                SetOpen(false);

            return;
        }

        if (Keyboard.current == null)
            return;

        if (endingUiSuppressed)
            return;

        // Handle B key toggle before modal lock guard, so we can swap with journal
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            var journalUI = FindFirstObjectByType<JournalUI>(FindObjectsInactive.Include);
            if (journalUI != null && journalUI.IsOpen)
                journalUI.Close();

            Toggle();
            return;
        }

        if (IsModalLocked)
            return;

        if (isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            lastEscapeCloseFrame = Time.frameCount;
            MarkModalEscapeConsumedThisFrame();
            if (debugUiInputLogs)
                Debug.Log($"[BackpackUI] Escape close consumed at frame={Time.frameCount}");
            SetOpen(false);
            return;
        }
    }

    public void Toggle()
    {
        if (!EvaluateUnlocked())
            return;

        if (!isOpen && IsBlockedByOtherInterface())
            return;

        SetOpen(!isOpen);
    }

    public void Open()
    {
        if (!EvaluateUnlocked())
            return;

        if (IsBlockedByOtherInterface())
            return;

        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (!requireUnlockFlag || string.IsNullOrWhiteSpace(unlockFlagKey))
            return;

        if (!string.Equals(key, unlockFlagKey, System.StringComparison.Ordinal))
            return;

        ApplyUnlockState();
    }

    private bool EvaluateUnlocked()
    {
        if (!requireUnlockFlag)
            return true;

        if (string.IsNullOrWhiteSpace(unlockFlagKey))
            return true;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return false;

        return flags.GetFlag(unlockFlagKey);
    }

    private static bool IsBlockedByOtherInterface()
    {
        return BrewingStationUI.IsAnyOpen || ProcessingStationUI.IsAnyOpen || IsModalLocked;
    }

    private void ApplyUnlockState()
    {
        if (EvaluateUnlocked())
            return;

        if (isOpen)
            SetOpen(false);
    }

    private void SetOpen(bool value)
    {
        var previousOpenState = isOpen;

        if (debugUiInputLogs && isOpen != value)
            Debug.Log($"[BackpackUI] SetOpen({value}) frame={Time.frameCount} modalLocked={IsModalLocked} modalCount={modalLockCount}");

        isOpen = value;

        if (isOpen)
            RebuildGridFromInventory();

        ApplyWindowVisibility(isOpen);

        if (lockMovementWhileOpen)
            SetMovementInputEnabled(!isOpen);

        if (previousOpenState != isOpen)
            AudioManager.Instance?.PlayUi(isOpen ? AudioCueId.UIBackpackOpen : AudioCueId.UIBackpackClose);
    }

    private void HandleInventoryChanged(ItemData itemData, int quantity)
    {
        RebuildGridFromInventory();
    }

    private void RebuildGridFromInventory()
    {
        CacheFixedSlots();

        if (inventorySystem == null || gridContainer == null || fixedSlots.Count == 0)
        {
            WarnIfBindingsMissing();
            return;
        }

        var inventorySlots = inventorySystem.Slots;
        var displaySlotCount = Mathf.Min(fixedSlots.Count, inventorySlots.Count);

        for (var i = 0; i < fixedSlots.Count; i++)
        {
            if (fixedSlots[i] != null)
            {
                fixedSlots[i].Clear();
                fixedSlots[i].BindInventorySlot(inventorySystem, i < inventorySlots.Count ? i : -1, true);
                fixedSlots[i].BindDrag(isOpen, () => isOpen && !IsModalLocked, HandleSlotEndDrag);
            }
        }

        for (var slotIndex = 0; slotIndex < displaySlotCount; slotIndex++)
        {
            var slotData = inventorySlots[slotIndex];
            if (slotData == null || slotData.IsEmpty)
                continue;

            fixedSlots[slotIndex].Initialize(slotData.Item, slotData.Quantity);
            fixedSlots[slotIndex].BindDrag(isOpen, () => isOpen && !IsModalLocked, HandleSlotEndDrag);
        }

        if (fixedSlots.Count < inventorySlots.Count)
            WarnIfInventoryExceedsSlots();
    }

    private void CacheFixedSlots()
    {
        fixedSlots.Clear();

        if (gridContainer == null)
            return;

        gridContainer.GetComponentsInChildren(true, fixedSlots);
        WarnIfBindingsMissing();
    }

    private void WarnIfInventoryExceedsSlots()
    {
        if (warnedInsufficientSlots)
            return;

        warnedInsufficientSlots = true;
        Debug.LogWarning($"[BackpackUI] Inventory has more items than available UI slots under '{gridContainer.name}'. Extra items will not be shown.");
    }

    private void SetMovementInputEnabled(bool value)
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();

        if (player == null || player.input == null)
            return;

        if (value)
            player.input.Player.Movement.Enable();
        else
            player.input.Player.Movement.Disable();
    }

    private void WarnIfBindingsMissing()
    {
        if (warnedMissingBindings)
            return;

        if (backpackWindow != null && gridContainer != null && fixedSlots.Count > 0)
            return;

        warnedMissingBindings = true;
        Debug.LogWarning("[BackpackUI] Missing references. Assign Backpack Window and Grid Container, and make sure Grid Container has InventorySlotUI children.");
    }

    private void SetupWindowVisibilityMode()
    {
        useCanvasGroupVisibility = false;
        backpackWindowCanvasGroup = null;

        if (backpackWindow == null)
            return;

        if (backpackWindow != gameObject)
            return;

        useCanvasGroupVisibility = true;
        backpackWindowCanvasGroup = backpackWindow.GetComponent<CanvasGroup>();

        if (backpackWindowCanvasGroup == null)
            backpackWindowCanvasGroup = backpackWindow.AddComponent<CanvasGroup>();
    }

    private void ApplyWindowVisibility(bool visible)
    {
        if (backpackWindow == null)
            return;

        if (useCanvasGroupVisibility)
        {
            backpackWindowCanvasGroup.alpha = visible ? 1f : 0f;
            backpackWindowCanvasGroup.interactable = visible;
            backpackWindowCanvasGroup.blocksRaycasts = visible;
            return;
        }

        backpackWindow.SetActive(visible);
    }

    private void HandleSlotEndDrag(InventorySlotUI slot, PointerEventData eventData)
    {
        if (slot == null || slot.ItemData == null || eventData == null)
            return;

        if (WasDroppedInsideBackpack(eventData))
            return;

        HandleDropOutsideBackpack(slot.ItemData);
    }

    private bool WasDroppedInsideBackpack(PointerEventData eventData)
    {
        if (backpackWindow == null)
            return false;

        var raycastTarget = eventData.pointerEnter != null
            ? eventData.pointerEnter.transform
            : eventData.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject.transform
                : null;

        if (raycastTarget == null)
            return false;

        return raycastTarget == backpackWindow.transform || raycastTarget.IsChildOf(backpackWindow.transform);
    }

    private void HandleDropOutsideBackpack(ItemData itemData)
    {
        if (itemData == null || inventorySystem == null)
            return;

        Debug.Log($"[BackpackUI] Released {itemData.ItemName} outside the backpack. Hook world-drop logic here.");
    }
}
