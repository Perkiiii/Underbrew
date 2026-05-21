using UnityEngine;
using UnityEngine.UI;

public class JournalButton : MonoBehaviour
{
    [SerializeField] private JournalUI journalUI;
    [SerializeField] private bool hideWhenLocked = true;

    private static bool endingUiSuppressed;

    private Button button;
    private CanvasGroup canvasGroup;
    private bool hasAppliedVisibilityState;
    private bool lastVisibleState = true;
    private bool isFlagSubscribed;

    private void Awake()
    {
        button = GetComponent<Button>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (hideWhenLocked && canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (button != null)
            button.onClick.AddListener(HandleClick);

        TrySubscribeToFlags();

        RefreshVisibilityState();
    }

    public static void SetEndingUiSuppressed(bool value)
    {
        endingUiSuppressed = value;

        var buttons = FindObjectsByType<JournalButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].RefreshVisibilityState();
        }
    }

    private void OnEnable()
    {
        TrySubscribeToFlags();
        RefreshVisibilityState();
    }

    private void OnDisable()
    {
        UnsubscribeFromFlags();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);

        UnsubscribeFromFlags();
    }

    private void HandleClick()
    {
        if (!IsJournalAvailable())
            return;

        if (JournalUI.IsAnyOpen)
        {
            JournalUI.CloseAnyOpen();
            return;
        }

        if (BackpackUI.IsAnyOpen)
            BackpackUI.CloseAnyOpen();

        journalUI.Open();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        RefreshVisibilityState();
    }

    private void RefreshVisibilityState()
    {
        if (button == null)
            return;

        var isVisible = IsJournalAvailable();
        if (hasAppliedVisibilityState && isVisible == lastVisibleState)
            return;

        button.interactable = isVisible;

        if (hideWhenLocked && canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        lastVisibleState = isVisible;
        hasAppliedVisibilityState = true;
    }

    private bool IsJournalAvailable()
    {
        return !endingUiSuppressed && journalUI != null && journalUI.IsUnlocked;
    }

    private void TrySubscribeToFlags()
    {
        if (isFlagSubscribed)
            return;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return;

        flags.OnFlagChanged += HandleFlagChanged;
        isFlagSubscribed = true;
    }

    private void UnsubscribeFromFlags()
    {
        if (!isFlagSubscribed)
            return;

        var flags = GameStateFlags.Instance;
        if (flags != null)
            flags.OnFlagChanged -= HandleFlagChanged;

        isFlagSubscribed = false;
    }
}
