using Underbrew.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Underbrew.World
{
    [RequireComponent(typeof(Collider2D))]
    public class BenchCheckpoint : MonoBehaviour, IInteractable
    {
        [Header("Checkpoint")]
        [SerializeField] private string checkpointId;
        [SerializeField] private Transform checkpointSpawnPoint;
        [SerializeField] private bool saveImmediately = true;

        [Header("Prompt")]
        [SerializeField] private string promptActionText = "Press E to rest at";
        [SerializeField] private string benchDisplayName = "Bench";

        [Header("Optional State Flag")]
        [SerializeField] private string activatedFlagKey;

        [Header("Optional Dialogue")]
        [SerializeField] private DialogueConversation saveConversation;

        [Header("Optional Light Pulse")]
        [SerializeField] private Light2D pulsingSpotlight;
        [SerializeField] private bool pulseSpotlight = true;
        [SerializeField] private float pulseMinIntensity = 0.5f;
        [SerializeField] private float pulseMaxIntensity = 1.5f;
        [SerializeField] private float pulseSpeed = 1f;

        [Header("Hooks")]
        [SerializeField] private UnityEvent onBenchRested;

        public string PromptText => $"{promptActionText} {benchDisplayName}";

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogWarning($"[BenchCheckpoint] '{name}' is missing a Collider2D. Interactor triggers will not detect this bench.");
                return;
            }

            if (!col.enabled)
                Debug.LogWarning($"[BenchCheckpoint] '{name}' has a disabled Collider2D. Enable it so the player can interact.");

            WarnIfCheckpointIdMissing();
        }

        private void Update()
        {
            if (!pulseSpotlight || pulsingSpotlight == null || pulseSpeed <= 0f)
                return;

            var oscillation = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            pulsingSpotlight.intensity = Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, oscillation);
        }

        public void Interact()
        {
            var manager = CheckpointManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[BenchCheckpoint] CheckpointManager not found.");
                return;
            }

            var spawnPoint = checkpointSpawnPoint != null ? checkpointSpawnPoint.position : transform.position;
            var sceneName = SceneManager.GetActiveScene().name;
            var id = string.IsNullOrWhiteSpace(checkpointId) ? name : checkpointId;

            bool checkpointActivated = manager.ActivateCheckpoint(id, spawnPoint, sceneName, saveImmediately);

            if (saveImmediately && checkpointActivated)
                AudioManager.Instance?.PlaySfx(AudioCueId.BenchCheckpointSave);

            if (!string.IsNullOrWhiteSpace(activatedFlagKey) && GameStateFlags.Instance != null)
                GameStateFlags.Instance.SetFlag(activatedFlagKey, true);

            TryStartSaveDialogue();

            onBenchRested?.Invoke();
        }

        public void CancelInteract()
        {
        }

        private void TryStartSaveDialogue()
        {
            if (!saveImmediately || saveConversation == null)
                return;

            var dialogueManager = DialogueManager.Instance;
            if (dialogueManager == null)
                dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager == null || dialogueManager.IsDialogueActive)
                return;

            dialogueManager.StartConversation(saveConversation);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(benchDisplayName))
                benchDisplayName = "Bench";

            if (string.IsNullOrWhiteSpace(promptActionText))
                promptActionText = "Press E to rest at";

            pulseMinIntensity = Mathf.Max(0f, pulseMinIntensity);
            pulseMaxIntensity = Mathf.Max(0f, pulseMaxIntensity);
            pulseSpeed = Mathf.Max(0f, pulseSpeed);

            if (pulseMaxIntensity < pulseMinIntensity)
            {
                var temp = pulseMinIntensity;
                pulseMinIntensity = pulseMaxIntensity;
                pulseMaxIntensity = temp;
            }

            var col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
                col.isTrigger = true;

            WarnIfCheckpointIdMissing();
        }

        private void WarnIfCheckpointIdMissing()
        {
            if (string.IsNullOrWhiteSpace(checkpointId))
            {
                Debug.LogWarning(
                    $"[BenchCheckpoint] '{name}' has a blank checkpointId and will fall back to the GameObject name. Assign an explicit checkpointId to stabilize saves.",
                    this);
            }
        }
    }
}
