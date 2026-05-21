using UnityEngine;
using Underbrew.Core;

public class Pickupable : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptActionText = "Press E to pick up";
    [SerializeField] private string fallbackDisplayName = "item";
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;

    [Header("Resource Respawn")]
    [SerializeField] private bool useResourceRespawnTracking;
    [SerializeField] private string resourceNodeId;
    [SerializeField] private float resourceRespawnCooldownSeconds = 300f;

    [Header("Post Pickup Effects")]
    [SerializeField] private DialogueConversation pickupConversation;
    [SerializeField] private string setFlagOnPickup;
    [SerializeField] private bool setFlagValueOnPickup = true;
    [SerializeField] private bool consumeEvenWithoutItem = true;

    private InventorySystem inventorySystem;

    public string PromptText
    {
        get
        {
            var displayName = itemData != null && !string.IsNullOrWhiteSpace(itemData.ItemName)
                ? itemData.ItemName
                : fallbackDisplayName;

            return $"{promptActionText} {displayName}";
        }
    }

    private void Awake()
    {
        inventorySystem = FindFirstObjectByType<InventorySystem>();
        WarnIfPersistenceConfigurationIsRisky();

        if (!ShouldExistForCurrentPersistenceState())
            Destroy(gameObject);
    }

    public void Interact()
    {
        var hasItemToAdd = itemData != null;

        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();

        if (hasItemToAdd && inventorySystem == null)
        {
            Debug.LogWarning("[Pickupable] InventorySystem not found. Pickup was not consumed.");
            return;
        }

        if (hasItemToAdd && amount <= 0)
        {
            Debug.LogWarning($"[Pickupable] Invalid amount on '{name}'. Amount must be greater than zero.");
            return;
        }

        if (hasItemToAdd && !inventorySystem.Add(itemData, amount))
        {
            Debug.Log($"[Pickupable] Inventory is full. Could not pick up {itemData.ItemName}.");
            return;
        }

        var hasPostPickupEffects = !string.IsNullOrWhiteSpace(setFlagOnPickup) || pickupConversation != null;
        if (!hasItemToAdd && !consumeEvenWithoutItem && !hasPostPickupEffects)
        {
            Debug.LogWarning($"[Pickupable] '{name}' has no item and no post-pickup effects. Pickup was not consumed.");
            return;
        }

        ApplyPostPickupEffects();
        MarkPersistenceConsumed();
        AudioManager.Instance?.PlaySfx(AudioCueId.PickupGeneric);

        Destroy(gameObject);
    }

    public void CancelInteract()
    {
    }

    private void ApplyPostPickupEffects()
    {
        if (!string.IsNullOrWhiteSpace(setFlagOnPickup) && GameStateFlags.Instance != null)
            GameStateFlags.Instance.SetFlag(setFlagOnPickup, setFlagValueOnPickup);

        if (pickupConversation == null)
            return;

        var manager = DialogueManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<DialogueManager>();

        if (manager == null)
        {
            Debug.LogWarning("[Pickupable] DialogueManager not found. Pickup dialogue was not started.");
            return;
        }

        if (!manager.StartConversation(pickupConversation))
            Debug.LogWarning("[Pickupable] Could not start pickup dialogue conversation.");
    }

    private bool ShouldExistForCurrentPersistenceState()
    {
        if (!useResourceRespawnTracking)
            return true;

        if (string.IsNullOrWhiteSpace(resourceNodeId))
            return true;

        var respawnState = ResourceRespawnState.Instance;
        if (respawnState == null)
            return true;

        return respawnState.IsResourceAvailable(resourceNodeId);
    }

    private void MarkPersistenceConsumed()
    {
        if (!useResourceRespawnTracking)
            return;

        if (string.IsNullOrWhiteSpace(resourceNodeId) || ResourceRespawnState.Instance == null)
            return;

        ResourceRespawnState.Instance.MarkCollected(resourceNodeId, resourceRespawnCooldownSeconds);
    }

    private void OnValidate()
    {
        if (resourceRespawnCooldownSeconds < 0f)
            resourceRespawnCooldownSeconds = 0f;

        WarnIfPersistenceConfigurationIsRisky();
    }

    private void WarnIfPersistenceConfigurationIsRisky()
    {
        if (useResourceRespawnTracking && string.IsNullOrWhiteSpace(resourceNodeId))
        {
            Debug.LogWarning(
                $"[Pickupable] '{name}' has resource respawn tracking enabled but no resourceNodeId. Resource cooldown state will not persist.",
                this);
        }
    }
}
