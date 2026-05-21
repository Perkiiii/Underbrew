using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalQuestsPageUI : JournalTabPageUI
{
    [SerializeField] private QuestJournalCatalogAsset questCatalogAsset;

    [Header("Left Panel")]
    [SerializeField] private Transform questListContainer;
    [SerializeField] private JournalQuestListEntryUI questListEntryPrefab;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private string emptyQuestMessage = "No quests yet.";

    [Header("Right Panel")]
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Toggle currentStepStatusToggle;
    [SerializeField] private TMP_Text currentStepText;
    [SerializeField] private Toggle additionalStepStatusToggle1;
    [SerializeField] private TMP_Text additionalStepText1;
    [SerializeField] private Toggle additionalStepStatusToggle2;
    [SerializeField] private TMP_Text additionalStepText2;
    [SerializeField] private string noCurrentStepMessage = "No current objective.";

    private readonly List<JournalQuestListEntryUI> spawnedEntries = new();
    private GameStateFlags flags;
    private QuestJournalEntry selectedQuest;
    private bool isSubscribed;

    protected override void OnShown()
    {
        EnsureFlags();
        SubscribeToFlags();
    }

    protected override void OnHidden()
    {
        UnsubscribeFromFlags();
    }

    protected override void OnRefreshPage()
    {
        EnsureFlags();
        BuildQuestList();
        UpdateDetailPanel();
    }

    private void OnDisable()
    {
        UnsubscribeFromFlags();
    }

    private void EnsureFlags()
    {
        if (flags != null)
            return;

        flags = GameStateFlags.Instance ?? FindFirstObjectByType<GameStateFlags>(FindObjectsInactive.Include);
    }

    private void SubscribeToFlags()
    {
        if (isSubscribed || flags == null)
            return;

        flags.OnFlagChanged += HandleFlagChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromFlags()
    {
        if (!isSubscribed)
            return;

        if (flags != null)
            flags.OnFlagChanged -= HandleFlagChanged;

        isSubscribed = false;
    }

    private void HandleFlagChanged(string _, bool __)
    {
        if (!isActiveAndEnabled)
            return;

        RefreshPage();
    }

    private void BuildQuestList()
    {
        ClearSpawnedEntries();

        var visibleQuests = GetVisibleQuests();
        if (questListContainer == null || questListEntryPrefab == null || visibleQuests.Count == 0)
        {
            SetEmptyState(emptyQuestMessage);
            return;
        }

        if (selectedQuest == null || !visibleQuests.Contains(selectedQuest))
            selectedQuest = visibleQuests[0];

        for (var i = 0; i < visibleQuests.Count; i++)
        {
            var quest = visibleQuests[i];
            var entry = Instantiate(questListEntryPrefab, questListContainer);
            entry.Configure(quest, IsQuestCompleted(quest), quest == selectedQuest, HandleQuestSelected);
            spawnedEntries.Add(entry);
        }

        SetEmptyState(string.Empty);
    }

    private void UpdateDetailPanel()
    {
        if (selectedQuest == null)
        {
            if (detailTitleText != null)
                detailTitleText.text = string.Empty;

            if (detailDescriptionText != null)
                detailDescriptionText.text = string.Empty;

            if (currentStepText != null)
                currentStepText.text = string.Empty;

            SetStepRowVisible(currentStepStatusToggle, currentStepText, false);
            SetStepRowVisible(additionalStepStatusToggle1, additionalStepText1, false);
            SetStepRowVisible(additionalStepStatusToggle2, additionalStepText2, false);

            return;
        }

        if (detailTitleText != null)
            detailTitleText.text = selectedQuest.Title;

        if (detailDescriptionText != null)
            detailDescriptionText.text = selectedQuest.Description;

        UpdateStepRows(selectedQuest);
    }

    private void HandleQuestSelected(JournalQuestListEntryUI entry)
    {
        if (entry == null || entry.BoundQuest == null)
            return;

        selectedQuest = entry.BoundQuest;
        RefreshSelectionStates();
        UpdateDetailPanel();
    }

    private void RefreshSelectionStates()
    {
        for (var i = 0; i < spawnedEntries.Count; i++)
        {
            var entry = spawnedEntries[i];
            if (entry == null)
                continue;

            entry.SetSelected(entry.BoundQuest == selectedQuest);
        }
    }

    private List<QuestJournalEntry> GetVisibleQuests()
    {
        var visibleQuests = new List<QuestJournalEntry>();
        if (questCatalogAsset == null)
            return visibleQuests;

        var source = questCatalogAsset.Quests;
        for (var i = 0; i < source.Count; i++)
        {
            var quest = source[i];
            if (quest == null || !IsQuestVisible(quest))
                continue;

            visibleQuests.Add(quest);
        }

        return visibleQuests;
    }

    private bool IsQuestVisible(QuestJournalEntry quest)
    {
        if (quest == null)
            return false;

        if (string.IsNullOrWhiteSpace(quest.VisibilityFlagKey))
            return true;

        EnsureFlags();
        return flags != null && flags.GetFlag(quest.VisibilityFlagKey);
    }

    private bool IsQuestCompleted(QuestJournalEntry quest)
    {
        if (quest == null || string.IsNullOrWhiteSpace(quest.CompletionFlagKey))
            return false;

        EnsureFlags();
        return flags != null && flags.GetFlag(quest.CompletionFlagKey);
    }

    private string BuildCurrentStepText(QuestJournalEntry quest)
    {
        if (quest == null)
            return string.Empty;

        if (IsQuestCompleted(quest))
        {
            return string.IsNullOrWhiteSpace(quest.CompletionSummary)
                ? noCurrentStepMessage
                : quest.CompletionSummary;
        }

        var steps = quest.Steps;
        if (steps == null || steps.Length == 0)
            return noCurrentStepMessage;

        EnsureFlags();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (step == null || string.IsNullOrWhiteSpace(step.StepText))
                continue;

            var isComplete = flags != null
                && !string.IsNullOrWhiteSpace(step.CompletionFlagKey)
                && flags.GetFlag(step.CompletionFlagKey);

            if (!isComplete)
                return step.StepText;
        }

        return string.IsNullOrWhiteSpace(quest.CompletionSummary)
            ? noCurrentStepMessage
            : quest.CompletionSummary;
    }

    private void ClearSpawnedEntries()
    {
        for (var i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i] != null)
                Destroy(spawnedEntries[i].gameObject);
        }

        spawnedEntries.Clear();
    }

    private void SetEmptyState(string message)
    {
        if (emptyStateText != null)
            emptyStateText.text = message;
    }

    private void UpdateStepRows(QuestJournalEntry quest)
    {
        var steps = quest != null ? quest.Steps : null;
        var hasSteps = steps != null && steps.Length > 0;

        if (!hasSteps)
        {
            SetStepRow(currentStepStatusToggle, currentStepText, noCurrentStepMessage, false, true);
            SetStepRowVisible(additionalStepStatusToggle1, additionalStepText1, false);
            SetStepRowVisible(additionalStepStatusToggle2, additionalStepText2, false);
            return;
        }

        SetStepRowFromQuestStep(currentStepStatusToggle, currentStepText, steps, 0);
        SetStepRowFromQuestStep(additionalStepStatusToggle1, additionalStepText1, steps, 1);
        SetStepRowFromQuestStep(additionalStepStatusToggle2, additionalStepText2, steps, 2);
    }

    private void SetStepRowFromQuestStep(Toggle statusToggle, TMP_Text stepText, QuestJournalStep[] steps, int index)
    {
        if (steps == null || index < 0 || index >= steps.Length)
        {
            SetStepRowVisible(statusToggle, stepText, false);
            return;
        }

        var step = steps[index];
        if (step == null || string.IsNullOrWhiteSpace(step.StepText))
        {
            SetStepRowVisible(statusToggle, stepText, false);
            return;
        }

        var isComplete = flags != null
            && !string.IsNullOrWhiteSpace(step.CompletionFlagKey)
            && flags.GetFlag(step.CompletionFlagKey);

        SetStepRow(statusToggle, stepText, step.StepText, isComplete, true);
    }

    private static void SetStepRow(Toggle statusToggle, TMP_Text stepText, string text, bool isComplete, bool visible)
    {
        if (stepText != null)
            stepText.text = text;

        if (statusToggle != null)
        {
            statusToggle.SetIsOnWithoutNotify(isComplete);
            statusToggle.interactable = false;
        }

        SetStepRowVisible(statusToggle, stepText, visible);
    }

    private static void SetStepRowVisible(Toggle statusToggle, TMP_Text stepText, bool visible)
    {
        if (statusToggle != null)
            statusToggle.gameObject.SetActive(visible);

        if (stepText != null)
            stepText.gameObject.SetActive(visible);
    }
}
