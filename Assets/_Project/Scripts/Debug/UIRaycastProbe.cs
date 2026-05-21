using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class UIRaycastProbe : MonoBehaviour
{
    [SerializeField] private bool enabledByDefault = false;
    [SerializeField] private bool includePhysicsRaycasts = true;
    [SerializeField] private bool verboseUiDetails = true;
    [SerializeField] private bool logMissingMainCamera = false;

    private bool probeEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        if (FindFirstObjectByType<UIRaycastProbe>(FindObjectsInactive.Include) != null)
            return;

        var root = new GameObject("UIRaycastProbe");
        DontDestroyOnLoad(root);
        root.AddComponent<UIRaycastProbe>();
    }

    private void Awake()
    {
        probeEnabled = enabledByDefault;
        Debug.Log("[UIRaycastProbe] Ready. Left-click to inspect raycast hits. Press F9 to toggle probe on/off.");
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            probeEnabled = !probeEnabled;
            Debug.Log($"[UIRaycastProbe] Probe {(probeEnabled ? "ENABLED" : "DISABLED")}");
        }
#else
        if (Input.GetKeyDown(KeyCode.F9))
        {
            probeEnabled = !probeEnabled;
            Debug.Log($"[UIRaycastProbe] Probe {(probeEnabled ? "ENABLED" : "DISABLED")}");
        }
#endif

        if (!probeEnabled)
            return;

        if (WasLeftClickThisFrame())
            LogClickTargets();
    }

    private bool WasLeftClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.leftButton.wasPressedThisFrame;
#endif
        return Input.GetMouseButtonDown(0);
    }

    private Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
#endif
        return Input.mousePosition;
    }

    private void LogClickTargets()
    {
        var position = GetPointerPosition();
        Debug.Log($"[UIRaycastProbe] Click at {position}");

        LogUiHits(position);

        if (includePhysicsRaycasts)
            LogPhysicsHits(position);
    }

    private void LogUiHits(Vector2 position)
    {
        var currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
        {
            Debug.LogWarning("[UIRaycastProbe] No EventSystem.current found. UI clicks cannot be raycast.");
            return;
        }

        var eventData = new PointerEventData(currentEventSystem)
        {
            position = position
        };

        var results = new List<RaycastResult>();
        currentEventSystem.RaycastAll(eventData, results);

        if (results.Count == 0)
        {
            Debug.Log("[UIRaycastProbe] UI hits: none");
            return;
        }

        for (var i = 0; i < results.Count; i++)
        {
            var hit = results[i];
            var target = hit.gameObject;
            var path = BuildPath(target.transform);

            if (!verboseUiDetails)
            {
                Debug.Log($"[UIRaycastProbe] UI hit #{i + 1}: {path}");
                continue;
            }

            var graphic = target.GetComponent<Graphic>();
            var canvasGroup = target.GetComponentInParent<CanvasGroup>();
            var raycastTarget = graphic == null || graphic.raycastTarget;
            var canvasInfo = hit.module != null ? hit.module.GetType().Name : "(no module)";
            var groupInfo = canvasGroup == null
                ? "none"
                : $"alpha={canvasGroup.alpha:0.##}, interactable={canvasGroup.interactable}, blocksRaycasts={canvasGroup.blocksRaycasts}";

            Debug.Log(
                $"[UIRaycastProbe] UI hit #{i + 1}: {path} | module={canvasInfo} | " +
                $"distance={hit.distance:0.###} sort={hit.sortingLayer}/{hit.sortingOrder} depth={hit.depth} | " +
                $"graphic.raycastTarget={raycastTarget} | canvasGroup={groupInfo}");
        }
    }

    private void LogPhysicsHits(Vector2 position)
    {
        var camera = Camera.main;
        if (camera == null)
        {
            if (logMissingMainCamera)
                Debug.Log("[UIRaycastProbe] Physics hits: skipped (no Camera.main)");

            return;
        }

        var ray = camera.ScreenPointToRay(position);
        var hits = Physics.RaycastAll(ray, 1000f);

        if (hits.Length == 0)
        {
            Debug.Log("[UIRaycastProbe] Physics hits: none");
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            Debug.Log($"[UIRaycastProbe] Physics hit #{i + 1}: {BuildPath(hit.transform)} dist={hit.distance:0.###}");
        }
    }

    private static string BuildPath(Transform current)
    {
        if (current == null)
            return "(null)";

        var names = new List<string>();
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
