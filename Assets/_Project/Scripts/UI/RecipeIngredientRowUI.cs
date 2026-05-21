using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeIngredientRowUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text amountText;

    public void Initialize(RecipeRequirement requirement, int currentAmount)
    {
        var item = requirement.Item;

        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (itemNameText != null)
            itemNameText.text = item != null ? item.ItemName : "Missing Item";

        if (amountText != null)
        {
            var enough = currentAmount >= requirement.Quantity;
            amountText.text = $"{currentAmount}/{requirement.Quantity}";
            amountText.color = enough ? Color.white : new Color(1f, 0.5f, 0.5f);
        }
    }
}
