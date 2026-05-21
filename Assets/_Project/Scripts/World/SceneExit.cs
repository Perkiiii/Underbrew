using UnityEngine;
using Underbrew.Core;

namespace Underbrew.World
{
    public class SceneExit : MonoBehaviour
    {
        [Header("Transition")]
        public string targetScene;
        public string targetSpawnID;
        public Direction exitDirection;

        [Header("Gate Lock")]
        [SerializeField] private bool requireUnlockFlag;
        [SerializeField] private string requiredUnlockFlagKey = "";
        [SerializeField] private DialogueConversation blockedConversation;
        [SerializeField] private ItemData requiredItem;
        [SerializeField] private int requiredItemQuantity = 1;
        [SerializeField] private string setFlagOnSuccessfulEntry;
        [SerializeField] private bool setFlagValueOnSuccessfulEntry = true;

        private bool isTriggered;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (isTriggered || !collision.CompareTag("Player"))
            {
                return;
            }

            if (!IsExitUnlocked())
            {
                TryStartBlockedDialogue();
                return;
            }

            if (SceneTransitionManager.Instance != null)
            {
                isTriggered = SceneTransitionManager.Instance.RequestTransition(targetScene, targetSpawnID, exitDirection);
                if (isTriggered)
                    ApplySuccessfulEntryEffects();
            }
            else
            {
                Debug.LogWarning("[SceneExit] SceneTransitionManager.Instance is null. Transition request was not sent.");
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                isTriggered = false;
            }
        }

        private bool IsExitUnlocked()
        {
            if (requireUnlockFlag && !string.IsNullOrWhiteSpace(requiredUnlockFlagKey))
            {
                var flags = GameStateFlags.Instance;
                if (flags == null)
                    return false;

                if (!flags.GetFlag(requiredUnlockFlagKey))
                    return false;
            }

            return HasRequiredItem();
        }

        private void TryStartBlockedDialogue()
        {
            if (blockedConversation == null)
                return;

            var dialogueManager = DialogueManager.Instance;
            if (dialogueManager == null)
                dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager == null || dialogueManager.IsDialogueActive)
                return;

            dialogueManager.StartConversation(blockedConversation);
        }

        private bool HasRequiredItem()
        {
            if (requiredItem == null)
                return true;

            var inventorySystem = FindFirstObjectByType<InventorySystem>();
            if (inventorySystem == null)
                return false;

            return inventorySystem.HasItem(requiredItem, Mathf.Max(1, requiredItemQuantity));
        }

        private void ApplySuccessfulEntryEffects()
        {
            if (string.IsNullOrWhiteSpace(setFlagOnSuccessfulEntry) || GameStateFlags.Instance == null)
                return;

            GameStateFlags.Instance.SetFlag(setFlagOnSuccessfulEntry, setFlagValueOnSuccessfulEntry);
        }
    }
}
