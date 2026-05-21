using UnityEngine;
using UnityEngine.UI;

public class BackpackButton : MonoBehaviour
{
    [SerializeField] private BackpackUI backpackUI;
    [SerializeField] private bool hideWhenLocked = true;

    private static bool endingUiSuppressed;

    private Button button;
    private CanvasGroup canvasGroup;
    private bool hasAppliedInteractableState;
    private bool lastInteractableState = true;
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

        RefreshInteractableState();
    }

    public static void SetEndingUiSuppressed(bool value)
    {
        endingUiSuppressed = value;

        var buttons = FindObjectsByType<BackpackButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].RefreshInteractableState();
        }
    }

    private void OnEnable()
    {
        TrySubscribeToFlags();
        RefreshInteractableState();
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
        if (!IsBackpackAvailable())
            return;

        if (BackpackUI.IsAnyOpen)
        {
            BackpackUI.CloseAnyOpen();
            return;
        }

        var journalUI = FindFirstObjectByType<JournalUI>(FindObjectsInactive.Include);
        if (journalUI != null && journalUI.IsOpen)
            journalUI.Close();

        backpackUI.Toggle();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        RefreshInteractableState();
    }

    private void RefreshInteractableState()
    {
        if (button == null)
            return;

        var isInteractable = IsBackpackAvailable();
        if (hasAppliedInteractableState && isInteractable == lastInteractableState)
            return;

        button.interactable = isInteractable;

        if (hideWhenLocked && canvasGroup != null)
        {
            canvasGroup.alpha = isInteractable ? 1f : 0f;
            canvasGroup.interactable = isInteractable;
            canvasGroup.blocksRaycasts = isInteractable;
        }

        lastInteractableState = isInteractable;
        hasAppliedInteractableState = true;
    }

    private bool IsBackpackAvailable()
    {
        return !endingUiSuppressed
            && backpackUI != null
            && backpackUI.IsUnlocked
            && !BrewingStationUI.IsAnyOpen
            && !ProcessingStationUI.IsAnyOpen
            && !BackpackUI.IsModalLocked;
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
