using System.Collections;
using Underbrew.Core;
using Underbrew.World;
using UnityEngine;

public class SpawnIntroDialogue : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueConversation conversation;
    [SerializeField] private float delayBeforeStartSeconds = 0.1f;

    [Header("When To Trigger")]
    [SerializeField] private bool onlyWhenSpawnIdMatches = true;
    [SerializeField] private string requiredSpawnId = NewGameSpawnPoint.SpawnToken;
    [SerializeField] private bool requirePlayerNearTrigger = true;
    [SerializeField] private float playerNearDistance = 2f;

    [Header("One-Time")]
    [SerializeField] private string consumedFlagKey = "intro.cave.opening_shown";

    [Header("Retry")]
    [SerializeField] private float managerWaitTimeoutSeconds = 3f;
    [SerializeField] private float retryIntervalSeconds = 0.1f;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging;

    private bool hasTriggeredThisSession;

    private void Start()
    {
        StartCoroutine(TryStartIntroDialogueRoutine());
    }

    private IEnumerator TryStartIntroDialogueRoutine()
    {
        if (conversation == null)
        {
            if (verboseLogging)
                Debug.LogWarning("[SpawnIntroDialogue] Conversation is not assigned. Intro dialogue will not start.");

            yield break;
        }

        if (delayBeforeStartSeconds > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeStartSeconds);

        float elapsed = 0f;
        float retryInterval = Mathf.Max(0.01f, retryIntervalSeconds);
        float timeout = Mathf.Max(0.1f, managerWaitTimeoutSeconds);
        string lastBlockReason = string.Empty;

        while (elapsed < timeout)
        {
            if (ShouldStopTrying())
                yield break;

            if (!CanAttemptNow(out lastBlockReason))
            {
                yield return new WaitForSecondsRealtime(retryInterval);
                elapsed += retryInterval;
                continue;
            }

            if (TryStartConversation(out lastBlockReason))
                yield break;

            yield return new WaitForSecondsRealtime(retryInterval);
            elapsed += retryInterval;
        }

        if (verboseLogging)
            Debug.LogWarning($"[SpawnIntroDialogue] Timed out waiting to start '{conversation.name}'. Last block: {lastBlockReason}");
    }

    private bool TryStartConversation(out string blockReason)
    {
        blockReason = string.Empty;

        var manager = DialogueManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<DialogueManager>();

        if (manager == null)
        {
            blockReason = "DialogueManager not found.";
            return false;
        }

        if (manager.IsDialogueActive)
        {
            blockReason = "Another dialogue is already active.";
            return false;
        }

        if (!manager.StartConversation(conversation))
        {
            blockReason = "DialogueManager rejected conversation start (missing entry node or UI).";
            return false;
        }

        hasTriggeredThisSession = true;

        if (!string.IsNullOrWhiteSpace(consumedFlagKey) && GameStateFlags.Instance != null)
            GameStateFlags.Instance.SetFlag(consumedFlagKey, true);

        if (verboseLogging)
            Debug.Log($"[SpawnIntroDialogue] Started conversation '{conversation.name}'.");

        return true;
    }

    private bool ShouldStopTrying()
    {
        if (hasTriggeredThisSession)
            return true;

        if (!string.IsNullOrWhiteSpace(consumedFlagKey) && GameStateFlags.Instance != null)
        {
            if (GameStateFlags.Instance.GetFlag(consumedFlagKey))
            {
                if (verboseLogging)
                    Debug.Log($"[SpawnIntroDialogue] Skipped because consumed flag '{consumedFlagKey}' is already true.");

                return true;
            }
        }

        return false;
    }

    private bool CanAttemptNow(out string blockReason)
    {
        blockReason = string.Empty;

        if (onlyWhenSpawnIdMatches)
        {
            var transitionManager = SceneTransitionManager.Instance;
            if (transitionManager == null)
            {
                blockReason = "SceneTransitionManager not available yet.";
                return false;
            }

            if (!string.Equals(transitionManager.TargetSpawnID, requiredSpawnId, System.StringComparison.Ordinal))
            {
                blockReason = $"Spawn mismatch. Required='{requiredSpawnId}', Current='{transitionManager.TargetSpawnID}'.";
                return false;
            }
        }

        if (requirePlayerNearTrigger)
        {
            var player = FindFirstObjectByType<Player>();
            if (player == null)
            {
                blockReason = "Player not found yet.";
                return false;
            }

            float maxDistance = Mathf.Max(0.1f, playerNearDistance);
            if ((player.transform.position - transform.position).sqrMagnitude > maxDistance * maxDistance)
            {
                blockReason = $"Player is farther than {maxDistance:0.##} units from intro trigger object.";
                return false;
            }
        }

        blockReason = "Ready";
        return true;
    }
}
