using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalQuestListEntryUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite unselectedBackgroundSprite;
    [SerializeField] private Sprite selectedBackgroundSprite;
    [SerializeField] private GameObject selectedIndicator;

    private QuestJournalEntry boundQuest;
    private System.Action<JournalQuestListEntryUI> clickHandler;

    public QuestJournalEntry BoundQuest => boundQuest;

    private void Awake()
    {
        AutoResolveReferences();

        if (button != null)
            button.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void Configure(QuestJournalEntry quest, bool isCompleted, bool isSelected, System.Action<JournalQuestListEntryUI> onClick)
    {
        boundQuest = quest;
        clickHandler = onClick;

        if (titleText != null)
        {
            var rawTitle = quest != null ? quest.Title : string.Empty;
            titleText.text = isCompleted ? $"<s>{rawTitle}</s>" : rawTitle;
        }

        ApplySelectionState(isSelected);
    }

    public void SetSelected(bool isSelected)
    {
        ApplySelectionState(isSelected);
    }

    private void HandleClicked()
    {
        clickHandler?.Invoke(this);
    }

    private void AutoResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (titleText == null)
            titleText = GetComponentInChildren<TMP_Text>(true);
    }

    private void ApplySelectionState(bool isSelected)
    {
        if (backgroundImage != null)
        {
            var targetSprite = isSelected ? selectedBackgroundSprite : unselectedBackgroundSprite;
            if (targetSprite != null)
                backgroundImage.sprite = targetSprite;
        }

        if (selectedIndicator != null)
            selectedIndicator.SetActive(isSelected);
    }
}
