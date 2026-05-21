using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueConversation", menuName = "Underbrew/Dialogue/Conversation")]
public class DialogueConversation : ScriptableObject
{
    [SerializeField] private string conversationId;
    [SerializeField] private string entryNodeId;
    [SerializeField] private DialogueNode[] nodes;

    public string ConversationId => conversationId;
    public string EntryNodeId => entryNodeId;
    public DialogueNode[] Nodes => nodes;

    public bool TryGetEntryNode(out DialogueNode node)
    {
        if (nodes == null || nodes.Length == 0)
        {
            node = null;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entryNodeId) && TryGetNode(entryNodeId, out node))
            return true;

        node = nodes[0];
        return node != null;
    }

    public bool TryGetNode(string nodeId, out DialogueNode node)
    {
        node = null;

        if (string.IsNullOrWhiteSpace(nodeId) || nodes == null)
            return false;

        for (var i = 0; i < nodes.Length; i++)
        {
            var candidate = nodes[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal))
            {
                node = candidate;
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public class DialogueNode
{
    [SerializeField] private string nodeId;
    [SerializeField] private string speakerName;
    [SerializeField] private DialogueLine[] lines;
    [SerializeField] private bool useEventStyle;
    [SerializeField] private string nextNodeId;
    [SerializeField] private DialogueChoice[] choices;
    [SerializeField] private DialogueOutcome[] onShownOutcomes;

    public string NodeId => nodeId;
    public string SpeakerName => speakerName;
    public int LineCount => lines?.Length ?? 0;
    public bool UseEventStyle => useEventStyle;
    public string NextNodeId => nextNodeId;
    public DialogueChoice[] Choices => choices;
    public DialogueOutcome[] OnShownOutcomes => onShownOutcomes;

    public bool TryGetLineText(int lineIndex, out string text)
    {
        text = string.Empty;

        if (lineIndex < 0 || lines == null || lineIndex >= lines.Length)
            return false;

        var line = lines[lineIndex];
        if (line == null)
            return false;

        text = line.Text ?? string.Empty;
        return true;
    }
}

[Serializable]
public class DialogueLine
{
    [SerializeField, TextArea(2, 5)] private string text;

    public string Text => text;
}

[Serializable]
public class DialogueChoice
{
    [SerializeField] private string choiceText;
    [SerializeField] private string nextNodeId;
    [SerializeField] private string requiredFlag;
    [SerializeField] private bool requiredFlagValue = true;
    [SerializeField] private bool hideWhenConditionFails = true;
    [SerializeField] private DialogueOutcome[] outcomes;

    public string ChoiceText => choiceText;
    public string NextNodeId => nextNodeId;
    public string RequiredFlag => requiredFlag;
    public bool RequiredFlagValue => requiredFlagValue;
    public bool HideWhenConditionFails => hideWhenConditionFails;
    public DialogueOutcome[] Outcomes => outcomes;
}

public enum DialogueOutcomeType
{
    SetFlag,
    AddItem,
    RemoveItem
}

[Serializable]
public class DialogueOutcome
{
    [SerializeField] private DialogueOutcomeType outcomeType;
    [SerializeField] private string flagKey;
    [SerializeField] private bool flagValue = true;
    [SerializeField] private ItemData item;
    [SerializeField, Min(1)] private int quantity = 1;

    public DialogueOutcomeType OutcomeType => outcomeType;
    public string FlagKey => flagKey;
    public bool FlagValue => flagValue;
    public ItemData Item => item;
    public int Quantity => quantity;
}
