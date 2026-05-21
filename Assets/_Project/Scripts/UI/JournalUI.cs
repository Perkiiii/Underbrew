using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Underbrew.Core;

public class JournalUI : MonoBehaviour
{
    [System.Serializable]
    public class JournalTab
    {
        public string tabName;
        public Button tabButton;
        public Sprite pageSprite;
        public JournalTabPageUI pageController;
    }

    [SerializeField] private GameObject journalWindow;
    [SerializeField] private Image pageImage;
    [SerializeField] private List<JournalTab> tabs = new();
    [SerializeField] private bool closeOnEscape = true;
    [SerializeField] private bool useModalLockWhenOpen = true;
    [SerializeField] private bool requireUnlockFlag;
    [SerializeField] private string unlockFlagKey = "feature.journal.unlocked";
    [SerializeField] private int defaultTabIndex;
    [SerializeField] private bool debugUiInputLogs;

    private bool isOpen;
    private bool warnedNoTabsConfigured;
    private int currentTabIndex = -1;
    private static int lastEscapeCloseFrame = -1;
    private static int openInstanceCount;
    private static bool endingUiSuppressed;
    private bool hasModalLock;
    private readonly List<UnityAction> tabButtonCallbacks = new();
    private CanvasGroup journalWindowCanvasGroup;
    private bool useCanvasGroupVisibility;

    public bool IsOpen => isOpen;
    public bool IsUnlocked => EvaluateUnlocked();
    public static bool IsEndingUiSuppressed => endingUiSuppressed;
    public static bool WasClosedByEscapeThisFrame => lastEscapeCloseFrame == Time.frameCount;
    public static bool IsAnyOpen => openInstanceCount > 0;
    public static void CloseAnyOpen()
    {
        var instances = FindObjectsByType<JournalUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < instances.Length; i++)
        {
            var instance = instances[i];
            if (instance == null || !instance.IsOpen)
                continue;

            instance.Close();
        }
    }

    public static void SetEndingUiSuppressed(bool value)
    {
        endingUiSuppressed = value;

        if (endingUiSuppressed)
            CloseAnyOpen();
    }

    private void Awake()
    {
        if (GameStateFlags.Instance != null)
            GameStateFlags.Instance.OnFlagChanged += HandleFlagChanged;

        WireTabButtons();
        SetupWindowVisibilityMode();

        ApplyWindowVisibility(false);

        isOpen = false;
        ApplyUnlockState();

        if (tabs.Count > 0)
            SetTabInternal(Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1));
    }

    private void OnDisable()
    {
        if (isOpen)
            Close();
    }

    private void OnDestroy()
    {
        UnwireTabButtons();

        if (GameStateFlags.Instance != null)
            GameStateFlags.Instance.OnFlagChanged -= HandleFlagChanged;

        if (hasModalLock)
        {
            BackpackUI.ReleaseModalLock();
            hasModalLock = false;
        }
    }

    private void Update()
    {
        if (!EvaluateUnlocked())
        {
            if (isOpen)
                Close();

            return;
        }

        if (Keyboard.current == null)
            return;

        if (endingUiSuppressed)
            return;

        if (!isOpen && BackpackUI.IsModalLocked)
            return;

        // Toggle with J key
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            if (debugUiInputLogs)
                Debug.Log($"[JournalUI] J key toggled at frame={Time.frameCount}");

            if (BackpackUI.IsAnyOpen)
                BackpackUI.CloseAnyOpen();

            Toggle();
            return;
        }

        // Close with Escape key (only when open)
        if (isOpen && closeOnEscape && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            lastEscapeCloseFrame = Time.frameCount;
            BackpackUI.MarkModalEscapeConsumedThisFrame();

            if (debugUiInputLogs)
                Debug.Log($"[JournalUI] Escape close consumed at frame={Time.frameCount}");

            Close();
        }
    }

    public void Toggle()
    {
        if (!EvaluateUnlocked())
            return;

        if (isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (!EvaluateUnlocked())
            return;

        if (endingUiSuppressed)
            return;

        if (IsAnyOpen && !isOpen)
            return;

        if (isOpen)
            return;

        if (tabs.Count == 0 && !warnedNoTabsConfigured)
        {
            warnedNoTabsConfigured = true;
            Debug.LogWarning("[JournalUI] No tabs configured. Opening window without tab image switching.");
        }

        if (currentTabIndex < 0 || currentTabIndex >= tabs.Count)
        {
            if (tabs.Count > 0)
                SetTabInternal(Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1));
        }

        SetOpen(true);
        ApplyCurrentTabPresentation();
        AudioManager.Instance?.PlayUi(AudioCueId.UIJournalOpen);

        if (useModalLockWhenOpen && !hasModalLock)
        {
            BackpackUI.AcquireModalLock();
            hasModalLock = true;
        }
    }

    public void Close()
    {
        if (!isOpen)
            return;

        SetOpen(false);
        HideAllTabPages();
        AudioManager.Instance?.PlayUi(AudioCueId.UIJournalClose);

        if (useModalLockWhenOpen && hasModalLock)
        {
            BackpackUI.ReleaseModalLock();
            hasModalLock = false;
        }
    }

    public void SetTab(int tabIndex)
    {
        if (tabs.Count == 0)
            return;

        if (tabIndex < 0 || tabIndex >= tabs.Count)
            return;

        SetTabInternal(tabIndex);
    }

    public void NextTab()
    {
        if (tabs.Count == 0)
            return;

        var nextIndex = (currentTabIndex + 1 + tabs.Count) % tabs.Count;
        SetTabInternal(nextIndex);
    }

    public void PreviousTab()
    {
        if (tabs.Count == 0)
            return;

        var previousIndex = (currentTabIndex - 1 + tabs.Count) % tabs.Count;
        SetTabInternal(previousIndex);
    }

    private void SetOpen(bool value)
    {
        if (isOpen == value)
            return;

        if (value)
            openInstanceCount++;
        else if (openInstanceCount > 0)
            openInstanceCount--;

        isOpen = value;

        ApplyWindowVisibility(value);

        if (debugUiInputLogs)
            Debug.Log($"[JournalUI] SetOpen({value}) frame={Time.frameCount} tabIndex={currentTabIndex}");
    }

    private void SetTabInternal(int tabIndex)
    {
        var previousTabIndex = currentTabIndex;
        currentTabIndex = tabIndex;

        if (isOpen)
            ApplyCurrentTabPresentation();

        if (isOpen && previousTabIndex >= 0 && previousTabIndex != tabIndex)
            AudioManager.Instance?.PlayUi(AudioCueId.UITab);

        if (debugUiInputLogs && tabIndex >= 0 && tabIndex < tabs.Count)
            Debug.Log($"[JournalUI] SetTab index={tabIndex} name='{tabs[tabIndex].tabName}' frame={Time.frameCount}");
    }

    private void ApplyCurrentTabPresentation()
    {
        if (tabs.Count == 0 || currentTabIndex < 0 || currentTabIndex >= tabs.Count)
        {
            if (pageImage != null)
                pageImage.gameObject.SetActive(false);

            return;
        }

        var currentTab = tabs[currentTabIndex];
        var currentController = currentTab != null ? currentTab.pageController : null;
        var currentSprite = currentTab != null ? currentTab.pageSprite : null;

        if (pageImage != null)
        {
            pageImage.sprite = currentSprite;
            pageImage.gameObject.SetActive(currentSprite != null);
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            var controller = tabs[i].pageController;
            if (controller == null)
                continue;

            var isSelectedTab = i == currentTabIndex;
            var shouldShow = isSelectedTab && controller.CanRenderPage;
            controller.SetVisible(shouldShow);
        }
    }

    private void HideAllTabPages()
    {
        for (var i = 0; i < tabs.Count; i++)
        {
            var controller = tabs[i].pageController;
            if (controller != null)
                controller.SetVisible(false);
        }
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

    private void ApplyUnlockState()
    {
        if (EvaluateUnlocked())
            return;

        if (isOpen)
            Close();
    }

    private void WireTabButtons()
    {
        tabButtonCallbacks.Clear();

        for (var i = 0; i < tabs.Count; i++)
        {
            var index = i;
            var button = tabs[i].tabButton;

            UnityAction callback = () => SetTab(index);
            tabButtonCallbacks.Add(callback);

            if (button == null)
                continue;

            button.onClick.AddListener(callback);
        }
    }

    private void UnwireTabButtons()
    {
        for (var i = 0; i < tabs.Count; i++)
        {
            var button = tabs[i].tabButton;
            if (button == null)
                continue;

            if (i < tabButtonCallbacks.Count)
                button.onClick.RemoveListener(tabButtonCallbacks[i]);
        }

        tabButtonCallbacks.Clear();
    }

    private void SetupWindowVisibilityMode()
    {
        useCanvasGroupVisibility = false;
        journalWindowCanvasGroup = null;

        if (journalWindow == null)
            return;

        if (journalWindow != gameObject)
            return;

        useCanvasGroupVisibility = true;
        journalWindowCanvasGroup = journalWindow.GetComponent<CanvasGroup>();

        if (journalWindowCanvasGroup == null)
            journalWindowCanvasGroup = journalWindow.AddComponent<CanvasGroup>();
    }

    private void ApplyWindowVisibility(bool visible)
    {
        if (journalWindow == null)
            return;

        if (useCanvasGroupVisibility)
        {
            journalWindowCanvasGroup.alpha = visible ? 1f : 0f;
            journalWindowCanvasGroup.interactable = visible;
            journalWindowCanvasGroup.blocksRaycasts = visible;
            return;
        }

        journalWindow.SetActive(visible);
    }
}
