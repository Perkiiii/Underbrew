using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using UnityEngine.SceneManagement;

public class TilemapExporter : MonoBehaviour
{
    public Camera exportCamera;
    public Tilemap tilemap;
    public int pixelsPerUnit = 32; // match your tile sprite import setting

    [ContextMenu("Export Tilemap")]
    public void ExportTilemap()
    {
        if (exportCamera == null || tilemap == null)
        {
            Debug.LogError("Tilemap export failed: Assign both Export Camera and Tilemap.");
            return;
        }

        tilemap.CompressBounds(); // shrink to only used tiles

        BoundsInt bounds = tilemap.cellBounds;

        if (bounds.size.x <= 0 || bounds.size.y <= 0)
        {
            Debug.LogWarning("Tilemap export skipped: Tilemap has no painted cells.");
            return;
        }

        int width = bounds.size.x;
        int height = bounds.size.y;

        int textureWidth = width * pixelsPerUnit;
        int textureHeight = height * pixelsPerUnit;

        // Compute exact world-space bounds using min/max cell corners.
        Vector3 worldMin = tilemap.CellToWorld(bounds.min);
        Vector3 worldMax = tilemap.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, bounds.zMax));
        Vector3 center = (worldMin + worldMax) * 0.5f;
        float worldWidth = worldMax.x - worldMin.x;
        float worldHeight = worldMax.y - worldMin.y;

        // Save original camera properties
        Vector3 originalPosition = exportCamera.transform.position;
        float originalOrthographicSize = exportCamera.orthographicSize;
        RenderTexture originalTargetTexture = exportCamera.targetTexture;
        float originalAspect = exportCamera.aspect;

        RenderTexture rt = null;
        Texture2D tex = null;

        try
        {
            exportCamera.transform.position = new Vector3(center.x, center.y, originalPosition.z);

            float targetAspect = (float)textureWidth / textureHeight;
            exportCamera.aspect = targetAspect;

            // Fit both width and height. Small padding avoids 1-pixel clipping at edges.
            const float edgePadding = 0.01f;
            exportCamera.orthographicSize = Mathf.Max(worldHeight * 0.5f, (worldWidth * 0.5f) / targetAspect) + edgePadding;

            rt = new RenderTexture(textureWidth, textureHeight, 24);
            exportCamera.targetTexture = rt;

            tex = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);

            exportCamera.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            tex.Apply();

            string sceneName = SceneManager.GetActiveScene().name;
            string fileName = $"TilemapExport_{sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(Application.dataPath, fileName);

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);

            Debug.Log($"Tilemap exported to {filePath}");
        }
        finally
        {
            exportCamera.transform.position = originalPosition;
            exportCamera.orthographicSize = originalOrthographicSize;
            exportCamera.aspect = originalAspect;
            exportCamera.targetTexture = originalTargetTexture;
            RenderTexture.active = null;

            if (rt != null)
                DestroyImmediate(rt);

            if (tex != null)
                DestroyImmediate(tex);
        }
    }
}