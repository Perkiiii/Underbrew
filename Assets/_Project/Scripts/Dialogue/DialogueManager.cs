using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    private const int MaxAutoAdvanceHopsPerRender = 64;

    public static DialogueManager Instance { get; private set; }

    [SerializeField] private DialogueUI dialogueUI;

    private readonly HashSet<string> appliedNodeOutcomeKeys = new();
    private readonly List<DialogueChoiceRuntime> visibleChoices = new();

    private DialogueConversation activeConversation;
    private DialogueNode currentNode;
    private int currentLineIndexInNode;

    public bool IsDialogueActive => activeConversation != null;

    public event Action<DialogueConversation> OnDialogueStarted;
    public event Action<DialogueConversation> OnDialogueEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // If this manager lives under a persistent root, the parent handles persistence.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (IsDialogueActive)
            ForceClose();

        Instance = null;
    }

    public bool StartConversation(DialogueConversation conversation)
    {
        if (conversation == null)
            return false;

        if (IsDialogueActive)
            return false;

        if (!conversation.TryGetEntryNode(out currentNode) || currentNode == null)
        {
            Debug.LogWarning($"[DialogueManager] Could not start conversation '{conversation.name}'. Entry node is missing.");
            return false;
        }

        activeConversation = conversation;
        appliedNodeOutcomeKeys.Clear();

        BackpackUI.AcquireModalLock();

        if (EnsureDialogueUI() == false)
        {
            BackpackUI.ReleaseModalLock();
            activeConversation = null;
            currentNode = null;
            return false;
        }

        dialogueUI.Open(this);
        EnterCurrentNode();
        OnDialogueStarted?.Invoke(activeConversation);
        return true;
    }

    public void AdvanceLine()
    {
        if (!IsDialogueActive || currentNode == null)
            return;

        if (visibleChoices.Count > 0)
            return;

        if (TryAdvanceLineInCurrentNode())
        {
            RenderCurrentNode();
            return;
        }

        if (MoveToNode(currentNode.NextNodeId))
            return;

        EndConversation();
    }

    public void SelectChoice(int visibleChoiceIndex)
    {
        if (!IsDialogueActive)
            return;

        if (visibleChoiceIndex < 0 || visibleChoiceIndex >= visibleChoices.Count)
            return;

        var choice = visibleChoices[visibleChoiceIndex].Choice;
        ApplyOutcomes(choice.Outcomes);

        if (MoveToNode(choice.NextNodeId))
            return;

        EndConversation();
    }

    public void EndConversation()
    {
        if (!IsDialogueActive)
            return;

        var endedConversation = activeConversation;

        activeConversation = null;
        currentNode = null;
        currentLineIndexInNode = 0;
        visibleChoices.Clear();

        if (dialogueUI != null)
            dialogueUI.Close();

        BackpackUI.ReleaseModalLock();
        OnDialogueEnded?.Invoke(endedConversation);
    }

    public void ForceClose()
    {
        if (!IsDialogueActive)
            return;

        EndConversation();
    }

    private bool EnsureDialogueUI()
    {
        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);

        if (dialogueUI == null)
            Debug.LogWarning("[DialogueManager] DialogueUI not found in scene. Add DialogueUI to a persistent canvas.");

        return dialogueUI != null;
    }

    private void EnterCurrentNode()
    {
        if (currentNode == null)
            return;

        // Node entry happens before line 0 is rendered.
        ApplyNodeOutcomesOnce(currentNode);
        currentLineIndexInNode = 0;
        RenderCurrentNode();
    }

    private void RenderCurrentNode()
    {
        if (dialogueUI == null)
            return;

        if (!IsDialogueActive || currentNode == null)
            return;

        for (var hop = 0; hop < MaxAutoAdvanceHopsPerRender; hop++)
        {
            if (!IsDialogueActive || currentNode == null)
                return;

            if (TryBuildNodeViewModel(currentNode, out var model, out var shouldAutoAdvance, out var autoAdvanceWarning))
            {
                dialogueUI.ShowNode(model);
                return;
            }

            if (!string.IsNullOrEmpty(autoAdvanceWarning))
                Debug.LogWarning(autoAdvanceWarning);

            if (!shouldAutoAdvance)
            {
                EndConversation();
                return;
            }

            if (!MoveToNodeWithoutRender(currentNode.NextNodeId))
            {
                EndConversation();
                return;
            }
        }

        Debug.LogWarning($"[DialogueManager] Auto-advance safety limit reached in conversation '{activeConversation.name}'. Check for pass-through loops.");
        EndConversation();
    }

    private bool TryBuildNodeViewModel(DialogueNode node, out DialogueNodeViewModel model, out bool shouldAutoAdvance, out string autoAdvanceWarning)
    {
        model = null;
        shouldAutoAdvance = false;
        autoAdvanceWarning = string.Empty;

        if (node == null)
        {
            autoAdvanceWarning = "[DialogueManager] Current node is null while rendering. Ending dialogue.";
            return false;
        }

        var lineCount = Mathf.Max(0, node.LineCount);

        if (lineCount > 0 && currentLineIndexInNode >= lineCount)
            currentLineIndexInNode = lineCount - 1;

        if (lineCount > 0 && currentLineIndexInNode < 0)
            currentLineIndexInNode = 0;

        var showChoicesNow = ShouldShowChoices(node, lineCount);

        visibleChoices.Clear();
        if (showChoicesNow)
            BuildVisibleChoices(node, visibleChoices);

        if (lineCount == 0)
        {
            if (visibleChoices.Count > 0)
            {
                model = CreateModel(node, string.Empty, canAdvance: false);
                return true;
            }

            if (HasConfiguredChoices(node))
            {
                shouldAutoAdvance = !string.IsNullOrWhiteSpace(node.NextNodeId);
                autoAdvanceWarning = shouldAutoAdvance
                    ? $"[DialogueManager] Node '{node.NodeId}' has only hidden/unmet choices. Falling back to next node '{node.NextNodeId}'."
                    : $"[DialogueManager] Node '{node.NodeId}' has only hidden/unmet choices and no nextNodeId. Ending conversation.";
                return false;
            }

            shouldAutoAdvance = !string.IsNullOrWhiteSpace(node.NextNodeId);
            autoAdvanceWarning = shouldAutoAdvance
                ? $"[DialogueManager] Node '{node.NodeId}' has no lines or choices. Auto-advancing to '{node.NextNodeId}'."
                : $"[DialogueManager] Node '{node.NodeId}' has no lines, no choices, and no nextNodeId. Ending conversation.";
            return false;
        }

        if (!node.TryGetLineText(currentLineIndexInNode, out var currentLineText))
        {
            autoAdvanceWarning = $"[DialogueManager] Node '{node.NodeId}' line index {currentLineIndexInNode} is invalid. Ending conversation.";
            return false;
        }

        var canAdvance = visibleChoices.Count == 0;
        var willCloseOnAdvance = canAdvance && WillCloseOnAdvance(node, lineCount);
        model = CreateModel(node, currentLineText, canAdvance, willCloseOnAdvance);
        return true;
    }

    private DialogueNodeViewModel CreateModel(DialogueNode node, string currentLineText, bool canAdvance, bool willCloseOnAdvance = false)
    {
        var model = new DialogueNodeViewModel
        {
            SpeakerName = node.SpeakerName,
            LineText = currentLineText,
            UseEventStyle = node.UseEventStyle,
            CanAdvance = canAdvance,
            WillCloseOnAdvance = willCloseOnAdvance,
            Choices = new List<DialogueChoiceViewModel>(visibleChoices.Count)
        };

        for (var i = 0; i < visibleChoices.Count; i++)
        {
            var runtimeChoice = visibleChoices[i];
            model.Choices.Add(new DialogueChoiceViewModel
            {
                ChoiceText = runtimeChoice.Choice.ChoiceText,
                VisibleChoiceIndex = i
            });
        }

        return model;
    }

    private bool WillCloseOnAdvance(DialogueNode node, int lineCount)
    {
        if (node == null)
            return true;

        var hasAnotherLineInNode = lineCount > 0 && currentLineIndexInNode < lineCount - 1;
        if (hasAnotherLineInNode)
            return false;

        if (string.IsNullOrWhiteSpace(node.NextNodeId) || activeConversation == null)
            return true;

        return !activeConversation.TryGetNode(node.NextNodeId, out var nextNode) || nextNode == null;
    }

    private bool ShouldShowChoices(DialogueNode node, int lineCount)
    {
        if (!HasConfiguredChoices(node))
            return false;

        if (lineCount <= 0)
            return true;

        return currentLineIndexInNode >= lineCount - 1;
    }

    private static bool HasConfiguredChoices(DialogueNode node)
    {
        return node != null && node.Choices != null && node.Choices.Length > 0;
    }

    private bool TryAdvanceLineInCurrentNode()
    {
        if (currentNode == null)
            return false;

        var lineCount = currentNode.LineCount;
        if (lineCount <= 0)
            return false;

        if (currentLineIndexInNode >= lineCount - 1)
            return false;

        currentLineIndexInNode++;
        return true;
    }

    private void BuildVisibleChoices(DialogueNode node, List<DialogueChoiceRuntime> results)
    {
        if (node == null || node.Choices == null)
            return;

        for (var i = 0; i < node.Choices.Length; i++)
        {
            var choice = node.Choices[i];
            if (choice == null)
                continue;

            var meetsCondition = MeetsChoiceCondition(choice);

            if (!meetsCondition && choice.HideWhenConditionFails)
                continue;

            results.Add(new DialogueChoiceRuntime(choice));
        }
    }

    private bool MeetsChoiceCondition(DialogueChoice choice)
    {
        if (choice == null)
            return false;

        if (string.IsNullOrWhiteSpace(choice.RequiredFlag))
            return true;

        var stateFlags = GameStateFlags.Instance;
        if (stateFlags == null)
            return false;

        var currentValue = stateFlags.GetFlag(choice.RequiredFlag);
        return currentValue == choice.RequiredFlagValue;
    }

    private void ApplyNodeOutcomesOnce(DialogueNode node)
    {
        if (node == null || node.OnShownOutcomes == null || node.OnShownOutcomes.Length == 0)
            return;

        var conversationId = string.IsNullOrWhiteSpace(activeConversation.ConversationId)
            ? activeConversation.name
            : activeConversation.ConversationId;

        var nodeId = string.IsNullOrWhiteSpace(node.NodeId) ? "<unnamed>" : node.NodeId;
        var key = conversationId + ":" + nodeId;

        if (appliedNodeOutcomeKeys.Contains(key))
            return;

        ApplyOutcomes(node.OnShownOutcomes);
        appliedNodeOutcomeKeys.Add(key);
    }

    private void ApplyOutcomes(DialogueOutcome[] outcomes)
    {
        if (outcomes == null || outcomes.Length == 0)
            return;

        var stateFlags = GameStateFlags.Instance;
        var inventory = FindFirstObjectByType<InventorySystem>();

        for (var i = 0; i < outcomes.Length; i++)
        {
            var outcome = outcomes[i];
            if (outcome == null)
                continue;

            switch (outcome.OutcomeType)
            {
                case DialogueOutcomeType.SetFlag:
                    if (stateFlags == null)
                    {
                        Debug.LogWarning("[DialogueManager] GameStateFlags not found. SetFlag outcome skipped.");
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(outcome.FlagKey))
                        stateFlags.SetFlag(outcome.FlagKey, outcome.FlagValue);
                    break;

                case DialogueOutcomeType.AddItem:
                    if (inventory == null)
                    {
                        Debug.LogWarning("[DialogueManager] InventorySystem not found. AddItem outcome skipped.");
                        break;
                    }

                    inventory.Add(outcome.Item, outcome.Quantity);
                    break;

                case DialogueOutcomeType.RemoveItem:
                    if (inventory == null)
                    {
                        Debug.LogWarning("[DialogueManager] InventorySystem not found. RemoveItem outcome skipped.");
                        break;
                    }

                    inventory.Remove(outcome.Item, outcome.Quantity);
                    break;
            }
        }
    }

    private bool MoveToNode(string nextNodeId)
    {
        if (!MoveToNodeWithoutRender(nextNodeId))
            return false;

        EnterCurrentNode();
        return true;
    }

    private bool MoveToNodeWithoutRender(string nextNodeId)
    {
        if (string.IsNullOrWhiteSpace(nextNodeId) || activeConversation == null)
            return false;

        if (!activeConversation.TryGetNode(nextNodeId, out currentNode) || currentNode == null)
        {
            Debug.LogWarning($"[DialogueManager] Node '{nextNodeId}' was not found in conversation '{activeConversation.name}'.");
            return false;
        }

        currentLineIndexInNode = 0;
        return true;
    }

    private void HandleSceneChanged(Scene oldScene, Scene newScene)
    {
        if (IsDialogueActive)
            ForceClose();
    }

    private readonly struct DialogueChoiceRuntime
    {
        public DialogueChoiceRuntime(DialogueChoice choice)
        {
            Choice = choice;
        }

        public DialogueChoice Choice { get; }
    }
}
