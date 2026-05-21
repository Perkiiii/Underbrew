using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Underbrew.Core;

public class ProcessingStationUI : MonoBehaviour
{
    [SerializeField] private GameObject processingWindow;
    [SerializeField] private bool deactivateRootWhenClosed = true;
    [SerializeField] private Transform backpackListContainer;
    [SerializeField] private ProcessingInputSlotUI inputSlot;
    [SerializeField] private Image outputIconImage;
    [SerializeField] private TMP_Text outputNameText;
    [SerializeField] private Button outputIconButton;
    [SerializeField] private Button outputSlotButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text stationTitleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool debugUiInputLogs;
    [Header("Quest Progress")]
    [SerializeField] private string setFlagOnSuccessfulProcessing;
    [SerializeField] private bool setFlagValueOnSuccessfulProcessing = true;

    private readonly List<InventorySlotUI> backpackSlots = new();

    private InventorySystem inventorySystem;
    private BackpackUI backpackUI;
    private CraftingStation activeStation;
    private ProcessingRecipe selectedRecipe;
    private ItemData selectedInputItem;
    private int selectedInputSourceSlotIndex = -1;

    private bool isOpen;
    private bool isProcessing;
    private float processingElapsed;
    private float processingDuration;
    private bool reopenBackpackOnClose;
    private bool warnedMissingSlotBindings;
    private bool warnedInsufficientSlots;
    private static int lastEscapeCloseFrame = -1;
    private static int openInstanceCount;

    public bool CanInteract => isOpen && !isProcessing;
    public static bool WasClosedByEscapeThisFrame => lastEscapeCloseFrame == Time.frameCount;
    public static bool IsAnyOpen => openInstanceCount > 0;

    private void Awake()
    {
        AutoBindUiReferences();
        DisableNonInteractiveTextRaycasts();
        inventorySystem = FindFirstObjectByType<InventorySystem>();

        EnsureOutputClickBinding();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmProcessing);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (inputSlot != null)
            inputSlot.Initialize(this);

        CacheBackpackSlots();
        SetProgressVisible(false);
        SetProgress01(0f);

        if (deactivateRootWhenClosed)
        {
            SetOpen(false);
            gameObject.SetActive(false);
            return;
        }

        SetOpen(false);
    }

    private void OnDestroy()
    {
        BackpackUI.ReleaseModalLock();

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmProcessing);

        if (outputIconButton != null)
            outputIconButton.onClick.RemoveListener(ConfirmProcessing);

        if (outputSlotButton != null)
            outputSlotButton.onClick.RemoveListener(ConfirmProcessing);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void Update()
    {
        if (!isOpen)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !isProcessing)
        {
            lastEscapeCloseFrame = Time.frameCount;
            BackpackUI.MarkModalEscapeConsumedThisFrame();
            if (debugUiInputLogs)
                Debug.Log($"[ProcessingStationUI] Escape close consumed at frame={Time.frameCount}");
            Close();
        }

        if (isProcessing)
            TickProcessing();
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            SetOpen(false);
            SetProgressVisible(false);
            SetProgress01(0f);
            BackpackUI.ReleaseModalLock();
        }
    }

    public void Open(CraftingStation station)
    {
        if (station == null)
            return;

        if (debugUiInputLogs)
            Debug.Log($"[ProcessingStationUI] Open requested frame={Time.frameCount} station='{station.StationDisplayName}' rootActive={gameObject.activeSelf}");

        AutoBindUiReferences();
        DisableNonInteractiveTextRaycasts();
        EnsureOutputClickBinding();

        if (isOpen)
            return;

        if (gameObject.activeSelf == false)
            gameObject.SetActive(true);

        if (backpackUI == null)
            backpackUI = FindFirstObjectByType<BackpackUI>(FindObjectsInactive.Include);

        reopenBackpackOnClose = backpackUI != null && backpackUI.IsOpen;
        if (reopenBackpackOnClose)
            backpackUI.Close();

        activeStation = station;

        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();

        if (inventorySystem != null)
        {
            inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
            inventorySystem.OnInventoryChanged += HandleInventoryChanged;
        }

        if (stationTitleText != null)
            stationTitleText.text = station.StationDisplayName;

        selectedInputItem = null;
        selectedRecipe = null;
        selectedInputSourceSlotIndex = -1;

        if (inputSlot != null)
        {
            inputSlot.Initialize(this);
            inputSlot.Clear();
        }

        UpdateOutputPreview();
        RebuildBackpackList();
        UpdateConfirmState();
        SetProgressVisible(false);
        SetProgress01(0f);
        SetOpen(true);

        if (!IsWindowVisible())
        {
            Debug.LogWarning("[ProcessingStationUI] Open requested, but processing window is not visible. Check ProcessingWindow reference and hierarchy.");
            SetOpen(false);
            return;
        }

        BackpackUI.AcquireModalLock();

        if (debugUiInputLogs)
            Debug.Log($"[ProcessingStationUI] Open success frame={Time.frameCount} modalCount={BackpackUI.ModalLockCount}");
    }

    public void Close()
    {
        if (!isOpen || isProcessing)
            return;

        if (debugUiInputLogs)
            Debug.Log($"[ProcessingStationUI] Close requested frame={Time.frameCount} modalCount(before)={BackpackUI.ModalLockCount}");

        activeStation = null;
        selectedInputItem = null;
        selectedRecipe = null;
        selectedInputSourceSlotIndex = -1;

        if (inputSlot != null)
            inputSlot.Clear();

        UpdateOutputPreview();
        SetOpen(false);
        SetProgressVisible(false);
        SetProgress01(0f);

        BackpackUI.ReleaseModalLock();

        if (debugUiInputLogs)
            Debug.Log($"[ProcessingStationUI] Close complete frame={Time.frameCount} modalCount(after)={BackpackUI.ModalLockCount}");

        if (reopenBackpackOnClose && backpackUI != null && backpackUI.IsOpen == false)
            backpackUI.Open();

        reopenBackpackOnClose = false;

        if (deactivateRootWhenClosed)
            gameObject.SetActive(false);
    }

    public void TrySetInputItem(ItemData itemData, int sourceSlotIndex)
    {
        if (!CanInteract)
            return;

        if (inventorySystem == null || sourceSlotIndex < 0 || sourceSlotIndex >= inventorySystem.Slots.Count)
            return;

        selectedInputItem = itemData;
        selectedInputSourceSlotIndex = sourceSlotIndex;
        selectedRecipe = FindMatchingRecipe(itemData);

        if (selectedRecipe == null)
        {
            var recipeCount = activeStation != null && activeStation.ProcessingRecipes != null
                ? activeStation.ProcessingRecipes.Length
                : 0;
            Debug.LogWarning($"[ProcessingStationUI] No matching processing recipe found for '{itemData?.ItemName ?? "null"}' on station '{activeStation?.StationDisplayName ?? "null"}'. StationType={activeStation?.StationType.ToString() ?? "null"}, recipeCount={recipeCount}.");
        }

        if (inputSlot != null)
            inputSlot.SetItem(itemData);

        RebuildBackpackList();
        UpdateOutputPreview();
        UpdateConfirmState();
        AudioManager.Instance?.PlaySfx(AudioCueId.ProcessAdd);
    }

    public void ClearInputItem()
    {
        if (!CanInteract)
            return;

        selectedInputItem = null;
        selectedRecipe = null;
        selectedInputSourceSlotIndex = -1;

        if (inputSlot != null)
            inputSlot.Clear();

        RebuildBackpackList();
        UpdateOutputPreview();
        UpdateConfirmState();
    }

    public void MoveReservedInputToSlot(int targetSlotIndex)
    {
        if (!CanInteract || inventorySystem == null)
            return;

        if (selectedInputSourceSlotIndex < 0 || targetSlotIndex < 0 || targetSlotIndex >= inventorySystem.Slots.Count)
            return;

        var originalSourceSlotIndex = selectedInputSourceSlotIndex;
        if (originalSourceSlotIndex != targetSlotIndex && !inventorySystem.MoveSlotItem(originalSourceSlotIndex, targetSlotIndex))
            return;

        selectedInputItem = null;
        selectedRecipe = null;
        selectedInputSourceSlotIndex = -1;

        if (inputSlot != null)
            inputSlot.Clear();

        RebuildBackpackList();
        UpdateOutputPreview();
        UpdateConfirmState();

        if (originalSourceSlotIndex == targetSlotIndex)
            AudioManager.Instance?.PlayUi(AudioCueId.UIBackpackMove);
    }

    private void ConfirmProcessing()
    {
        if (!CanInteract)
            return;

        if (selectedRecipe == null || inventorySystem == null)
            return;

        if (!inventorySystem.CanAddAfterConsuming(selectedRecipe.OutputItem, selectedRecipe.Requirements, selectedRecipe.OutputQuantity))
        {
            statusText.text = "Backpack is full.";
            UpdateConfirmState();
            return;
        }

        if (!inventorySystem.ConsumeRequirements(selectedRecipe.Requirements))
        {
            statusText.text = "Missing required ingredients.";
            RebuildBackpackList();
            UpdateConfirmState();
            return;
        }

        isProcessing = true;
        processingElapsed = 0f;
        processingDuration = Mathf.Max(0.01f, selectedRecipe.ProcessingTime);

        if (statusText != null)
            statusText.text = selectedRecipe.OutputItem != null
                ? $"Processing {selectedRecipe.OutputItem.ItemName}..."
                : "Processing...";

        if (inputSlot != null)
            inputSlot.SetLocked(true);

        SetBackpackDragEnabled(false);
        SetProgressVisible(true);
        SetProgress01(0f);
        UpdateConfirmState();
    }

    private void TickProcessing()
    {
        processingElapsed += Time.deltaTime;
        var progress = Mathf.Clamp01(processingElapsed / processingDuration);
        SetProgress01(progress);

        if (progress < 1f)
            return;

        CompleteProcessing();
    }

    private void CompleteProcessing()
    {
        var itemCreated = inventorySystem != null
            && selectedRecipe != null
            && inventorySystem.Add(selectedRecipe.OutputItem, selectedRecipe.OutputQuantity);

        if (itemCreated && !string.IsNullOrWhiteSpace(setFlagOnSuccessfulProcessing) && GameStateFlags.Instance != null)
            GameStateFlags.Instance.SetFlag(setFlagOnSuccessfulProcessing, setFlagValueOnSuccessfulProcessing);

        if (itemCreated)
            AudioManager.Instance?.PlaySfx(AudioCueId.ProcessComplete);

        if (statusText != null)
            statusText.text = itemCreated && selectedRecipe != null && selectedRecipe.OutputItem != null
                ? $"Created {selectedRecipe.OutputItem.ItemName} x{selectedRecipe.OutputQuantity}"
                : "Backpack is full.";

        isProcessing = false;
        processingElapsed = 0f;
        processingDuration = 0f;

        selectedInputItem = null;
        selectedRecipe = null;
        selectedInputSourceSlotIndex = -1;

        if (inputSlot != null)
        {
            inputSlot.SetLocked(false);
            inputSlot.Clear();
        }

        SetBackpackDragEnabled(true);
        SetProgressVisible(false);
        SetProgress01(0f);
        UpdateOutputPreview();
        RebuildBackpackList();
        UpdateConfirmState();
    }

    private void RebuildBackpackList()
    {
        CacheBackpackSlots();

        if (inventorySystem == null || backpackListContainer == null || backpackSlots.Count == 0)
        {
            WarnIfSlotBindingsMissing();
            return;
        }

        var inventorySlots = inventorySystem.Slots;
        var displaySlotCount = Mathf.Min(backpackSlots.Count, inventorySlots.Count);

        for (var i = 0; i < backpackSlots.Count; i++)
        {
            if (backpackSlots[i] == null)
                continue;

            backpackSlots[i].Clear();
            var allowReorder = GetReservedQuantityForSlot(i) <= 0;
            backpackSlots[i].BindInventorySlot(inventorySystem, i < inventorySlots.Count ? i : -1, allowReorder);
            backpackSlots[i].BindProcessingDrag(this, !isProcessing);
        }

        for (var slotIndex = 0; slotIndex < displaySlotCount; slotIndex++)
        {
            var slotData = inventorySlots[slotIndex];
            if (slotData == null || slotData.IsEmpty)
                continue;

            var visibleQuantity = slotData.Quantity - GetReservedQuantityForSlot(slotIndex);
            if (visibleQuantity <= 0)
                continue;

            backpackSlots[slotIndex].Initialize(slotData.Item, visibleQuantity);
            backpackSlots[slotIndex].BindProcessingDrag(this, !isProcessing);
        }

        if (backpackSlots.Count < inventorySlots.Count)
            WarnIfInventoryExceedsDisplayedSlots();
    }

    private ProcessingRecipe FindMatchingRecipe(ItemData inputItem)
    {
        if (activeStation == null || inputItem == null)
            return null;

        var recipes = activeStation.ProcessingRecipes;
        if (recipes == null)
            return null;

        for (var i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            if (recipe == null)
                continue;

            if (recipe.StationType != activeStation.StationType)
                continue;

            if (recipe.InputItem == inputItem)
                return recipe;
        }

        return null;
    }

    private void UpdateOutputPreview()
    {
        if (outputIconImage != null)
        {
            outputIconImage.sprite = selectedRecipe != null && selectedRecipe.OutputItem != null
                ? selectedRecipe.OutputItem.Icon
                : null;
            outputIconImage.enabled = outputIconImage.sprite != null;
        }

        if (outputNameText != null)
        {
            if (selectedRecipe != null && selectedRecipe.OutputItem != null)
                outputNameText.text = $"{selectedRecipe.OutputItem.ItemName} x{selectedRecipe.OutputQuantity}";
            else if (selectedInputItem != null)
                outputNameText.text = "No valid grinder recipe for this item.";
            else
                outputNameText.text = "Output will appear here.";
        }
    }

    private void UpdateConfirmState()
    {
        var canConfirm = !isProcessing
            && selectedRecipe != null
            && inventorySystem != null
            && inventorySystem.CanFulfillRequirements(selectedRecipe.Requirements)
            && inventorySystem.CanAddAfterConsuming(selectedRecipe.OutputItem, selectedRecipe.Requirements, selectedRecipe.OutputQuantity);

        if (confirmButton != null)
            confirmButton.interactable = canConfirm;

        if (outputIconButton != null)
            outputIconButton.interactable = canConfirm;

        if (outputSlotButton != null)
            outputSlotButton.interactable = canConfirm;
    }

    private void SetProgressVisible(bool value)
    {
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(value);
    }

    private void SetProgress01(float value)
    {
        if (progressSlider == null)
            return;

        progressSlider.value = Mathf.Clamp01(value) * 100f;
    }

    private void SetBackpackDragEnabled(bool value)
    {
        for (var i = 0; i < backpackSlots.Count; i++)
        {
            if (backpackSlots[i] != null)
                backpackSlots[i].SetDragEnabled(value);
        }
    }

    private void HandleInventoryChanged(ItemData _, int __)
    {
        RebuildBackpackList();
        selectedRecipe = FindMatchingRecipe(selectedInputItem);
        UpdateOutputPreview();
        UpdateConfirmState();
    }

    private void SetOpen(bool value)
    {
        if (isOpen == value)
            return;

        if (debugUiInputLogs)
            Debug.Log($"[ProcessingStationUI] SetOpen({value}) frame={Time.frameCount} previous={isOpen}");

        if (value)
            openInstanceCount++;
        else if (openInstanceCount > 0)
            openInstanceCount--;

        isOpen = value;

        if (processingWindow != null)
            processingWindow.SetActive(value);
    }

    private bool IsWindowVisible()
    {
        if (!gameObject.activeInHierarchy)
            return false;

        if (processingWindow == null)
            return false;

        return processingWindow.activeInHierarchy;
    }

    private void CacheBackpackSlots()
    {
        backpackSlots.Clear();

        if (backpackListContainer == null)
            return;

        backpackListContainer.GetComponentsInChildren(true, backpackSlots);
        WarnIfSlotBindingsMissing();
    }

    private void WarnIfSlotBindingsMissing()
    {
        if (warnedMissingSlotBindings)
            return;

        if (backpackListContainer != null && backpackSlots.Count > 0)
            return;

        warnedMissingSlotBindings = true;
        Debug.LogWarning("[ProcessingStationUI] Assign Backpack List Container and add prebuilt InventorySlotUI children for the processing backpack area.");
    }

    private void WarnIfInventoryExceedsDisplayedSlots()
    {
        if (warnedInsufficientSlots)
            return;

        warnedInsufficientSlots = true;
        Debug.LogWarning($"[ProcessingStationUI] Inventory has more items than available processing UI slots under '{backpackListContainer.name}'. Extra items will not be shown.");
    }

    private int GetReservedQuantityForSlot(int slotIndex)
    {
        return selectedInputSourceSlotIndex == slotIndex && selectedInputItem != null ? 1 : 0;
    }

    private void AutoBindUiReferences()
    {
        if (processingWindow == null)
            processingWindow = transform.Find("ProcessingWindow")?.gameObject;

        if (processingWindow == null)
        {
            var processingPanel = transform.Find("Processing Panel");
            if (processingPanel == null)
                processingPanel = transform.Find("ProcessingPanel");
            if (processingPanel == null)
                processingPanel = transform.Find("Window");

            if (processingPanel != null)
                processingWindow = processingPanel.gameObject;
        }

        if (processingWindow == null)
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var name = child.name;
                if (name.IndexOf("process", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("window", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("panel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    processingWindow = child.gameObject;
                    break;
                }
            }
        }

        if (processingWindow == null)
            processingWindow = gameObject;

        if (inputSlot == null)
            inputSlot = GetComponentInChildren<ProcessingInputSlotUI>(true);

        if (outputIconImage == null)
        {
            var outputIconTransform = transform.Find("ProcessingWindow/OutputIcon");
            if (outputIconTransform == null)
                outputIconTransform = transform.Find("OutputIcon");

            if (outputIconTransform != null)
                outputIconImage = outputIconTransform.GetComponent<Image>();
        }

        if (outputNameText == null)
        {
            var outputNameTransform = transform.Find("ProcessingWindow/OutputName");
            if (outputNameTransform == null)
                outputNameTransform = transform.Find("ProcessingWindow/Output Name");
            if (outputNameTransform == null)
                outputNameTransform = transform.Find("OutputName");

            if (outputNameTransform != null)
                outputNameText = outputNameTransform.GetComponent<TMP_Text>();
        }

        if (outputIconButton == null && outputIconImage != null)
            outputIconButton = outputIconImage.GetComponent<Button>();

        if (outputSlotButton == null)
        {
            var outputSlotTransform = transform.Find("ProcessingWindow/Output Slot");
            if (outputSlotTransform == null)
                outputSlotTransform = transform.Find("Output Slot");
            if (outputSlotTransform == null && outputIconImage != null && outputIconImage.transform.parent != null)
                outputSlotTransform = outputIconImage.transform.parent;

            if (outputSlotTransform != null)
                outputSlotButton = outputSlotTransform.GetComponent<Button>();
        }

        if (confirmButton == null)
            confirmButton = transform.Find("ProcessingWindow/ConfirmButton")?.GetComponent<Button>();

        if (closeButton == null)
            closeButton = FindBestCloseButton();

        if (progressSlider == null)
        {
            var progressTransform = transform.Find("ProcessingWindow/ProgressSlider");
            if (progressTransform == null)
                progressTransform = transform.Find("ProcessingWindow/Progress Bar");
            if (progressTransform == null)
                progressTransform = transform.Find("ProgressSlider");

            if (progressTransform != null)
                progressSlider = progressTransform.GetComponent<Slider>();
        }

        if (stationTitleText == null)
        {
            var titleTransform = transform.Find("ProcessingWindow/StationTitle");
            if (titleTransform == null)
                titleTransform = transform.Find("StationTitle");

            if (titleTransform != null)
                stationTitleText = titleTransform.GetComponent<TMP_Text>();
        }

        if (statusText == null)
        {
            var statusTransform = transform.Find("ProcessingWindow/StatusText");
            if (statusTransform == null)
                statusTransform = transform.Find("StatusText");

            if (statusTransform != null)
                statusText = statusTransform.GetComponent<TMP_Text>();
        }

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 100f;
            progressSlider.wholeNumbers = false;
        }
    }

    private void EnsureOutputClickBinding()
    {
        if (outputIconImage == null)
            return;

        outputIconImage.raycastTarget = true;

        if (outputIconButton == null)
            outputIconButton = outputIconImage.GetComponent<Button>();

        if (outputIconButton == null)
            outputIconButton = outputIconImage.gameObject.AddComponent<Button>();

        outputIconButton.transition = Selectable.Transition.ColorTint;
        outputIconButton.targetGraphic = outputIconImage;
        outputIconButton.onClick.RemoveListener(ConfirmProcessing);
        outputIconButton.onClick.AddListener(ConfirmProcessing);
        outputIconButton.interactable = false;

        if (outputSlotButton == null && outputIconImage.transform.parent != null)
            outputSlotButton = outputIconImage.transform.parent.GetComponent<Button>();

        if (outputSlotButton == null && outputIconImage.transform.parent != null)
            outputSlotButton = outputIconImage.transform.parent.gameObject.AddComponent<Button>();

        if (outputSlotButton != null)
        {
            var slotGraphic = outputSlotButton.GetComponent<Graphic>();
            if (slotGraphic != null)
                slotGraphic.raycastTarget = true;

            outputSlotButton.transition = Selectable.Transition.ColorTint;
            outputSlotButton.targetGraphic = slotGraphic;
            outputSlotButton.onClick.RemoveListener(ConfirmProcessing);
            outputSlotButton.onClick.AddListener(ConfirmProcessing);
            outputSlotButton.interactable = false;
        }
    }

    private Button FindBestCloseButton()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null || button == outputIconButton || button == outputSlotButton || button == confirmButton)
                continue;

            var name = button.name;
            if (name.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("exit", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name == "X")
            {
                return button;
            }
        }

        return null;
    }

    private void DisableNonInteractiveTextRaycasts()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null)
                continue;

            if (closeButton != null && text.gameObject == closeButton.gameObject)
                continue;

            text.raycastTarget = false;
        }
    }

}
