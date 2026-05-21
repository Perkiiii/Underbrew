using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Underbrew.Core;

public class OneShotPickupable : MonoBehaviour, IInteractable
{
    public enum PickupAudioType
    {
        Generic,
        Special
    }

    [SerializeField] private string promptActionText = "Press E to pick up";
    [SerializeField] private string fallbackDisplayName = "item";
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;

    [Header("One-Shot Persistence")]
    [SerializeField] private string consumedFlagKey;

    [Header("Pickup Requirements")]
    [SerializeField] private string requiredFlagKey;
    [SerializeField] private bool requiredFlagValue = true;
    [SerializeField] private DialogueConversation blockedPickupConversation;

    [Header("Post Pickup Effects")]
    [SerializeField] private DialogueConversation pickupConversation;
    [SerializeField] private string setFlagOnPickup;
    [SerializeField] private bool setFlagValueOnPickup = true;
    [SerializeField] private bool consumeEvenWithoutItem = true;

    [Header("Audio")]
    [SerializeField] private PickupAudioType pickupAudioType = PickupAudioType.Special;

    [Header("Optional Light Pulse")]
    [SerializeField] private Light2D pulsingSpotlight;
    [SerializeField] private bool pulseSpotlight = true;
    [SerializeField] private float pulseMinIntensity = 0.5f;
    [SerializeField] private float pulseMaxIntensity = 1.5f;
    [SerializeField] private float pulseSpeed = 1f;

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

        if (IsAlreadyConsumed())
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (GameStateFlags.Instance != null)
            GameStateFlags.Instance.OnFlagChanged += HandleFlagChanged;
    }

    private void OnDisable()
    {
        if (GameStateFlags.Instance != null)
            GameStateFlags.Instance.OnFlagChanged -= HandleFlagChanged;
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
        if (IsAlreadyConsumed())
        {
            Destroy(gameObject);
            return;
        }

        if (!CanPickupNow())
        {
            TryStartBlockedPickupConversation();
            return;
        }

        var hasItemToAdd = itemData != null;

        if (inventorySystem == null)
            inventorySystem = FindFirstObjectByType<InventorySystem>();

        if (hasItemToAdd && inventorySystem == null)
        {
            Debug.LogWarning("[OneShotPickupable] InventorySystem not found. Pickup was not consumed.");
            return;
        }

        if (hasItemToAdd && amount <= 0)
        {
            Debug.LogWarning($"[OneShotPickupable] Invalid amount on '{name}'. Amount must be greater than zero.");
            return;
        }

        if (hasItemToAdd && !inventorySystem.Add(itemData, amount))
        {
            Debug.Log($"[OneShotPickupable] Inventory is full. Could not pick up {itemData.ItemName}.");
            return;
        }

        var hasPostPickupEffects = !string.IsNullOrWhiteSpace(setFlagOnPickup) || pickupConversation != null;
        if (!hasItemToAdd && !consumeEvenWithoutItem && !hasPostPickupEffects)
        {
            Debug.LogWarning($"[OneShotPickupable] '{name}' has no item and no post-pickup effects. Pickup was not consumed.");
            return;
        }

        MarkConsumed();
        ApplyPostPickupEffects();
        AudioManager.Instance?.PlaySfx(pickupAudioType == PickupAudioType.Special ? AudioCueId.PickupSpecial : AudioCueId.PickupGeneric);

        Destroy(gameObject);
    }

    public void CancelInteract()
    {
    }

    private bool IsAlreadyConsumed()
    {
        if (string.IsNullOrWhiteSpace(consumedFlagKey))
            return false;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return false;

        return flags.GetFlag(consumedFlagKey);
    }

    private void MarkConsumed()
    {
        if (string.IsNullOrWhiteSpace(consumedFlagKey) || GameStateFlags.Instance == null)
            return;

        GameStateFlags.Instance.SetFlag(consumedFlagKey, true);
    }

    private bool CanPickupNow()
    {
        if (string.IsNullOrWhiteSpace(requiredFlagKey))
            return true;

        var flags = GameStateFlags.Instance;
        if (flags == null)
            return false;

        return flags.GetFlag(requiredFlagKey) == requiredFlagValue;
    }

    private void TryStartBlockedPickupConversation()
    {
        if (blockedPickupConversation == null)
            return;

        var manager = DialogueManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<DialogueManager>();

        if (manager == null)
        {
            Debug.LogWarning("[OneShotPickupable] DialogueManager not found. Blocked pickup dialogue was not started.");
            return;
        }

        if (manager.IsDialogueActive)
            return;

        if (!manager.StartConversation(blockedPickupConversation))
            Debug.LogWarning("[OneShotPickupable] Could not start blocked pickup dialogue conversation.");
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
            Debug.LogWarning("[OneShotPickupable] DialogueManager not found. Pickup dialogue was not started.");
            return;
        }

        if (!manager.StartConversation(pickupConversation))
            Debug.LogWarning("[OneShotPickupable] Could not start pickup dialogue conversation.");
    }

    private void OnValidate()
    {
        pulseMinIntensity = Mathf.Max(0f, pulseMinIntensity);
        pulseMaxIntensity = Mathf.Max(0f, pulseMaxIntensity);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);

        if (pulseMaxIntensity < pulseMinIntensity)
        {
            var temp = pulseMinIntensity;
            pulseMinIntensity = pulseMaxIntensity;
            pulseMaxIntensity = temp;
        }

        WarnIfPersistenceConfigurationIsRisky();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (!value)
            return;

        if (string.IsNullOrWhiteSpace(consumedFlagKey))
            return;

        if (!string.Equals(key, consumedFlagKey, System.StringComparison.Ordinal))
            return;

        Destroy(gameObject);
    }

    private void WarnIfPersistenceConfigurationIsRisky()
    {
        if (string.IsNullOrWhiteSpace(consumedFlagKey))
        {
            Debug.LogWarning(
                $"[OneShotPickupable] '{name}' has a blank consumedFlagKey. Collected state will not persist across saves.",
                this);
        }
    }
}
