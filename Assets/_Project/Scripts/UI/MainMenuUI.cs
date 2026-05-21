using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Underbrew.Core;

public class MainMenuUI : MonoBehaviour
{
    private const string HasSavePlayerPrefsKey = "Underbrew.HasSave";
    private const string BootLaunchModePlayerPrefsKey = "Underbrew.BootLaunchMode";

    [Header("Scene Loading")]
    [SerializeField] private string bootSceneName = "UG_BOOT";

    [Header("Save File")]
    [SerializeField] private string saveFileName = "underbrew_save_v1.json";

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Save Detection")]
    [SerializeField] private bool usePlayerPrefsSaveMarker = true;
    [SerializeField] private bool editorPretendSaveExists;

    [Header("Button Visibility")]
    [SerializeField] private bool showContinueOnlyWhenSaveExists = true;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private bool menuClickDiagnostics = false;

    [Header("Transition Retry")]
    [SerializeField] private float transitionRetryTimeoutSeconds = 1.5f;
    [SerializeField] private float transitionRetryIntervalSeconds = 0.05f;

    private bool isWaitingForTransition;
    private bool warnedMissingAudioManager;
    private bool warnedMissingAudioListener;

    [Header("Music")]
    [SerializeField] private float musicFadeDuration = 1.5f;
    [SerializeField] private float musicFadeOutOnTransitionDuration = 0.75f;

    private bool hasStartedMenuExitAudio;

    private bool IsMenuClickDiagnosticsEnabled => menuClickDiagnostics || Application.isEditor || Debug.isDebugBuild;

    private void Awake()
    {
        ResolveButtonReferences();
        EnsureButtonHoverVisuals();

        WireButton(newGameButton, StartNewGame);
        WireButton(continueButton, ContinueGame);
        WireButton(settingsButton, OpenSettings);
        WireButton(exitButton, ExitGame);
    }

    private void Start()
    {
        RefreshMenuState();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        StartMenuMusic();
    }

    private void StartMenuMusic()
    {
        var audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogWarning("[MainMenuUI] Cannot start menu music: AudioManager not found.");
            return;
        }

        audioManager.SetMusicWithFade(AudioCueId.MusicMenuLoop, musicFadeDuration);
    }

    public void RefreshMenuState()
    {
        ResolveButtonReferences();
        EnsureButtonHoverVisuals();

        var hasSave = HasSaveGame();

        if (newGameButton != null)
            newGameButton.gameObject.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(!showContinueOnlyWhenSaveExists || hasSave);

        if (verboseLogging)
            Debug.Log($"[MainMenuUI] RefreshMenuState hasSave={hasSave}, newGame='{GetButtonDebugName(newGameButton)}', continue='{GetButtonDebugName(continueButton)}'");
    }

    public void StartNewGame()
    {
        PlayMenuClick(transitionSensitive: true, actionName: "StartNewGame");

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.ResetRuntimeStateForNewGame();
            SaveManager.Instance.DeleteSave();
        }
        else
            SaveManager.DeleteSaveOnDisk(saveFileName);

        ClearSaveMarker();
        SetBootLaunchMode(1);

        LoadSceneWithTransitionFallback(bootSceneName);
    }

    public void ContinueGame()
    {
        if (!HasSaveGame())
        {
            Debug.LogWarning("[MainMenuUI] Continue requested, but no save was found.");
            RefreshMenuState();
            return;
        }

        PlayMenuClick(transitionSensitive: true, actionName: "ContinueGame");
        SetBootLaunchMode(2);
        LoadSceneWithTransitionFallback(bootSceneName);
    }

    public void OpenSettings()
    {
        PlayMenuClick(transitionSensitive: false, actionName: "OpenSettings");

        if (settingsPanel == null)
        {
            Debug.Log("[MainMenuUI] Settings button pressed, but no settings panel is assigned yet.");
            return;
        }

        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        PlayMenuClick(transitionSensitive: false, actionName: "CloseSettings");

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void ExitGame()
    {
        PlayMenuClick(transitionSensitive: false, actionName: "ExitGame");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetHasSaveMarker(bool hasSave)
    {
        if (!usePlayerPrefsSaveMarker)
            return;

        if (hasSave)
            PlayerPrefs.SetInt(HasSavePlayerPrefsKey, 1);
        else
            PlayerPrefs.DeleteKey(HasSavePlayerPrefsKey);

        PlayerPrefs.Save();
        RefreshMenuState();
    }

    private bool HasSaveGame()
    {
        if (SaveManager.Instance != null)
            return SaveManager.Instance.HasValidSaveFile();

        if (SaveManager.HasValidSaveOnDisk(saveFileName))
            return true;

        if (Application.isEditor && editorPretendSaveExists)
            return true;

        if (!usePlayerPrefsSaveMarker)
            return false;

        return PlayerPrefs.GetInt(HasSavePlayerPrefsKey, 0) == 1;
    }

    private void ClearSaveMarker()
    {
        if (!usePlayerPrefsSaveMarker)
            return;

        PlayerPrefs.DeleteKey(HasSavePlayerPrefsKey);
        PlayerPrefs.Save();
    }

    private void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void LoadSceneWithTransitionFallback(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[MainMenuUI] Scene name is blank; cannot load scene.");
            return;
        }

        if (isWaitingForTransition)
            return;

        BeginMenuExitAudio();

        if (SceneTransitionManager.Instance != null)
        {
            bool started = SceneTransitionManager.Instance.RequestTransition(sceneName, string.Empty, Direction.Down);
            if (started)
                return;

            StartCoroutine(RetryTransitionThenFallback(sceneName));
            return;
        }
        else
        {
            Debug.LogWarning("[MainMenuUI] SceneTransitionManager is missing, falling back to direct scene load.");
        }

        SceneManager.LoadScene(sceneName);
    }

    private void BeginMenuExitAudio()
    {
        if (hasStartedMenuExitAudio)
            return;

        hasStartedMenuExitAudio = true;

        var audioManager = AudioManager.Instance;
        if (audioManager == null)
            return;

        audioManager.SetMusicWithFade(AudioCueId.None, musicFadeOutOnTransitionDuration);
    }

    private IEnumerator RetryTransitionThenFallback(string sceneName)
    {
        isWaitingForTransition = true;
        SetMenuButtonsInteractable(false);

        float elapsed = 0f;
        float retryInterval = Mathf.Max(0.01f, transitionRetryIntervalSeconds);
        float timeout = Mathf.Max(0.1f, transitionRetryTimeoutSeconds);

        while (elapsed < timeout)
        {
            var transitionManager = SceneTransitionManager.Instance;
            if (transitionManager != null)
            {
                bool started = transitionManager.RequestTransition(sceneName, string.Empty, Direction.Down);
                if (started)
                {
                    isWaitingForTransition = false;
                    SetMenuButtonsInteractable(true);
                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(retryInterval);
            elapsed += retryInterval;
        }

        isWaitingForTransition = false;
        SetMenuButtonsInteractable(true);

        Debug.LogWarning("[MainMenuUI] Transition request timed out; falling back to direct scene load.");
        SceneManager.LoadScene(sceneName);
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (newGameButton != null)
            newGameButton.interactable = interactable;

        if (continueButton != null)
            continueButton.interactable = interactable;

        if (settingsButton != null)
            settingsButton.interactable = interactable;

        if (exitButton != null)
            exitButton.interactable = interactable;
    }

    private void SetBootLaunchMode(int mode)
    {
        PlayerPrefs.SetInt(BootLaunchModePlayerPrefsKey, mode);
        PlayerPrefs.Save();
    }

    private void PlayMenuClick(bool transitionSensitive, string actionName)
    {
        var audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            if (!warnedMissingAudioManager)
            {
                warnedMissingAudioManager = true;
                Debug.LogWarning("[MainMenuUI] UIMenuClick skipped because AudioManager.Instance is null. Add a scene-local AudioManager to UG_MENU_MAIN (persistAcrossScenes=false).");
            }
            return;
        }

        if (FindFirstObjectByType<AudioListener>() == null && !warnedMissingAudioListener)
        {
            warnedMissingAudioListener = true;
            Debug.LogWarning("[MainMenuUI] No active AudioListener found in scene. UI audio may be silent in main menu.");
        }

        if (IsMenuClickDiagnosticsEnabled)
        {
            Debug.Log($"[MainMenuUI] Requesting UIMenuClick action={actionName} transitionSensitive={transitionSensitive} scene='{SceneManager.GetActiveScene().name}' frame={Time.frameCount} time={Time.unscaledTime:F3}");
        }

        if (transitionSensitive)
            audioManager.PlayUiTransitionSafe(AudioCueId.UIMenuClick);
        else
            audioManager.PlayUi(AudioCueId.UIMenuClick);
    }

    private void ResolveButtonReferences()
    {
        newGameButton = ResolveButton(newGameButton, "New Game");
        continueButton = ResolveButton(continueButton, "Continue");
        settingsButton = ResolveButton(settingsButton, "Settings");
        exitButton = ResolveButton(exitButton, "Exit");
    }

    private void EnsureButtonHoverVisuals()
    {
        EnsurePauseButtonHoverVisuals(newGameButton);
        EnsurePauseButtonHoverVisuals(continueButton);
        EnsurePauseButtonHoverVisuals(settingsButton);
        EnsurePauseButtonHoverVisuals(exitButton);
    }

    private static void EnsurePauseButtonHoverVisuals(Button button)
    {
        if (button == null)
            return;

        var hoverVisuals = button.GetComponent<PauseButtonHoverVisuals>();
        if (hoverVisuals == null)
            hoverVisuals = button.gameObject.AddComponent<PauseButtonHoverVisuals>();

        hoverVisuals.AutoResolveVisualTargets();
        hoverVisuals.SetHovered(false);
    }

    private Button ResolveButton(Button currentButton, string expectedName)
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (currentButton != null && currentButton.gameObject.scene == activeScene)
            return currentButton;

        Button resolvedButton = FindButtonInActiveScene(expectedName);
        if (resolvedButton != null)
            return resolvedButton;

        return currentButton;
    }

    private static Button FindButtonInActiveScene(string expectedName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (button.gameObject.scene != activeScene)
                continue;

            if (string.Equals(button.gameObject.name, expectedName, StringComparison.OrdinalIgnoreCase))
                return button;
        }

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (button.gameObject.scene != activeScene)
                continue;

            if (button.gameObject.name.IndexOf(expectedName, StringComparison.OrdinalIgnoreCase) >= 0)
                return button;
        }

        return null;
    }

    private static string GetButtonDebugName(Button button)
    {
        if (button == null)
            return "<null>";

        return $"{button.gameObject.name} (scene={button.gameObject.scene.name}, active={button.gameObject.activeInHierarchy})";
    }
}
