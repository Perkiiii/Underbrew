using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using Underbrew.Core;

public class InGameMenuUI : MonoBehaviour
{
    private const string MainMenuSceneName = "UG_MENU_MAIN";

    [SerializeField] private string menuSceneName = "UG_MENU_MAIN";
    [SerializeField] private Button continueButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private bool openOnEscape = true;
    [SerializeField] private bool closeOnEscapeWhenOpen = true;
    [SerializeField] private bool lockPlayerInputWhenOpen = true;
    [SerializeField] private bool debugUiInputLogs;

    private Player cachedPlayer;
    private Component cachedGuiRoot;
    private bool isOpen;
    private bool warnedMissingMenuPanel;

    private void Awake()
    {
        ResolveUiReferences();
        RefreshButtonBindings();

        ApplyMenuVisibility(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        cachedPlayer = FindFirstObjectByType<Player>();
        ApplyOpenState(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        ClearButtonBindings();
    }

    private void Update()
    {
        if (!openOnEscape || Keyboard.current == null)
            return;

        if (IsInMainMenuScene())
            return;

        if (menuPanel == null)
            ResolveUiReferences();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool anyModalOpen = BackpackUI.IsModalLocked;
            bool modalEscapeConsumed = BackpackUI.WasModalEscapeConsumedThisFrame;

            if (debugUiInputLogs)
            {
                Debug.Log($"[InGameMenuUI] Escape pressed frame={Time.frameCount} isOpen={isOpen} modalLocked={anyModalOpen} modalEscapeConsumed={modalEscapeConsumed} modalCount={BackpackUI.ModalLockCount}");
            }

            if (anyModalOpen || modalEscapeConsumed)
            {
                if (debugUiInputLogs)
                    Debug.Log($"[InGameMenuUI] Escape ignored by guard at frame={Time.frameCount}");
                return;
            }

            bool hasSeparateSettingsPanel = settingsPanel != null && settingsPanel != menuPanel;
            if (hasSeparateSettingsPanel && settingsPanel.activeSelf)
            {
                if (debugUiInputLogs)
                    Debug.Log($"[InGameMenuUI] Escape closing settings panel at frame={Time.frameCount}");
                CloseSettings();
                return;
            }

            if (isOpen && closeOnEscapeWhenOpen)
            {
                if (debugUiInputLogs)
                    Debug.Log($"[InGameMenuUI] Escape closing menu at frame={Time.frameCount}");
                SetMenuOpen(false, playSound: false);
            }
            else if (!isOpen)
            {
                if (debugUiInputLogs)
                    Debug.Log($"[InGameMenuUI] Escape opening menu at frame={Time.frameCount}");
                SetMenuOpen(true, playSound: false);
            }
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedPlayer = FindFirstObjectByType<Player>();
        ClearButtonBindings();

        if (IsInMainMenuScene(scene.name))
        {
            menuPanel = null;
            settingsPanel = null;
            isOpen = false;
            return;
        }

        ResolveUiReferences();
        RefreshButtonBindings();
        ApplyOpenState(false);
    }

    public void SetSaveButton(Button button)
    {
        WireButton(saveButton, SaveGame, removeOnly: true);
        saveButton = button;
        WireButton(saveButton, SaveGame);
        EnsurePauseButtonHoverVisuals(saveButton);
    }

    public void SetContinueButton(Button button)
    {
        WireButton(continueButton, CloseMenu, removeOnly: true);
        continueButton = button;
        WireButton(continueButton, CloseMenu);
        EnsurePauseButtonHoverVisuals(continueButton);
    }

    public void SetSettingsButton(Button button)
    {
        WireButton(settingsButton, ToggleSettings, removeOnly: true);
        settingsButton = button;
        WireButton(settingsButton, ToggleSettings);
    }

    public void SetExitButton(Button button)
    {
        WireButton(exitButton, QuitToMenu, removeOnly: true);
        exitButton = button;
        WireButton(exitButton, QuitToMenu);
    }

    public void SetMenuPanel(GameObject panel)
    {
        menuPanel = panel;

        if (menuPanel != null)
            ApplyMenuVisibility(isOpen);
    }

    public void OpenMenu()
    {
        SetMenuOpen(true, playSound: true);
    }

    public void CloseMenu()
    {
        SetMenuOpen(false, playSound: true);
    }

    public void ToggleMenu()
    {
        SetMenuOpen(!isOpen, playSound: true);
    }

    public void QuitToMenu()
    {
        AudioManager.Instance?.PlayUiTransitionSafe(AudioCueId.UIMenuClick);

        if (string.IsNullOrWhiteSpace(menuSceneName))
        {
            Debug.LogWarning("[InGameMenuUI] Menu scene name is blank.");
            return;
        }

        ApplyOpenState(false);

        if (SceneTransitionManager.Instance != null)
        {
            bool started = SceneTransitionManager.Instance.RequestTransition(menuSceneName, string.Empty, Direction.Down);
            if (started)
                return;

            Debug.LogWarning("[InGameMenuUI] Transition request was rejected, falling back to direct scene load.");
        }

        if (SceneTransitionManager.Instance == null)
            Debug.LogWarning("[InGameMenuUI] SceneTransitionManager is missing, falling back to direct scene load.");

        SceneManager.LoadScene(menuSceneName);
    }

    public void SaveGame()
    {
        AudioManager.Instance?.PlayUi(AudioCueId.UIMenuClick);

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[InGameMenuUI] SaveManager is missing, cannot save.");
            return;
        }

        if (!SaveManager.Instance.SaveCurrentToDisk())
            Debug.LogWarning("[InGameMenuUI] Save failed.");
    }

    public void OpenSettings()
    {
        AudioManager.Instance?.PlayUi(AudioCueId.UIMenuClick);

        if (settingsPanel == null)
        {
            Debug.LogWarning("[InGameMenuUI] Settings panel is not assigned.");
            return;
        }

        settingsPanel.SetActive(true);
    }

    public void ToggleSettings()
    {
        AudioManager.Instance?.PlayUi(AudioCueId.UIMenuClick);

        if (settingsPanel == null)
        {
            Debug.LogWarning("[InGameMenuUI] Settings panel is not assigned.");
            return;
        }

        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void CloseSettings()
    {
        CloseSettings(playSound: true);
    }

    private void CloseSettings(bool playSound)
    {
        if (playSound)
            AudioManager.Instance?.PlayUi(AudioCueId.UIMenuClick);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void SetMenuOpen(bool value, bool playSound)
    {
        if (playSound)
            AudioManager.Instance?.PlayUi(AudioCueId.UIMenuClick);

        ApplyOpenState(value);
    }

    private void ApplyOpenState(bool value)
    {
        if (value)
            CloseSettings(playSound: false);

        var visibilityApplied = ApplyMenuVisibility(value);
        isOpen = value && visibilityApplied;

        if (lockPlayerInputWhenOpen)
            SetPlayerInputEnabled(!isOpen);

        if (!isOpen)
            CloseSettings(playSound: false);

        if (value && !visibilityApplied && debugUiInputLogs)
            Debug.Log($"[InGameMenuUI] OpenMenu aborted at frame={Time.frameCount} because menu panel visibility could not be applied.");
    }

    private bool ApplyMenuVisibility(bool visible)
    {
        if (menuPanel == null)
            ResolveUiReferences();

        if (menuPanel == null)
        {
            if (!warnedMissingMenuPanel)
            {
                warnedMissingMenuPanel = true;
                Debug.LogWarning("[InGameMenuUI] Menu panel is missing. Assign it in the inspector or ensure a 'SettingsPanel' object exists in the UI root.");
            }
            return false;
        }

        if (menuPanel == gameObject)
            return false;

        menuPanel.SetActive(visible);
        return true;
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<Player>();

        if (cachedPlayer == null || cachedPlayer.input == null)
            return;

        if (enabled)
            cachedPlayer.input.Player.Enable();
        else
            cachedPlayer.input.Player.Disable();
    }

    private void WireButton(Button button, UnityEngine.Events.UnityAction action, bool removeOnly = false)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);

        if (!removeOnly)
            button.onClick.AddListener(action);
    }

    private void ResolveUiReferences()
    {
        cachedGuiRoot = FindGuiRootComponent();
        InvokeNoArgMethod(cachedGuiRoot, "AutoResolveReferences");

        if (menuPanel == null && cachedGuiRoot != null)
            menuPanel = ReadPropertyOrField<GameObject>(cachedGuiRoot, "SettingsPanel");

        if (settingsPanel == null && cachedGuiRoot != null)
            settingsPanel = ReadPropertyOrField<GameObject>(cachedGuiRoot, "SettingsPanel");

        if (continueButton == null && cachedGuiRoot != null)
            continueButton = ReadPropertyOrField<Button>(cachedGuiRoot, "ContinueButton");

        if (saveButton == null && cachedGuiRoot != null)
            saveButton = ReadPropertyOrField<Button>(cachedGuiRoot, "SaveButton");

        if (settingsButton == null && cachedGuiRoot != null)
            settingsButton = ReadPropertyOrField<Button>(cachedGuiRoot, "SettingsButton");

        if (exitButton == null && cachedGuiRoot != null)
            exitButton = ReadPropertyOrField<Button>(cachedGuiRoot, "ExitButton");

        menuPanel = ResolvePanel(menuPanel, "GUI", "InGameMenu", "MenuPanel", "PausePanel", "SettingsPanel");
        settingsPanel = ResolvePanel(settingsPanel, "SettingsPanel");

        continueButton = ResolveButton(continueButton, "Continue Button", "Continue");
        saveButton = ResolveButton(saveButton, "Save Button", "Save");
        settingsButton = ResolveButton(settingsButton, "Settings Button", "Settings");
        exitButton = ResolveButton(exitButton, "Exit Button", "Exit");

        EnsurePauseButtonHoverVisuals(continueButton);
        EnsurePauseButtonHoverVisuals(saveButton);
        EnsurePauseButtonHoverVisuals(settingsButton);
        EnsurePauseButtonHoverVisuals(exitButton);
    }

    private void ClearButtonBindings()
    {
        WireButton(continueButton, CloseMenu, removeOnly: true);
        WireButton(saveButton, SaveGame, removeOnly: true);
        WireButton(settingsButton, ToggleSettings, removeOnly: true);
        WireButton(exitButton, QuitToMenu, removeOnly: true);
    }

    private void RefreshButtonBindings()
    {
        WireButton(continueButton, CloseMenu);
        WireButton(saveButton, SaveGame);
        WireButton(settingsButton, ToggleSettings);
        WireButton(exitButton, QuitToMenu);
    }

    private static Button ResolveButton(Button current, params string[] expectedNames)
    {
        if (current != null)
            return current;

        if (expectedNames == null || expectedNames.Length == 0)
            return null;

        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allTransforms.Length; i++)
        {
            var candidate = allTransforms[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene.name == MainMenuSceneName)
                continue;

            for (var j = 0; j < expectedNames.Length; j++)
            {
                var expectedName = expectedNames[j];
                if (!string.Equals(candidate.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var button = candidate.GetComponent<Button>();
                if (button != null)
                    return button;
            }
        }

        return null;
    }

    private static void EnsurePauseButtonHoverVisuals(Button button)
    {
        if (button == null)
            return;

        var hoverVisuals = button.GetComponent("PauseButtonHoverVisuals");
        if (hoverVisuals == null)
        {
            var hoverType = System.Type.GetType("PauseButtonHoverVisuals") ?? FindTypeByName("PauseButtonHoverVisuals");
            if (hoverType == null)
                return;

            hoverVisuals = button.gameObject.AddComponent(hoverType);
        }

        if (hoverVisuals == null)
            return;

        InvokeNoArgMethod(hoverVisuals, "AutoResolveVisualTargets");
        InvokeBoolMethod(hoverVisuals, "SetHovered", false);
    }

    private static System.Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            var assembly = assemblies[i];
            if (assembly == null)
                continue;

            var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static GameObject ResolvePanel(GameObject currentPanel, params string[] expectedNames)
    {
        if (currentPanel != null)
            return currentPanel;

        if (expectedNames == null || expectedNames.Length == 0)
            return null;

        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allTransforms.Length; i++)
        {
            var candidate = allTransforms[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene.name == "UG_MENU_MAIN")
                continue;

            for (var j = 0; j < expectedNames.Length; j++)
            {
                var expectedName = expectedNames[j];
                if (!string.Equals(candidate.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                return candidate.gameObject;
            }
        }

        return null;
    }

    private static bool IsInMainMenuScene(string sceneName = null)
    {
        var activeSceneName = string.IsNullOrWhiteSpace(sceneName)
            ? SceneManager.GetActiveScene().name
            : sceneName;

        return string.Equals(activeSceneName, MainMenuSceneName, System.StringComparison.Ordinal);
    }

    private static Component FindGuiRootComponent()
    {
        var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allComponents.Length; i++)
        {
            var component = allComponents[i];
            if (component == null)
                continue;

            if (!string.Equals(component.GetType().Name, "InGameGUI", System.StringComparison.Ordinal))
                continue;

            return component;
        }

        return null;
    }

    private static void InvokeNoArgMethod(Component component, string methodName)
    {
        if (component == null || string.IsNullOrWhiteSpace(methodName))
            return;

        var method = component.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
            return;

        method.Invoke(component, null);
    }

    private static void InvokeBoolMethod(Component component, string methodName, bool value)
    {
        if (component == null || string.IsNullOrWhiteSpace(methodName))
            return;

        var method = component.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
            return;

        method.Invoke(component, new object[] { value });
    }

    private static T ReadPropertyOrField<T>(Component component, string memberName) where T : class
    {
        if (component == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        var type = component.GetType();

        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
            return property.GetValue(component) as T;

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return field.GetValue(component) as T;

        return null;
    }
}
