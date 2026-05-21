using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestJournalCatalog", menuName = "Underbrew/Journal/Quest Catalog")]
public class QuestJournalCatalogAsset : ScriptableObject
{
    [SerializeField] private List<QuestJournalEntry> quests = new();

    public IReadOnlyList<QuestJournalEntry> Quests => quests;
}

[Serializable]
public class QuestJournalEntry
{
    [SerializeField] private string questId;
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 6)] private string description;
    [SerializeField] private string visibilityFlagKey;
    [SerializeField] private string completionFlagKey;
    [SerializeField] private string completionSummary = "Completed.";
    [SerializeField] private QuestJournalStep[] steps;

    public string QuestId => questId;
    public string Title => title;
    public string Description => description;
    public string VisibilityFlagKey => visibilityFlagKey;
    public string CompletionFlagKey => completionFlagKey;
    public string CompletionSummary => completionSummary;
    public QuestJournalStep[] Steps => steps;
}

[Serializable]
public class QuestJournalStep
{
    [SerializeField, TextArea(1, 3)] private string stepText;
    [SerializeField] private string completionFlagKey;

    public string StepText => stepText;
    public string CompletionFlagKey => completionFlagKey;
}
