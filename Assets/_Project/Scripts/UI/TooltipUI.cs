using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [SerializeField] private Image backgroundPanel;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Vector2 mouseOffset = new(20f, -20f);
    [SerializeField] private Vector2 screenPadding = new(8f, 8f);
    [SerializeField] private int sortingOrderOffset = 100;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas parentCanvas;
    private Canvas tooltipCanvas;
    private readonly Vector3[] worldCorners = new Vector3[4];
    private Object currentSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();
        tooltipCanvas = GetComponent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (tooltipCanvas == null)
            tooltipCanvas = gameObject.AddComponent<Canvas>();

        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + sortingOrderOffset : sortingOrderOffset;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        DisableTooltipRaycasts();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (gameObject.activeSelf == false)
            return;

        FollowMouse();
    }

    public void Show(string title, string description, Object source)
    {
        currentSource = source;

        if (tooltipText != null)
        {
            tooltipText.text = string.IsNullOrEmpty(title) ? string.Empty : title;
            tooltipText.ForceMeshUpdate();
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrEmpty(description) ? string.Empty : description;
            descriptionText.ForceMeshUpdate();
        }

        if (backgroundPanel != null)
            backgroundPanel.enabled = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        DisableTooltipRaycasts();

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        FollowMouse();
    }

    public void Hide()
    {
        Hide(null);
    }

    public void Hide(Object source)
    {
        if (source != null && currentSource != null && source != currentSource)
            return;

        currentSource = null;

        if (tooltipText != null)
            tooltipText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = string.Empty;

        if (backgroundPanel != null)
            backgroundPanel.enabled = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (rectTransform == null)
            return;

        var targetPosition = GetSmartTargetPosition();
        rectTransform.position = targetPosition;

        var eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        rectTransform.GetWorldCorners(worldCorners);

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;

        for (var i = 0; i < worldCorners.Length; i++)
        {
            var screenCorner = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[i]);
            minX = Mathf.Min(minX, screenCorner.x);
            maxX = Mathf.Max(maxX, screenCorner.x);
            minY = Mathf.Min(minY, screenCorner.y);
            maxY = Mathf.Max(maxY, screenCorner.y);
        }

        var delta = Vector2.zero;

        if (minX < screenPadding.x)
            delta.x = screenPadding.x - minX;
        else if (maxX > Screen.width - screenPadding.x)
            delta.x = (Screen.width - screenPadding.x) - maxX;

        if (minY < screenPadding.y)
            delta.y = screenPadding.y - minY;
        else if (maxY > Screen.height - screenPadding.y)
            delta.y = (Screen.height - screenPadding.y) - maxY;

        rectTransform.position = targetPosition + delta;
    }

    private Vector2 GetSmartTargetPosition()
    {
        var mousePosition = (Vector2)Input.mousePosition;
        var tooltipSize = rectTransform.rect.size;
        var absOffsetX = Mathf.Abs(mouseOffset.x);
        var absOffsetY = Mathf.Abs(mouseOffset.y);

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            var scale = parentCanvas.scaleFactor <= 0f ? 1f : parentCanvas.scaleFactor;
            tooltipSize *= scale;
        }

        var placeLeft = mousePosition.x + absOffsetX + tooltipSize.x > Screen.width - screenPadding.x;
        var placeAbove = mousePosition.y - absOffsetY - tooltipSize.y < screenPadding.y;

        var xOffset = placeLeft ? -absOffsetX : absOffsetX;
        var yOffset = placeAbove ? absOffsetY : -absOffsetY;

        return mousePosition + new Vector2(xOffset, yOffset);
    }

    private void DisableTooltipRaycasts()
    {
        if (backgroundPanel != null)
            backgroundPanel.raycastTarget = false;

        if (tooltipText != null)
            tooltipText.raycastTarget = false;

        if (descriptionText != null)
            descriptionText.raycastTarget = false;
    }
}
