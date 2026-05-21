using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Underbrew.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    private const int BlackoutOverrideSortingOrder = 1000;

    private enum DialogueTextRevealMode
    {
        Instant,
        Fade,
        Typewriter
    }

    public static DialogueUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private Image speakerBackdropImage;
    [SerializeField] private TMP_Text lineText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image promptImage;
    [SerializeField] private string continuePrompt = "Press E to continue";
    [SerializeField] private string closePrompt = "Press E to close";
    [SerializeField] private string choosePrompt = "Choose a response";
    [SerializeField] private string skipPrompt = "Press E or click to skip";
    [SerializeField] private bool animatePromptText = true;
    [SerializeField] [Min(0f)] private float promptFloatDistance = 6f;
    [SerializeField] [Min(0f)] private float promptFloatSpeed = 2f;

    [Header("Text Reveal")]
    [SerializeField] private DialogueTextRevealMode revealMode = DialogueTextRevealMode.Typewriter;
    [SerializeField] [Min(0.01f)] private float typewriterCharactersPerSecond = 45f;
    [SerializeField] [Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField] private bool usePunctuationPauses = true;
    [SerializeField] [Min(0f)] private float shortPunctuationPause = 0.05f;
    [SerializeField] [Min(0f)] private float longPunctuationPause = 0.12f;
    [SerializeField] [Min(0f)] private float submitDebounceDuration = 0.08f;

    [Header("Choices")]
    [SerializeField] private Transform choicesContainer;
    [SerializeField] private DialogueChoiceButtonUI choiceButtonPrefab;

    private readonly List<DialogueChoiceButtonUI> choiceButtons = new();

    private DialogueManager manager;
    private Coroutine revealCoroutine;
    private DialogueNodeViewModel currentModel;
    private bool visible;
    private bool canAdvance;
    private bool isRevealInProgress;
    private bool renderAboveBlackout;
    private int selectedChoiceIndex;
    private float nextSubmitAllowedTime;
    private RectTransform promptImageRectTransform;
    private RectTransform promptTextRectTransform;
    private Vector2 promptImageBaseAnchoredPosition;
    private Vector2 promptTextBaseAnchoredPosition;
    private bool hasPromptImageBasePosition;
    private bool hasPromptTextBasePosition;
    private int blockChoiceSubmitUntilFrame = -1;
    private Canvas dialogueCanvas;
    private bool originalOverrideSorting;
    private int originalSortingOrder;
    private int originalSortingLayerId;
    private bool hasStoredCanvasSortingState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        CacheDialogueCanvas();
        CachePromptBasePositions();
        ApplyVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!visible || manager == null || Keyboard.current == null)
            return;

        AnimatePromptText();

        if (isRevealInProgress)
        {
            if (WasSubmitPressed(nextSubmitAllowedTime))
                CompleteRevealImmediately();

            return;
        }

        if (choiceButtons.Count > 0)
        {
            HandleChoiceInput();
            return;
        }

        if (!canAdvance)
            return;

        if (WasSubmitPressed(nextSubmitAllowedTime))
            manager.AdvanceLine();
    }

    public void Open(DialogueManager owningManager)
    {
        manager = owningManager;
        nextSubmitAllowedTime = Time.unscaledTime + submitDebounceDuration;
        ApplyBlackoutSortingOverrideIfNeeded();
        ApplyVisible(true);
    }

    public void Close()
    {
        StopCurrentReveal();
        currentModel = null;
        manager = null;
        canAdvance = false;
        isRevealInProgress = false;
        ClearChoiceButtons();

        if (speakerText != null)
            speakerText.text = string.Empty;

        if (speakerBackdropImage != null)
            speakerBackdropImage.enabled = false;

        if (lineText != null)
        {
            lineText.text = string.Empty;
            lineText.maxVisibleCharacters = int.MaxValue;
            SetLineTextAlpha(1f);
        }

        if (promptText != null)
            promptText.text = string.Empty;

        if (promptImage != null)
            promptImage.enabled = false;

        ResetPromptAnimation();
        ApplyVisible(false);
        RestoreCanvasSortingOverride();
    }

    public void SetRenderAboveBlackout(bool value)
    {
        renderAboveBlackout = value;

        if (!renderAboveBlackout)
        {
            RestoreCanvasSortingOverride();
            return;
        }

        if (visible)
            ApplyBlackoutSortingOverrideIfNeeded();
    }

    public void ShowNode(DialogueNodeViewModel model)
    {
        if (model == null)
            return;

        currentModel = model;
        nextSubmitAllowedTime = Time.unscaledTime + submitDebounceDuration;

        var hasSpeakerName = !string.IsNullOrWhiteSpace(model.SpeakerName);

        if (speakerText != null)
            speakerText.text = hasSpeakerName ? model.SpeakerName : string.Empty;

        if (speakerBackdropImage != null)
            speakerBackdropImage.enabled = hasSpeakerName;

        ClearChoiceButtons();
        canAdvance = false;
        StartReveal(model);
    }

    private void BuildChoiceButtons(List<DialogueChoiceViewModel> choices)
    {
        ClearChoiceButtons();

        if (choices == null || choices.Count == 0)
            return;

        if (choicesContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("[DialogueUI] Choice container or choice button prefab is missing.");
            return;
        }

        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            var button = Instantiate(choiceButtonPrefab, choicesContainer);
            button.Initialize(choice, HandleChoiceClicked);
            button.SetInteractable(false);
            choiceButtons.Add(button);
        }

        selectedChoiceIndex = 0;
        ApplyChoiceSelectionVisual();
    }

    private void ClearChoiceButtons()
    {
        for (var i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
                Destroy(choiceButtons[i].gameObject);
        }

        choiceButtons.Clear();
        selectedChoiceIndex = 0;
    }

    private void HandleChoiceInput()
    {
        if (Time.frameCount <= blockChoiceSubmitUntilFrame)
            return;

        if (!WasKeyboardSubmitPressed(nextSubmitAllowedTime))
            return;

        var selectedButton = choiceButtons[selectedChoiceIndex];
        if (selectedButton != null)
            manager.SelectChoice(selectedButton.VisibleChoiceIndex);
    }

    private void HandleChoiceClicked(int visibleChoiceIndex)
    {
        if (manager == null)
            return;

        if (Time.frameCount <= blockChoiceSubmitUntilFrame)
            return;

        if (isRevealInProgress)
        {
            CompleteRevealImmediately();
            return;
        }

        manager.SelectChoice(visibleChoiceIndex);
    }

    private void ApplyChoiceSelectionVisual()
    {
        for (var i = 0; i < choiceButtons.Count; i++)
        {
            var button = choiceButtons[i];
            if (button == null)
                continue;

            button.SetSelected(i == selectedChoiceIndex);
        }
    }

    private void ApplyVisible(bool value)
    {
        visible = value;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        gameObject.SetActive(visible);
    }

    private void CacheDialogueCanvas()
    {
        if (dialogueCanvas != null)
            return;

        dialogueCanvas = GetComponentInParent<Canvas>();
    }

    private void ApplyBlackoutSortingOverrideIfNeeded()
    {
        if (!renderAboveBlackout)
            return;

        CacheDialogueCanvas();
        if (dialogueCanvas == null)
            return;

        if (!hasStoredCanvasSortingState)
        {
            originalOverrideSorting = dialogueCanvas.overrideSorting;
            originalSortingOrder = dialogueCanvas.sortingOrder;
            originalSortingLayerId = dialogueCanvas.sortingLayerID;
            hasStoredCanvasSortingState = true;
        }

        dialogueCanvas.overrideSorting = true;

        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.TryGetOverlaySorting(out var overlaySortingLayerId, out var overlaySortingOrder))
        {
            dialogueCanvas.sortingLayerID = overlaySortingLayerId;
            dialogueCanvas.sortingOrder = overlaySortingOrder + 1;
            return;
        }

        dialogueCanvas.sortingOrder = Mathf.Max(dialogueCanvas.sortingOrder, BlackoutOverrideSortingOrder);
    }

    private void RestoreCanvasSortingOverride()
    {
        if (!hasStoredCanvasSortingState)
            return;

        CacheDialogueCanvas();
        if (dialogueCanvas == null)
            return;

        dialogueCanvas.overrideSorting = originalOverrideSorting;
        dialogueCanvas.sortingOrder = originalSortingOrder;
        dialogueCanvas.sortingLayerID = originalSortingLayerId;
        hasStoredCanvasSortingState = false;
    }

    private void StartReveal(DialogueNodeViewModel model)
    {
        StopCurrentReveal();

        if (lineText == null)
        {
            FinishReveal(model);
            return;
        }

        lineText.text = string.IsNullOrWhiteSpace(model.LineText) ? string.Empty : model.LineText;
        lineText.maxVisibleCharacters = int.MaxValue;
        SetLineTextAlpha(1f);
        lineText.ForceMeshUpdate();

        if (string.IsNullOrEmpty(lineText.text))
        {
            FinishReveal(model);
            return;
        }

        switch (revealMode)
        {
            case DialogueTextRevealMode.Instant:
                FinishReveal(model);
                break;

            case DialogueTextRevealMode.Fade:
                revealCoroutine = StartCoroutine(FadeRevealCoroutine(model));
                break;

            case DialogueTextRevealMode.Typewriter:
                revealCoroutine = StartCoroutine(TypewriterRevealCoroutine(model));
                break;
        }
    }

    private IEnumerator FadeRevealCoroutine(DialogueNodeViewModel model)
    {
        isRevealInProgress = true;
        lineText.maxVisibleCharacters = int.MaxValue;
        SetLineTextAlpha(0f);
        UpdatePromptText();

        if (fadeDuration <= 0f)
        {
            SetLineTextAlpha(1f);
            FinishReveal(model);
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetLineTextAlpha(Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        SetLineTextAlpha(1f);
        FinishReveal(model);
    }

    private IEnumerator TypewriterRevealCoroutine(DialogueNodeViewModel model)
    {
        isRevealInProgress = true;
        SetLineTextAlpha(1f);
        lineText.maxVisibleCharacters = 0;
        UpdatePromptText();

        var characterCount = lineText.textInfo.characterCount;
        var visibleCharacterCount = 0;
        var revealProgress = 0f;

        while (visibleCharacterCount < characterCount)
        {
            revealProgress += typewriterCharactersPerSecond * Time.unscaledDeltaTime;
            var targetVisibleCount = Mathf.Min(characterCount, Mathf.FloorToInt(revealProgress));

            while (visibleCharacterCount < targetVisibleCount)
            {
                var characterInfo = lineText.textInfo.characterInfo[visibleCharacterCount];
                visibleCharacterCount++;
                lineText.maxVisibleCharacters = visibleCharacterCount;

                if (!usePunctuationPauses || !characterInfo.isVisible)
                    continue;

                var pauseDuration = GetPunctuationPauseDuration(characterInfo.character);
                if (pauseDuration > 0f)
                    yield return WaitForSecondsRealtimeOrSkip(pauseDuration);
            }

            yield return null;
        }

        lineText.maxVisibleCharacters = int.MaxValue;
        FinishReveal(model);
    }

    private IEnumerator WaitForSecondsRealtimeOrSkip(float duration)
    {
        if (duration <= 0f)
            yield break;

        var remaining = duration;
        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CompleteRevealImmediately()
    {
        if (!isRevealInProgress)
            return;

        StopCurrentReveal();

        if (lineText != null)
        {
            lineText.maxVisibleCharacters = int.MaxValue;
            SetLineTextAlpha(1f);
        }

        FinishReveal(currentModel);
    }

    private void FinishReveal(DialogueNodeViewModel model)
    {
        StopCurrentReveal();
        isRevealInProgress = false;

        if (lineText != null)
        {
            lineText.maxVisibleCharacters = int.MaxValue;
            SetLineTextAlpha(1f);
        }

        BuildChoiceButtons(model != null ? model.Choices : null);

        if (choiceButtons.Count > 0)
            blockChoiceSubmitUntilFrame = Time.frameCount;

        canAdvance = model != null && model.CanAdvance;
        UpdatePromptText();
    }

    private void StopCurrentReveal()
    {
        if (revealCoroutine == null)
            return;

        StopCoroutine(revealCoroutine);
        revealCoroutine = null;
    }

    private void UpdatePromptText()
    {
        var hasPromptImage = promptImage != null;

        if (hasPromptImage)
            promptImage.enabled = true;

        if (promptText == null)
            return;

        if (isRevealInProgress)
        {
            var showCloseDuringReveal = currentModel != null && currentModel.WillCloseOnAdvance;
            promptText.text = showCloseDuringReveal ? closePrompt : skipPrompt;
            return;
        }

        if (choiceButtons.Count > 0)
        {
            promptText.text = choosePrompt;
            return;
        }

        var shouldShowClose = canAdvance && currentModel != null && currentModel.WillCloseOnAdvance;
        promptText.text = shouldShowClose ? closePrompt : continuePrompt;
    }

    private void AnimatePromptText()
    {
        if (!animatePromptText || promptFloatDistance <= 0f || promptFloatSpeed <= 0f)
        {
            ResetPromptAnimation();
            return;
        }

        var hasAnyPromptTarget = CachePromptBasePositions();
        if (!hasAnyPromptTarget)
            return;

        var offset = Vector2.up * (Mathf.Sin(Time.unscaledTime * promptFloatSpeed) * promptFloatDistance);

        if (promptImageRectTransform != null)
            promptImageRectTransform.anchoredPosition = promptImageBaseAnchoredPosition + offset;

        if (promptTextRectTransform != null)
            promptTextRectTransform.anchoredPosition = promptTextBaseAnchoredPosition + offset;
    }

    private bool CachePromptBasePositions()
    {
        var hasAnyPromptTarget = false;

        if (promptImageRectTransform == null && promptImage != null)
            promptImageRectTransform = promptImage.rectTransform;

        if (promptImageRectTransform != null)
        {
            if (!hasPromptImageBasePosition)
            {
                promptImageBaseAnchoredPosition = promptImageRectTransform.anchoredPosition;
                hasPromptImageBasePosition = true;
            }

            hasAnyPromptTarget = true;
        }

        if (promptTextRectTransform == null && promptText != null)
            promptTextRectTransform = promptText.rectTransform;

        if (promptTextRectTransform != null)
        {
            if (!hasPromptTextBasePosition)
            {
                promptTextBaseAnchoredPosition = promptTextRectTransform.anchoredPosition;
                hasPromptTextBasePosition = true;
            }

            hasAnyPromptTarget = true;
        }

        return hasAnyPromptTarget;
    }

    private void ResetPromptAnimation()
    {
        if (!CachePromptBasePositions())
            return;

        if (promptImageRectTransform != null)
            promptImageRectTransform.anchoredPosition = promptImageBaseAnchoredPosition;

        if (promptTextRectTransform != null)
            promptTextRectTransform.anchoredPosition = promptTextBaseAnchoredPosition;
    }

    private void SetLineTextAlpha(float alpha)
    {
        if (lineText == null)
            return;

        var color = lineText.color;
        color.a = Mathf.Clamp01(alpha);
        lineText.color = color;
    }

    private float GetPunctuationPauseDuration(char character)
    {
        return character switch
        {
            ',' or ';' or ':' => shortPunctuationPause,
            '.' or '!' or '?' => longPunctuationPause,
            _ => 0f
        };
    }

    private static bool WasSubmitPressed(float nextAllowedTime)
    {
        if (Time.unscaledTime < nextAllowedTime || Keyboard.current == null)
            return false;

        return Keyboard.current.eKey.wasPressedThisFrame;
    }

    private static bool WasKeyboardSubmitPressed(float nextAllowedTime)
    {
        if (Time.unscaledTime < nextAllowedTime || Keyboard.current == null)
            return false;

        return Keyboard.current.eKey.wasPressedThisFrame;
    }
}
