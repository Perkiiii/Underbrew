using System.Collections;
using Underbrew.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underbrew.World
{
    [RequireComponent(typeof(Collider2D))]
    public class EndingSequenceTrigger : MonoBehaviour
    {
        [Header("Requirements")]
        [SerializeField] private string requiredFlagKey = "quest.cavern.enter";
        [SerializeField] private bool requiredFlagValue = true;
        [SerializeField] private DialogueConversation blockedConversation;

        [Header("Sequence")]
        [SerializeField] private DialogueConversation endingConversation;
        [SerializeField] private float preDialogueBlackHoldSeconds = 0.2f;
        [SerializeField] private float postDialoguePauseSeconds = 0.75f;
        [SerializeField] private string menuSceneName = "UG_MENU_MAIN";

        [Header("One Shot")]
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private string consumedFlagKey = "ending.cavern.completed";

        private bool isRunning;
        private bool isBlockedDialogueActive;

        private void Reset()
        {
            var collider2D = GetComponent<Collider2D>();
            if (collider2D != null)
                collider2D.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (isRunning)
                return;

            if (collision.GetComponentInParent<Player>() == null)
                return;

            if (!CanTrigger())
            {
                TryStartBlockedConversation();
                return;
            }

            StartCoroutine(RunEndingSequence());
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<Player>() == null)
                return;

            isBlockedDialogueActive = false;
        }

        private bool CanTrigger()
        {
            var flags = GameStateFlags.Instance;

            if (!string.IsNullOrWhiteSpace(requiredFlagKey))
            {
                if (flags == null)
                    return false;

                if (flags.GetFlag(requiredFlagKey) != requiredFlagValue)
                    return false;
            }

            if (!triggerOnce || string.IsNullOrWhiteSpace(consumedFlagKey))
                return true;

            if (flags == null)
                return true;

            return !flags.GetFlag(consumedFlagKey);
        }

        private void TryStartBlockedConversation()
        {
            if (blockedConversation == null || isBlockedDialogueActive)
                return;

            var dialogueManager = DialogueManager.Instance;
            if (dialogueManager == null)
                dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager == null || dialogueManager.IsDialogueActive)
                return;

            if (!dialogueManager.StartConversation(blockedConversation))
                return;

            isBlockedDialogueActive = true;
        }

        private IEnumerator RunEndingSequence()
        {
            isRunning = true;

            if (triggerOnce && !string.IsNullOrWhiteSpace(consumedFlagKey) && GameStateFlags.Instance != null)
                GameStateFlags.Instance.SetFlag(consumedFlagKey, true);

            var player = FindFirstObjectByType<Player>();
            if (player != null && player.input != null)
                player.input.Player.Disable();

            var transitionManager = SceneTransitionManager.Instance;
            if (transitionManager != null)
                yield return transitionManager.FadeOverlayToBlack(false);
            else
                yield return null;

            if (preDialogueBlackHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(preDialogueBlackHoldSeconds);

            var dialogueUi = DialogueUI.Instance;
            var enabledBlackoutDialogueOverride = false;
            SetEndingUiSuppressed(true);

            if (endingConversation != null)
            {
                var dialogueManager = DialogueManager.Instance;
                if (dialogueManager == null)
                    dialogueManager = FindFirstObjectByType<DialogueManager>();

                if (dialogueManager != null && !dialogueManager.IsDialogueActive)
                {
                    if (dialogueUi != null)
                    {
                        dialogueUi.SetRenderAboveBlackout(true);
                        enabledBlackoutDialogueOverride = true;
                    }

                    dialogueManager.StartConversation(endingConversation);

                    while (dialogueManager != null && dialogueManager.IsDialogueActive)
                        yield return null;
                }
                else
                {
                    Debug.LogWarning("[EndingSequenceTrigger] DialogueManager unavailable or already busy. Ending conversation step was skipped.");
                }
            }

            if (enabledBlackoutDialogueOverride && dialogueUi != null)
                dialogueUi.SetRenderAboveBlackout(false);

            SetEndingUiSuppressed(false);

            if (postDialoguePauseSeconds > 0f)
                yield return new WaitForSecondsRealtime(postDialoguePauseSeconds);

            if (transitionManager != null)
            {
                var started = transitionManager.RequestTransition(menuSceneName, string.Empty, Direction.Down);
                if (started)
                    yield break;
            }

            SceneManager.LoadScene(menuSceneName);
        }

        private static void SetEndingUiSuppressed(bool value)
        {
            BackpackUI.SetEndingUiSuppressed(value);
            JournalUI.SetEndingUiSuppressed(value);
            BackpackButton.SetEndingUiSuppressed(value);
            JournalButton.SetEndingUiSuppressed(value);
        }
    }
}
