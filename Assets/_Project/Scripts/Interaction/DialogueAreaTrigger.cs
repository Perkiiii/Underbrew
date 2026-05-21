using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogueAreaTrigger : MonoBehaviour
{
    [SerializeField] private DialogueConversation conversation;
    [SerializeField] private bool triggerOnceInSession = true;
    [SerializeField] private string consumedFlagKey;

    private bool hasTriggeredInSession;

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!CanTrigger())
            return;

        if (collision.GetComponentInParent<Player>() == null)
            return;

        var manager = DialogueManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<DialogueManager>();

        if (manager == null)
        {
            Debug.LogWarning("[DialogueAreaTrigger] DialogueManager not found in scene.");
            return;
        }

        if (!manager.StartConversation(conversation))
            return;

        hasTriggeredInSession = true;

        if (!string.IsNullOrWhiteSpace(consumedFlagKey) && GameStateFlags.Instance != null)
            GameStateFlags.Instance.SetFlag(consumedFlagKey, true);
    }

    public void ResetTriggerState()
    {
        hasTriggeredInSession = false;

        if (!string.IsNullOrWhiteSpace(consumedFlagKey) && GameStateFlags.Instance != null)
            GameStateFlags.Instance.ClearFlag(consumedFlagKey);
    }

    private bool CanTrigger()
    {
        if (conversation == null)
            return false;

        if (triggerOnceInSession && hasTriggeredInSession)
            return false;

        if (string.IsNullOrWhiteSpace(consumedFlagKey))
            return true;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return true;

        return !flags.GetFlag(consumedFlagKey);
    }
}
