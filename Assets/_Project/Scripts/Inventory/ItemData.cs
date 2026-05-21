using UnityEngine;

public enum ItemType
{
    RawResource,
    CraftingResource,
    Potion
}

public enum EffectType
{
    Heal,
    Speed,
    Vision,
    Stamina
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Underbrew/Items/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string saveId;
    [SerializeField] private string itemName;
    [SerializeField] private Sprite icon;

    [Header("Tooltip")]
    [SerializeField, TextArea(2, 5)] private string tooltipDescription;

    [Header("Journal")]
    [SerializeField] private int journalSortOrder;
    [SerializeField] private Sprite journalLargeIcon;
    [SerializeField] private ItemType itemType;
    [SerializeField, TextArea(3, 10)] private string journalEntry;
    [SerializeField] private EffectType effectType;
    [SerializeField] private float effectValue;
    [SerializeField] private float effectDuration;

    public string ItemName => itemName;
    public string SaveId => string.IsNullOrWhiteSpace(saveId) ? itemName : saveId;
    public Sprite Icon => icon;
    public int JournalSortOrder => journalSortOrder;
    public Sprite JournalLargeIcon => journalLargeIcon != null ? journalLargeIcon : icon;
    public ItemType Type => itemType;
    public string TooltipDescription => tooltipDescription;
    public string JournalEntry => string.IsNullOrWhiteSpace(journalEntry) ? tooltipDescription : journalEntry;
    public EffectType PotionEffectType => effectType;
    public float EffectValue => effectValue;
    public float EffectDuration => effectDuration;
    public bool IsPotion => itemType == ItemType.Potion;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(saveId))
            Debug.LogWarning($"[ItemData] '{name}' has a blank saveId and will fall back to itemName '{itemName}'. Assign an explicit saveId to stabilize persistence.", this);
    }
}
