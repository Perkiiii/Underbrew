using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InGameGUI : MonoBehaviour
{
    public static InGameGUI Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject backpackPanel;
    [SerializeField] private GameObject journalPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject interactionPrompt;

    [Header("Pause Menu Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    public GameObject SettingsPanel => settingsPanel;
    public GameObject BackpackPanel => backpackPanel;
    public GameObject JournalPanel => journalPanel;
    public GameObject DialoguePanel => dialoguePanel;
    public GameObject InteractionPrompt => interactionPrompt;
    public Button ContinueButton => continueButton;
    public Button SaveButton => saveButton;
    public Button SettingsButton => settingsButton;
    public Button ExitButton => exitButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        AutoResolveReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Auto Wire GUI References")]
    public void AutoResolveReferences()
    {
        settingsPanel = ResolveGameObject(settingsPanel, "SettingsPanel");
        backpackPanel = ResolveGameObject(backpackPanel, "BackpackPanel");
        journalPanel = ResolveGameObject(journalPanel, "JournalPanel");
        dialoguePanel = ResolveGameObject(dialoguePanel, "DialoguePanel");
        interactionPrompt = ResolveGameObject(interactionPrompt, "InteractionPrompt");

        continueButton = ResolveButton(continueButton, "Continue Button", "Continue");
        saveButton = ResolveButton(saveButton, "Save Button", "Save");
        settingsButton = ResolveButton(settingsButton, "Settings Button", "Settings");
        exitButton = ResolveButton(exitButton, "Exit Button", "Exit");
    }

    private GameObject ResolveGameObject(GameObject current, string expectedName)
    {
        if (current != null)
            return current;

        var match = FindDescendantByName(transform, expectedName);
        return match != null ? match.gameObject : null;
    }

    private Button ResolveButton(Button current, params string[] expectedNames)
    {
        if (current != null)
            return current;

        for (var i = 0; i < expectedNames.Length; i++)
        {
            var expectedName = expectedNames[i];
            var match = FindDescendantByName(transform, expectedName);
            if (match == null)
                continue;

            var button = match.GetComponent<Button>();
            if (button != null)
                return button;
        }

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string expectedName)
    {
        if (root == null || string.IsNullOrWhiteSpace(expectedName))
            return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (!string.Equals(candidate.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return candidate;
        }

        return null;
    }
}
