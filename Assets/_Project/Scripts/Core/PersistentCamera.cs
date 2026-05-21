using System.Collections;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentCamera : MonoBehaviour
{
    public static PersistentCamera Instance { get; private set; }

    [SerializeField] private bool snapToPlayerOnSceneLoad = true;
    [SerializeField] private int sceneLoadSnapSettleFrames = 4;
    [SerializeField] private bool forceInstantProCameraWarpOnSnap = true;
    [SerializeField] private bool enforceMainCameraTag = true;

    private Coroutine sceneLoadSnapRoutine;
    private ProCamera2D proCamera2D;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EnsureMainCameraTag();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (sceneLoadSnapRoutine != null)
        {
            StopCoroutine(sceneLoadSnapRoutine);
            sceneLoadSnapRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SnapToPosition(Vector3 worldPosition)
    {
        var desiredWorldPosition = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        transform.position = desiredWorldPosition;

        if (!forceInstantProCameraWarpOnSnap)
            return;

        var proCam = ResolveProCamera2D();
        if (proCam == null)
            return;

        var proCamTransform = proCam.transform;
        var desiredCameraWorld = new Vector3(worldPosition.x, worldPosition.y, proCamTransform.position.z);
        var desiredCameraLocal = proCamTransform.parent != null
            ? proCamTransform.parent.InverseTransformPoint(desiredCameraWorld)
            : desiredCameraWorld;

        proCam.MoveCameraInstantlyToPosition(new Vector2(desiredCameraLocal.x, desiredCameraLocal.y));
    }

    public void SnapToPlayer()
    {
        var player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            SnapToPosition(player.transform.position);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        EnsureMainCameraTag();

        if (!snapToPlayerOnSceneLoad)
            return;

        if (sceneLoadSnapRoutine != null)
            StopCoroutine(sceneLoadSnapRoutine);

        sceneLoadSnapRoutine = StartCoroutine(SnapToPlayerOverFrames());
    }

    private IEnumerator SnapToPlayerOverFrames()
    {
        SnapToPlayer();

        var settleFrames = Mathf.Max(0, sceneLoadSnapSettleFrames);
        for (var i = 0; i < settleFrames; i++)
        {
            yield return new WaitForEndOfFrame();
            SnapToPlayer();
        }

        sceneLoadSnapRoutine = null;
    }

    private void EnsureMainCameraTag()
    {
        if (!enforceMainCameraTag)
            return;

        var cameraComponent = GetComponentInChildren<Camera>(true);
        if (cameraComponent == null)
            return;

        if (!cameraComponent.CompareTag("MainCamera"))
            cameraComponent.tag = "MainCamera";
    }

    private ProCamera2D ResolveProCamera2D()
    {
        if (proCamera2D != null)
            return proCamera2D;

        proCamera2D = GetComponent<ProCamera2D>();
        if (proCamera2D != null)
            return proCamera2D;

        proCamera2D = GetComponentInChildren<ProCamera2D>(true);
        if (proCamera2D != null)
            return proCamera2D;

        proCamera2D = FindFirstObjectByType<ProCamera2D>(FindObjectsInactive.Include);
        return proCamera2D;
    }
}
