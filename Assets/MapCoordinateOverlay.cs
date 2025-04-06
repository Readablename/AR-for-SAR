using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class MapCoordinateOverlay : MonoBehaviour
{
    // Singleton instance
    public static MapCoordinateOverlay Instance { get; private set; }

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

    [Header("GPS Data Source")]
    [SerializeField] private GPSSocketClient gpsClient;
    [Tooltip("Initial location until first GPS data arrives")]
    public float initialLat = 63.4f;
    public float initialLon = 10.4f;

    // The corners for top-left, top-right, bottom-left, bottom-right
    private float leftX = -48f;
    private float rightX = 16813f;
    private float topY = 48f;
    private float bottomY = -12276f;

    private MapData mapData;

    // Store the marker instance for real-time updates.
    private RectTransform currentMarkerRT;

    // Public property to expose the main marker
    public RectTransform CurrentMarker
    {
        get { return currentMarkerRT; }
    }

    private void Awake()
    {
        // Set up singleton instance.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // 1) Load grid from JSON
        LoadMapData();

        // 2) Optionally display all grid points
        if (showGrid)
        {
            PlaceAllGridMarkers();
        }

        // 3) Find GPS client if not assigned
        if (gpsClient == null)
        {
            gpsClient = FindObjectOfType<GPSSocketClient>();
            if (gpsClient == null)
            {
                Debug.LogError("GPSSocketClient not found! No GPS updates will occur.");
            }
            else
            {
                // Subscribe to GPS updates
                gpsClient.OnGPSDataUpdated += OnGPSDataReceived;
                Debug.Log("Successfully connected to GPSSocketClient");
            }
        }
        else
        {
            // Subscribe to GPS updates
            gpsClient.OnGPSDataUpdated += OnGPSDataReceived;
        }

        // 4) Create initial marker with default position
        CreateOrUpdateMarker(initialLat, initialLon);
        Debug.Log($"Placed initial marker at lat={initialLat}, lon={initialLon} - waiting for GPS data");
    }

    void OnDestroy()
    {
        // Unsubscribe from events when destroyed
        if (gpsClient != null)
        {
            gpsClient.OnGPSDataUpdated -= OnGPSDataReceived;
        }
    }

    private void OnGPSDataReceived(GPSData data)
    {
        if (!data.valid)
        {
            Debug.LogWarning("Received invalid GPS data");
            return;
        }

        // Update using GPS data
        CreateOrUpdateMarker(data.latitude, data.longitude);
        Debug.Log($"Updated marker with GPS data: Lat={data.latitude}, Lon={data.longitude}");
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
    /// Creates or updates the marker based on latitude and longitude.
    /// </summary>
    public void CreateOrUpdateMarker(float lat, float lon)
    {
        Vector2 unityPos = GetUnityPositionFromLatLon(lat, lon);

        // If the marker already exists, update its position.
        if (currentMarkerRT != null)
        {
            currentMarkerRT.anchoredPosition = unityPos;
        }
        else
        {
            // Instantiate the marker as a permanent object.
            GameObject markerObj = Instantiate(markerPrefab, mapContent);
            markerObj.SetActive(true);

            currentMarkerRT = markerObj.GetComponent<RectTransform>();
            if (currentMarkerRT != null)
            {
                currentMarkerRT.anchoredPosition = unityPos;
            }
            else
            {
                markerObj.transform.position = new Vector3(unityPos.x, unityPos.y, 0f);
            }

            // Force marker on top
            Canvas markerCanvas = markerObj.GetComponent<Canvas>();
            if (markerCanvas == null) markerCanvas = markerObj.AddComponent<Canvas>();
            markerCanvas.overrideSorting = true;
            markerCanvas.sortingOrder = 999;

            Image img = markerObj.GetComponent<Image>();
            if (img != null) img.enabled = true;
        }

        Debug.Log($"IDW => lat={lat}, lon={lon} => unity=({unityPos.x:F1},{unityPos.y:F1})");
    }

    /// <summary>
    /// Returns the Unity position from latitude and longitude using IDW interpolation.
    /// </summary>
    private Vector2 GetUnityPositionFromLatLon(float lat, float lon)
    {
        if (mapData == null || mapData.grid == null)
        {
            Debug.LogError("No grid data loaded!");
            return Vector2.zero;
        }

        // 1) Get the k nearest neighbors in lat/lon space
        List<GridPoint> neighbors = FindKClosestNeighbors(lat, lon, kNeighbors);

        // 2) Do IDW to find final normalized (x,y)
        Vector2 norm = InverseDistanceWeightedNormalized(lat, lon, neighbors, distancePower);

        // 3) Convert to Unity coords
        return ConvertNormalizedToUnity(norm.x, norm.y);
    }

    /// <summary>
    /// Finds the k nearest neighbors in lat/lon space from the grid.
    /// </summary>
    private List<GridPoint> FindKClosestNeighbors(float lat, float lon, int k)
    {
        List<(float distSq, GridPoint gp)> distList = new List<(float, GridPoint)>(mapData.grid.Length);
        foreach (GridPoint gp in mapData.grid)
        {
            float dx = gp.lat - lat;
            float dy = gp.lon - lon;
            float distSq = dx * dx + dy * dy;
            distList.Add((distSq, gp));
        }
        distList.Sort((a, b) => a.distSq.CompareTo(b.distSq));
        List<GridPoint> result = new List<GridPoint>(k);
        for (int i = 0; i < k && i < distList.Count; i++)
        {
            result.Add(distList[i].gp);
        }
        return result;
    }

    /// <summary>
    /// Performs inverse-distance weighting to get a normalized (x,y) from neighbor points.
    /// </summary>
    private Vector2 InverseDistanceWeightedNormalized(float lat, float lon, List<GridPoint> neighbors, float power)
    {
        float sumWeights = 0f, sumX = 0f, sumY = 0f;
        foreach (GridPoint gp in neighbors)
        {
            float dx = gp.lat - lat;
            float dy = gp.lon - lon;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-9f)
            {
                return new Vector2(gp.normalizedX, gp.normalizedY);
            }
            float w = 1f / Mathf.Pow(dist, power);
            sumWeights += w;
            sumX += w * gp.normalizedX;
            sumY += w * gp.normalizedY;
        }
        if (sumWeights < 1e-12f)
        {
            return new Vector2(neighbors[0].normalizedX, neighbors[0].normalizedY);
        }
        return new Vector2(sumX / sumWeights, sumY / sumWeights);
    }

    /// <summary>
    /// Converts normalized coordinates [0..1] to Unity coordinates.
    /// </summary>
    private Vector2 ConvertNormalizedToUnity(float nx, float ny)
    {
        float x = Mathf.Lerp(leftX, rightX, nx);
        float y = Mathf.Lerp(topY, bottomY, ny);
        return new Vector2(x, y);
    }

    /// <summary>
    /// Optionally places grid markers for visualization.
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
    /// Instantiates a marker at a given Unity coordinate.
    /// This is used for placing additional waypoint markers.
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
        Canvas markerCanvas = markerObj.GetComponent<Canvas>();
        if (markerCanvas == null) markerCanvas = markerObj.AddComponent<Canvas>();
        markerCanvas.overrideSorting = true;
        markerCanvas.sortingOrder = 999;
        Image img = markerObj.GetComponent<Image>();
        if (img != null) img.enabled = true;
    }

    // JSON data classes
    [Serializable]
    public class MapData
    {
        public GridPoint[] grid;
    }

    [Serializable]
    public class GridPoint
    {
        public float normalizedX;
        public float normalizedY;
        public float lat;
        public float lon;
    }
}

