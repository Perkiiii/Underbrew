using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PauseButtonHoverVisuals : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject background;
    [SerializeField] private GameObject border;

    private void Awake()
    {
        AutoResolveVisualTargets();
        SetHovered(false);
    }

    private void OnEnable()
    {
        SetHovered(false);
    }

    public void AutoResolveVisualTargets()
    {
        if (background == null)
            background = FindChildByName(transform, "Background");

        if (border == null)
            border = FindChildByName(transform, "Border");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    public void SetHovered(bool hovered)
    {
        if (background != null)
            background.SetActive(hovered);

        if (border != null)
            border.SetActive(hovered);
    }

    private static GameObject FindChildByName(Transform root, string expectedName)
    {
        if (root == null || string.IsNullOrWhiteSpace(expectedName))
            return null;

        var children = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (!string.Equals(child.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return child.gameObject;
        }

        return null;
    }
}
