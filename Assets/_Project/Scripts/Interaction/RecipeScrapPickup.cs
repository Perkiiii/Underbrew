using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Underbrew.Core;

public class RecipeScrapPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptActionText = "Press E to read";
    [SerializeField] private string fallbackDisplayName = "recipe scrap";
    [SerializeField] private BrewingRecipe recipe;

    [Header("One-Shot Persistence")]
    [SerializeField] private string consumedFlagKey;

    [Header("Post Pickup Effects")]
    [SerializeField] private DialogueConversation pickupConversation;
    [SerializeField] private string setFlagOnPickup;
    [SerializeField] private bool setFlagValueOnPickup = true;

    [Header("Optional Light Pulse")]
    [SerializeField] private Light2D pulsingSpotlight;
    [SerializeField] private bool pulseSpotlight = true;
    [SerializeField] private float pulseMinIntensity = 0.5f;
    [SerializeField] private float pulseMaxIntensity = 1.5f;
    [SerializeField] private float pulseSpeed = 1f;

    public string PromptText
    {
        get
        {
            var displayName = recipe != null && recipe.OutputItem != null && !string.IsNullOrWhiteSpace(recipe.OutputItem.ItemName)
                ? recipe.OutputItem.ItemName
                : fallbackDisplayName;

            return $"{promptActionText} {displayName}";
        }
    }

    private void Awake()
    {
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

        var discoverySystem = PotionRecipeDiscoverySystem.Instance ?? FindFirstObjectByType<PotionRecipeDiscoverySystem>(FindObjectsInactive.Include);
        if (recipe == null || discoverySystem == null)
        {
            Debug.LogWarning("[RecipeScrapPickup] Missing recipe or PotionRecipeDiscoverySystem. Pickup was not consumed.");
            return;
        }

        var wasNewDiscovery = discoverySystem.Discover(recipe);
        MarkConsumed();
        ApplyPostPickupEffects();
        AudioManager.Instance?.PlaySfx(AudioCueId.PickupSpecial);
        if (wasNewDiscovery)
            AudioManager.Instance?.PlaySfx(AudioCueId.RecipeUnlock);

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
            Debug.LogWarning("[RecipeScrapPickup] DialogueManager not found. Pickup dialogue was not started.");
            return;
        }

        if (!manager.StartConversation(pickupConversation))
            Debug.LogWarning("[RecipeScrapPickup] Could not start pickup dialogue conversation.");
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
                $"[RecipeScrapPickup] '{name}' has a blank consumedFlagKey. Collected state will not persist across saves.",
                this);
        }
    }
}
