using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlagTrigger2D : MonoBehaviour
{
    [SerializeField] private string flagKey;
    [SerializeField] private bool flagValue = true;
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

        var flags = GameStateFlags.Instance;
        if (flags == null || string.IsNullOrWhiteSpace(flagKey))
            return;

        flags.SetFlag(flagKey, flagValue);
        hasTriggeredInSession = true;

        if (!string.IsNullOrWhiteSpace(consumedFlagKey))
            flags.SetFlag(consumedFlagKey, true);
    }

    private bool CanTrigger()
    {
        if (string.IsNullOrWhiteSpace(flagKey))
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
