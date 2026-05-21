using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Underbrew.Core;

[System.Serializable]
public class BrewQuestFlagEntry
{
    [SerializeField] private BrewingRecipe recipe;
    [SerializeField] private string flagKey;
    [SerializeField] private bool flagValue = true;
    [SerializeField] private DialogueConversation postBrewConversation;
    [SerializeField] private string postBrewConversationConsumedFlagKey;

    public BrewingRecipe Recipe => recipe;
    public string FlagKey => flagKey;
    public bool FlagValue => flagValue;
    public DialogueConversation PostBrewConversation => postBrewConversation;
    public string PostBrewConversationConsumedFlagKey => postBrewConversationConsumedFlagKey;
}

public class BrewingStationUI : MonoBehaviour
{
    [SerializeField] private GameObject brewingWindow;
    [SerializeField] private bool deactivateRootWhenClosed = true;
    [SerializeField] private Transform backpackListContainer;
    [SerializeField] private BrewingInputSlotUI inputSlotA;
    [SerializeField] private BrewingInputSlotUI inputSlotB;
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
    [SerializeField] private List<BrewQuestFlagEntry> brewQuestFlagEntries = new();

    private readonly List<InventorySlotUI> backpackSlots = new();

    private InventorySystem inventorySystem;
    private BackpackUI backpackUI;
    private CraftingStation activeStation;
    private BrewingRecipe selectedRecipe;
    private ItemData selectedInputA;
    private ItemData selectedInputB;
    private int selectedInputASourceSlotIndex = -1;
    private int selectedInputBSourceSlotIndex = -1;

    private bool isOpen;
    private bool isProcessing;
    private float brewingElapsed;
    private float brewingDuration;
    private bool reopenBackpackOnClose;
    private bool warnedMissingSlotBindings;
    private bool warnedInsufficientSlots;
    private DialogueConversation pendingPostCloseConversation;
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
            confirmButton.onClick.AddListener(ConfirmBrewing);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (inputSlotA != null)
            inputSlotA.Initialize(this, 0);

        if (inputSlotB != null)
            inputSlotB.Initialize(this, 1);

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
            confirmButton.onClick.RemoveListener(ConfirmBrewing);

        if (outputIconButton != null)
            outputIconButton.onClick.RemoveListener(ConfirmBrewing);

        if (outputSlotButton != null)
            outputSlotButton.onClick.RemoveListener(ConfirmBrewing);

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
                Debug.Log($"[BrewingStationUI] Escape close consumed at frame={Time.frameCount}");
            Close();
        }

        if (isProcessing)
            TickBrewing();
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
            Debug.Log($"[BrewingStationUI] Open requested frame={Time.frameCount} station='{station.StationDisplayName}' rootActive={gameObject.activeSelf}");

        AutoBindUiReferences();
        DisableNonInteractiveTextRaycasts();
        EnsureOutputClickBinding();

        if (isOpen)
            return;

        if (!gameObject.activeSelf)
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

        selectedInputA = null;
        selectedInputB = null;
        selectedRecipe = null;
        selectedInputASourceSlotIndex = -1;
        selectedInputBSourceSlotIndex = -1;

        if (inputSlotA != null)
            inputSlotA.Initialize(this, 0);

        if (inputSlotB != null)
            inputSlotB.Initialize(this, 1);

        UpdateOutputPreview();
        RebuildBackpackList();
        UpdateConfirmState();
        SetProgressVisible(false);
        SetProgress01(0f);
        SetOpen(true);

        if (!IsWindowVisible())
        {
            Debug.LogWarning("[BrewingStationUI] Open requested, but brewing window is not visible. Check BrewingWindow reference and hierarchy.");
            SetOpen(false);
            return;
        }

        BackpackUI.AcquireModalLock();

        if (debugUiInputLogs)
            Debug.Log($"[BrewingStationUI] Open success frame={Time.frameCount} modalCount={BackpackUI.ModalLockCount}");
    }

    public void Close()
    {
        if (!isOpen || isProcessing)
            return;

        if (debugUiInputLogs)
            Debug.Log($"[BrewingStationUI] Close requested frame={Time.frameCount} modalCount(before)={BackpackUI.ModalLockCount}");

        activeStation = null;
        selectedInputA = null;
        selectedInputB = null;
        selectedRecipe = null;
        selectedInputASourceSlotIndex = -1;
        selectedInputBSourceSlotIndex = -1;

        if (inputSlotA != null)
            inputSlotA.Clear();

        if (inputSlotB != null)
            inputSlotB.Clear();

        UpdateOutputPreview();
        SetOpen(false);
        SetProgressVisible(false);
        SetProgress01(0f);

        BackpackUI.ReleaseModalLock();

        if (debugUiInputLogs)
            Debug.Log($"[BrewingStationUI] Close complete frame={Time.frameCount} modalCount(after)={BackpackUI.ModalLockCount}");

        if (reopenBackpackOnClose && backpackUI != null && !backpackUI.IsOpen)
            backpackUI.Open();

        reopenBackpackOnClose = false;

        if (deactivateRootWhenClosed)
            gameObject.SetActive(false);

        TryStartPendingPostCloseConversation();
    }

    public void TrySetInputItem(int slotIndex, ItemData itemData, int sourceSlotIndex)
    {
        if (!CanInteract)
            return;

        if (inventorySystem == null || sourceSlotIndex < 0 || sourceSlotIndex >= inventorySystem.Slots.Count)
            return;

        if (slotIndex == 0)
        {
            selectedInputA = itemData;
            selectedInputASourceSlotIndex = sourceSlotIndex;
            if (inputSlotA != null)
                inputSlotA.SetItem(itemData);
        }
        else
        {
            selectedInputB = itemData;
            selectedInputBSourceSlotIndex = sourceSlotIndex;
            if (inputSlotB != null)
                inputSlotB.SetItem(itemData);
        }

        selectedRecipe = FindMatchingRecipe(selectedInputA, selectedInputB);
        RebuildBackpackList();
        UpdateOutputPreview();
        UpdateConfirmState();
        AudioManager.Instance?.PlaySfx(AudioCueId.ProcessAdd);
    }

    public void ClearInputItem(int slotIndex)
    {
        if (!CanInteract)
            return;

        if (slotIndex == 0)
        {
            selectedInputA = null;
            selectedInputASourceSlotIndex = -1;
            if (inputSlotA != null)
                inputSlotA.Clear();
        }
        else
        {
            selectedInputB = null;
            selectedInputBSourceSlotIndex = -1;
            if (inputSlotB != null)
                inputSlotB.Clear();
        }

        selectedRecipe = FindMatchingRecipe(selectedInputA, selectedInputB);
        RebuildBackpackList();
        UpdateOutputPreview();
        UpdateConfirmState();
    }

    public void MoveReservedInputToSlot(int inputSlotIndex, int targetSlotIndex)
    {
        if (!CanInteract || inventorySystem == null)
            return;

        if (targetSlotIndex < 0 || targetSlotIndex >= inventorySystem.Slots.Count)
            return;

        var sourceSlotIndex = inputSlotIndex == 0 ? selectedInputASourceSlotIndex : selectedInputBSourceSlotIndex;
        if (sourceSlotIndex < 0)
            return;

        if (sourceSlotIndex != targetSlotIndex && !inventorySystem.MoveSlotItem(sourceSlotIndex, targetSlotIndex))
            return;

        if (inputSlotIndex == 0)
        {
            selectedInputA = null;
            selectedInputASourceSlotIndex = -1;
            if (inputSlotA != null)
                inputSlotA.Clear();
        }
        else
        {
            selectedInputB = null;
            selectedInputBSourceSlotIndex = -1;
            if (inputSlotB != null)
                inputSlotB.Clear();
        }

        selectedRecipe = FindMatchingRecipe(selectedInputA, selectedInputB);
        RebuildBackpackList();
        UpdateOutputPreview();
        UpdateConfirmState();

        if (sourceSlotIndex == targetSlotIndex)
            AudioManager.Instance?.PlayUi(AudioCueId.UIBackpackMove);
    }

    private void ConfirmBrewing()
    {
        if (!CanInteract || selectedRecipe == null || inventorySystem == null)
            return;

        if (!inventorySystem.CanFulfillRequirements(selectedRecipe.Requirements))
        {
            if (statusText != null)
                statusText.text = "Missing required ingredients.";

            UpdateConfirmState();
            return;
        }

        if (!inventorySystem.CanAddAfterConsuming(selectedRecipe.OutputItem, selectedRecipe.Requirements, selectedRecipe.OutputQuantity))
        {
            if (statusText != null)
                statusText.text = "Backpack is full.";

            UpdateConfirmState();
            return;
        }

        if (!inventorySystem.ConsumeRequirements(selectedRecipe.Requirements))
        {
            if (statusText != null)
                statusText.text = "Missing required ingredients.";

            RebuildBackpackList();
            UpdateConfirmState();
            return;
        }

        isProcessing = true;
        brewingElapsed = 0f;
        brewingDuration = Mathf.Max(0.01f, selectedRecipe.BrewingTime);
        AudioManager.Instance?.PlaySfx(AudioCueId.BrewStart);

        if (statusText != null)
            statusText.text = selectedRecipe.OutputItem != null
                ? $"Brewing {selectedRecipe.OutputItem.ItemName}..."
                : "Brewing...";

        if (inputSlotA != null)
            inputSlotA.SetLocked(true);

        if (inputSlotB != null)
            inputSlotB.SetLocked(true);

        SetBackpackDragEnabled(false);
        SetProgressVisible(true);
        SetProgress01(0f);
        UpdateConfirmState();
    }

    private void TickBrewing()
    {
        brewingElapsed += Time.deltaTime;
        var progress = Mathf.Clamp01(brewingElapsed / brewingDuration);
        SetProgress01(progress);

        if (progress < 1f)
            return;

        CompleteBrewing();
    }

    private void CompleteBrewing()
    {
        var completedRecipe = selectedRecipe;
        var itemCreated = inventorySystem != null
            && completedRecipe != null
            && inventorySystem.Add(completedRecipe.OutputItem, completedRecipe.OutputQuantity);

        if (itemCreated)
        {
            ApplyPostBrewEffectsForRecipe(completedRecipe);
            AudioManager.Instance?.PlaySfx(AudioCueId.BrewComplete);
        }

        if (statusText != null)
            statusText.text = itemCreated && completedRecipe != null && completedRecipe.OutputItem != null
                ? $"Created {completedRecipe.OutputItem.ItemName} x{completedRecipe.OutputQuantity}"
                : "Backpack is full.";

        isProcessing = false;
        brewingElapsed = 0f;
        brewingDuration = 0f;

        selectedInputA = null;
        selectedInputB = null;
        selectedRecipe = null;
        selectedInputASourceSlotIndex = -1;
        selectedInputBSourceSlotIndex = -1;

        if (inputSlotA != null)
        {
            inputSlotA.SetLocked(false);
            inputSlotA.Clear();
        }

        if (inputSlotB != null)
        {
            inputSlotB.SetLocked(false);
            inputSlotB.Clear();
        }

        SetBackpackDragEnabled(true);
        SetProgressVisible(false);
        SetProgress01(0f);
        UpdateOutputPreview();
        RebuildBackpackList();
        UpdateConfirmState();
    }

    private void ApplyPostBrewEffectsForRecipe(BrewingRecipe completedRecipe)
    {
        if (completedRecipe == null || brewQuestFlagEntries == null)
            return;

        for (var i = 0; i < brewQuestFlagEntries.Count; i++)
        {
            var entry = brewQuestFlagEntries[i];
            if (entry == null || entry.Recipe != completedRecipe)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.FlagKey) && GameStateFlags.Instance != null)
                GameStateFlags.Instance.SetFlag(entry.FlagKey, entry.FlagValue);

            if (ShouldQueuePostBrewConversation(entry))
                pendingPostCloseConversation = entry.PostBrewConversation;
        }
    }

    private static bool ShouldQueuePostBrewConversation(BrewQuestFlagEntry entry)
    {
        if (entry == null || entry.PostBrewConversation == null)
            return false;

        var consumedFlagKey = entry.PostBrewConversationConsumedFlagKey;
        if (string.IsNullOrWhiteSpace(consumedFlagKey))
            return true;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return false;

        if (flags.GetFlag(consumedFlagKey))
            return false;

        flags.SetFlag(consumedFlagKey, true);
        return true;
    }

    private void TryStartPendingPostCloseConversation()
    {
        if (pendingPostCloseConversation == null)
            return;

        var manager = DialogueManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<DialogueManager>();

        if (manager == null)
        {
            Debug.LogWarning("[BrewingStationUI] DialogueManager not found. Post-brew conversation was not started.");
            return;
        }

        if (manager.IsDialogueActive)
            return;

        var conversationToStart = pendingPostCloseConversation;
        pendingPostCloseConversation = null;

        if (!manager.StartConversation(conversationToStart))
            Debug.LogWarning("[BrewingStationUI] Could not start post-brew conversation.");
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
            backpackSlots[i].BindProcessingDrag(null, !isProcessing);
            backpackSlots[i].BindDrag(!isProcessing, () => CanInteract, null);
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
            backpackSlots[slotIndex].BindDrag(!isProcessing, () => CanInteract, null);
        }

        if (backpackSlots.Count < inventorySlots.Count)
            WarnIfInventoryExceedsDisplayedSlots();
    }

    private BrewingRecipe FindMatchingRecipe(ItemData inputA, ItemData inputB)
    {
        if (activeStation == null || inputA == null || inputB == null)
            return null;

        var recipes = activeStation.BrewingRecipes;
        if (recipes == null)
            return null;

        for (var i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            if (recipe == null || recipe.StationType != activeStation.StationType)
                continue;

            if (RecipeMatchesInputs(recipe, inputA, inputB))
                return recipe;
        }

        return null;
    }

    private bool RecipeMatchesInputs(BrewingRecipe recipe, ItemData inputA, ItemData inputB)
    {
        var ingredients = recipe.Ingredients;
        if (ingredients == null || ingredients.Length != 2)
            return false;

        var first = ingredients[0].Item;
        var second = ingredients[1].Item;

        var directMatch = first == inputA && second == inputB;
        var reverseMatch = first == inputB && second == inputA;
        return directMatch || reverseMatch;
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
            else if (selectedInputA != null && selectedInputB != null)
                outputNameText.text = "No valid brewing recipe.";
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
        selectedRecipe = FindMatchingRecipe(selectedInputA, selectedInputB);
        UpdateOutputPreview();
        UpdateConfirmState();
    }

    private void SetOpen(bool value)
    {
        if (isOpen == value)
            return;

        if (debugUiInputLogs)
            Debug.Log($"[BrewingStationUI] SetOpen({value}) frame={Time.frameCount} previous={isOpen}");

        if (value)
            openInstanceCount++;
        else if (openInstanceCount > 0)
            openInstanceCount--;

        isOpen = value;

        if (brewingWindow != null)
            brewingWindow.SetActive(value);
    }

    private bool IsWindowVisible()
    {
        if (!gameObject.activeInHierarchy)
            return false;

        if (brewingWindow == null)
            return false;

        return brewingWindow.activeInHierarchy;
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
        Debug.LogWarning("[BrewingStationUI] Assign Backpack List Container and add prebuilt InventorySlotUI children for the brewing backpack area.");
    }

    private void WarnIfInventoryExceedsDisplayedSlots()
    {
        if (warnedInsufficientSlots)
            return;

        warnedInsufficientSlots = true;
        Debug.LogWarning($"[BrewingStationUI] Inventory has more items than available brewing UI slots under '{backpackListContainer.name}'. Extra items will not be shown.");
    }

    private int GetReservedQuantityForSlot(int slotIndex)
    {
        var reservedQuantity = 0;

        if (selectedInputASourceSlotIndex == slotIndex && selectedInputA != null)
            reservedQuantity++;

        if (selectedInputBSourceSlotIndex == slotIndex && selectedInputB != null)
            reservedQuantity++;

        return reservedQuantity;
    }

    private void RemapReservedSourceSlotIndices(int fromSlotIndex, int toSlotIndex)
    {
        selectedInputASourceSlotIndex = RemapSourceSlotIndex(selectedInputASourceSlotIndex, fromSlotIndex, toSlotIndex);
        selectedInputBSourceSlotIndex = RemapSourceSlotIndex(selectedInputBSourceSlotIndex, fromSlotIndex, toSlotIndex);
    }

    private int RemapSourceSlotIndex(int currentSlotIndex, int fromSlotIndex, int toSlotIndex)
    {
        if (currentSlotIndex == fromSlotIndex)
            return toSlotIndex;

        if (currentSlotIndex == toSlotIndex)
            return fromSlotIndex;

        return currentSlotIndex;
    }

    private void AutoBindUiReferences()
    {
        if (brewingWindow == null)
            brewingWindow = transform.Find("BrewingWindow")?.gameObject;

        if (brewingWindow == null)
        {
            var brewingPanel = transform.Find("Brewing Panel");
            if (brewingPanel == null)
                brewingPanel = transform.Find("BrewingPanel");
            if (brewingPanel == null)
                brewingPanel = transform.Find("Window");

            if (brewingPanel != null)
                brewingWindow = brewingPanel.gameObject;
        }

        if (brewingWindow == null)
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var name = child.name;
                if (name.IndexOf("brew", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("window", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("panel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    brewingWindow = child.gameObject;
                    break;
                }
            }
        }

        if (brewingWindow == null)
            brewingWindow = gameObject;

        if (inputSlotA == null)
            inputSlotA = transform.Find("BrewingWindow/Input Slot A")?.GetComponent<BrewingInputSlotUI>();

        if (inputSlotB == null)
            inputSlotB = transform.Find("BrewingWindow/Input Slot B")?.GetComponent<BrewingInputSlotUI>();

        if (inputSlotA == null || inputSlotB == null)
        {
            var inputSlots = GetComponentsInChildren<BrewingInputSlotUI>(true);
            if (inputSlots.Length > 0 && inputSlotA == null)
                inputSlotA = inputSlots[0];
            if (inputSlots.Length > 1 && inputSlotB == null)
                inputSlotB = inputSlots[1];
        }

        if (outputIconImage == null)
        {
            var outputIconTransform = transform.Find("BrewingWindow/OutputIcon");
            if (outputIconTransform == null)
                outputIconTransform = transform.Find("OutputIcon");

            if (outputIconTransform != null)
                outputIconImage = outputIconTransform.GetComponent<Image>();
        }

        if (outputNameText == null)
        {
            var outputNameTransform = transform.Find("BrewingWindow/OutputName");
            if (outputNameTransform == null)
                outputNameTransform = transform.Find("BrewingWindow/Output Name");
            if (outputNameTransform == null)
                outputNameTransform = transform.Find("OutputName");

            if (outputNameTransform != null)
                outputNameText = outputNameTransform.GetComponent<TMP_Text>();
        }

        if (outputIconButton == null && outputIconImage != null)
            outputIconButton = outputIconImage.GetComponent<Button>();

        if (outputSlotButton == null)
        {
            var outputSlotTransform = transform.Find("BrewingWindow/Output Slot");
            if (outputSlotTransform == null)
                outputSlotTransform = transform.Find("Output Slot");
            if (outputSlotTransform == null && outputIconImage != null && outputIconImage.transform.parent != null)
                outputSlotTransform = outputIconImage.transform.parent;

            if (outputSlotTransform != null)
                outputSlotButton = outputSlotTransform.GetComponent<Button>();
        }

        if (confirmButton == null)
            confirmButton = transform.Find("BrewingWindow/ConfirmButton")?.GetComponent<Button>();

        if (closeButton == null)
            closeButton = FindBestCloseButton();

        if (progressSlider == null)
        {
            var progressTransform = transform.Find("BrewingWindow/ProgressSlider");
            if (progressTransform == null)
                progressTransform = transform.Find("BrewingWindow/Progress Bar");
            if (progressTransform == null)
                progressTransform = transform.Find("ProgressSlider");

            if (progressTransform != null)
                progressSlider = progressTransform.GetComponent<Slider>();
        }

        if (stationTitleText == null)
        {
            var titleTransform = transform.Find("BrewingWindow/StationTitle");
            if (titleTransform == null)
                titleTransform = transform.Find("StationTitle");

            if (titleTransform != null)
                stationTitleText = titleTransform.GetComponent<TMP_Text>();
        }

        if (statusText == null)
        {
            var statusTransform = transform.Find("BrewingWindow/StatusText");
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
        outputIconButton.onClick.RemoveListener(ConfirmBrewing);
        outputIconButton.onClick.AddListener(ConfirmBrewing);
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
            outputSlotButton.onClick.RemoveListener(ConfirmBrewing);
            outputSlotButton.onClick.AddListener(ConfirmBrewing);
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
