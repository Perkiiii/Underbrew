using UnityEngine;

public class DialogueInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueConversation conversation;
    [SerializeField] private string promptActionText = "Press E to talk to";
    [SerializeField] private string speakerDisplayName = "Character";
    [SerializeField] private bool oneTimeInSession;
    [SerializeField] private string consumedFlagKey;

    private bool hasBeenConsumedInSession;

    public string PromptText => $"{promptActionText} {speakerDisplayName}";

    public void Interact()
    {
        if (!CanStartDialogue())
            return;

        var manager = DialogueManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<DialogueManager>();

        if (manager == null)
        {
            Debug.LogWarning("[DialogueInteractable] DialogueManager not found in scene.");
            return;
        }

        if (!manager.StartConversation(conversation))
            return;

        if (oneTimeInSession)
            hasBeenConsumedInSession = true;

        if (!string.IsNullOrWhiteSpace(consumedFlagKey) && GameStateFlags.Instance != null)
            GameStateFlags.Instance.SetFlag(consumedFlagKey, true);
    }

    public void CancelInteract()
    {
    }

    private bool CanStartDialogue()
    {
        if (conversation == null)
            return false;

        if (oneTimeInSession && hasBeenConsumedInSession)
            return false;

        if (string.IsNullOrWhiteSpace(consumedFlagKey))
            return true;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return true;

        return !flags.GetFlag(consumedFlagKey);
    }
}
