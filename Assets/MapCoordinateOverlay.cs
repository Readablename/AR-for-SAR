using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// Demonstrates using multiple nearest grid points for an IDW interpolation
/// so that your test marker can smoothly position itself between grid markers.
/// </summary>
public class MapCoordinateOverlay : MonoBehaviour
{
    [Header("Marker & Parent")]
    public GameObject markerPrefab;
    public RectTransform mapContent;

    [Header("JSON Grid Data")]
    public TextAsset jsonFile;

    [Header("Show Grid (Optional)")]
    public bool showGrid = false;
    public int gridStep = 10; // skip some for visualization

    [Header("Inverse Distance Weighting Settings")]
    [Tooltip("Number of neighbors to use for IDW interpolation.")]
    public int kNeighbors = 4;

    [Tooltip("Power parameter for IDW. 1=linear, 2=square, etc.")]
    public float distancePower = 1f;

    [Header("Test Marker Settings")]
    public bool placeTestMarker = true;
    public float testLat = 63.4f;
    public float testLon = 10.4f;

    // The corners for top-left, top-right, bottom-left, bottom-right
    // normalizedX=0 => x=-48, normalizedX=1 => x=16813
    // normalizedY=0 => y=48,  normalizedY=1 => y=-12276
    private float leftX = -48f;
    private float rightX = 16813f;
    private float topY = 48f;
    private float bottomY = -12276f;

    private MapData mapData;

    void Start()
    {
        // 1) Load grid from JSON
        LoadMapData();

        // 2) Optionally display all grid points
        if (showGrid)
        {
            PlaceAllGridMarkers();
        }

        // 3) Optionally place a test marker at lat/lon using IDW
        if (placeTestMarker)
        {
            PlaceMarkerAtLatLon(testLat, testLon);
            Debug.Log($"Placed test marker at lat={testLat}, lon={testLon}");
        }
    }

    private void LoadMapData()
    {
        if (jsonFile == null)
        {
            Debug.LogError("No JSON file assigned!");
            return;
        }

        mapData = JsonUtility.FromJson<MapData>(jsonFile.text);
        if (mapData == null || mapData.grid == null)
        {
            Debug.LogError("JSON data invalid or no 'grid' array found!");
            return;
        }

        Debug.Log($"Loaded {mapData.grid.Length} grid points from JSON.");
    }

    /// <summary>
    /// Places a marker at the lat/lon by IDW interpolation of the k nearest grid points.
    /// </summary>
    public void PlaceMarkerAtLatLon(float lat, float lon)
    {
        if (mapData == null || mapData.grid == null)
        {
            Debug.LogError("No grid data loaded!");
            return;
        }

        // 1) Get the k nearest neighbors in lat/lon space
        List<GridPoint> neighbors = FindKClosestNeighbors(lat, lon, kNeighbors);

        // 2) Do IDW to find final normalized (x,y)
        Vector2 norm = InverseDistanceWeightedNormalized(lat, lon, neighbors, distancePower);

        // 3) Convert to Unity coords
        Vector2 unityPos = ConvertNormalizedToUnity(norm.x, norm.y);

        // 4) Instantiate the marker
        Debug.Log($"IDW => lat={lat}, lon={lon} => norm=({norm.x:F3},{norm.y:F3}), unity=({unityPos.x:F1},{unityPos.y:F1})");
        InstantiateMarker(unityPos);
    }

    /// <summary>
    /// Finds the k nearest neighbors in lat/lon space from the grid.
    /// </summary>
    private List<GridPoint> FindKClosestNeighbors(float lat, float lon, int k)
    {
        // We'll store (distance, gridPoint) pairs, then sort by distance.
        List<(float distSq, GridPoint gp)> distList = new List<(float, GridPoint)>(mapData.grid.Length);

        foreach (GridPoint gp in mapData.grid)
        {
            float dx = gp.lat - lat;
            float dy = gp.lon - lon;
            float distSq = dx * dx + dy * dy; // squared distance
            distList.Add((distSq, gp));
        }

        // Sort ascending by distSq
        distList.Sort((a, b) => a.distSq.CompareTo(b.distSq));

        // Take the first k points
        List<GridPoint> result = new List<GridPoint>(k);
        for (int i = 0; i < k && i < distList.Count; i++)
        {
            result.Add(distList[i].gp);
        }
        return result;
    }

    /// <summary>
    /// Performs inverse-distance weighting to get a single normalized (x,y)
    /// from multiple neighbor points. If distance=0 for any neighbor, we short-circuit to that point.
    /// </summary>
    private Vector2 InverseDistanceWeightedNormalized(float lat, float lon, List<GridPoint> neighbors, float power)
    {
        float sumWeights = 0f;
        float sumX = 0f;
        float sumY = 0f;

        foreach (GridPoint gp in neighbors)
        {
            float dx = gp.lat - lat;
            float dy = gp.lon - lon;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist < 1e-9f)
            {
                // Exactly on a grid point, short-circuit
                return new Vector2(gp.normalizedX, gp.normalizedY);
            }

            // weight = 1 / dist^power
            float w = 1f / Mathf.Pow(dist, power);
            sumWeights += w;
            sumX += w * gp.normalizedX;
            sumY += w * gp.normalizedY;
        }

        if (sumWeights < 1e-12f)
        {
            // fallback if something weird happens
            return new Vector2(neighbors[0].normalizedX, neighbors[0].normalizedY);
        }

        float outX = sumX / sumWeights;
        float outY = sumY / sumWeights;
        return new Vector2(outX, outY);
    }

    /// <summary>
    /// If you want to see all grid points, we skip some to avoid clutter.
    /// </summary>
    private void PlaceAllGridMarkers()
    {
        if (mapData == null || mapData.grid == null) return;

        Debug.Log($"Placing grid markers for {mapData.grid.Length} points (skip={gridStep})...");
        for (int i = 0; i < mapData.grid.Length; i += gridStep)
        {
            GridPoint gp = mapData.grid[i];
            Vector2 unityPos = ConvertNormalizedToUnity(gp.normalizedX, gp.normalizedY);
            InstantiateMarker(unityPos);
        }
    }

    /// <summary>
    /// Converts normalized coords [0..1] to Unity coords for your map.
    /// 0 => top/left, 1 => bottom/right.
    /// So x=0 => leftX, x=1 => rightX
    ///    y=0 => topY,  y=1 => bottomY
    /// </summary>
    private Vector2 ConvertNormalizedToUnity(float nx, float ny)
    {
        float x = Mathf.Lerp(leftX, rightX, nx);
        float y = Mathf.Lerp(topY, bottomY, ny);
        return new Vector2(x, y);
    }

    /// <summary>
    /// Instantiates a marker in mapContent, sets its anchoredPosition,
    /// and forces it to render on top by giving it a Canvas with a high sortingOrder.
    /// </summary>
    private void InstantiateMarker(Vector2 position)
    {
        if (markerPrefab == null || mapContent == null)
        {
            Debug.LogError("MarkerPrefab or mapContent not set!");
            return;
        }

        GameObject markerObj = Instantiate(markerPrefab, mapContent);
        markerObj.SetActive(true);

        RectTransform rt = markerObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = position;
        }
        else
        {
            markerObj.transform.position = new Vector3(position.x, position.y, 0f);
        }

        // Force marker on top
        Canvas markerCanvas = markerObj.GetComponent<Canvas>();
        if (markerCanvas == null) markerCanvas = markerObj.AddComponent<Canvas>();
        markerCanvas.overrideSorting = true;
        markerCanvas.sortingOrder = 999;

        Image img = markerObj.GetComponent<Image>();
        if (img != null) img.enabled = true;
    }

    // JSON data classes
    [System.Serializable]
    public class MapData
    {
        public GridPoint[] grid;
    }

    [System.Serializable]
    public class GridPoint
    {
        public float normalizedX;
        public float normalizedY;
        public float lat;
        public float lon;
    }
}
