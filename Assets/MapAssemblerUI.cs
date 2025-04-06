using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InteractiveMapAssembler : MonoBehaviour
{
    [Header("Tile Settings")]
    public GameObject tilePrefab;
    public RectTransform mapContent;
    // Overall UI offset for the assembled map.
    public Vector2 mapOffset = new Vector2(0f, 0f);

    // Expected tile dimensions (in pixels) for a 2x2 layout:
    private const float interiorTileWidth = 2048f;
    private const float interiorTileHeight = 2048f;
    private const float rightTileWidth = 480f;      // right column (non-corner) tile width
    private const float bottomTileHeight = 40f;       // bottom row (non-corner) tile height
    private const float cornerTileWidth = 960f;       // bottom-right corner tile width
    private const float cornerTileHeight = 80f;       // bottom-right corner tile height

    [Header("Panning and Zooming")]
    public float panSpeed = 50f;
    public float zoomSpeed = 0.1f;
    public float minZoom = 0.5f;
    public float maxZoom = 5f;

    // For mouse input.
    private Vector2 lastMousePosition;

    // For touch input.
    private Vector2[] lastTouchPositions = new Vector2[2];
    private bool wasPinching = false;

    [Header("Recenter Settings")]
    [Tooltip("The marker RectTransform will be automatically assigned from MapCoordinateOverlay.")]
    public RectTransform markerRectTransform;
    [Tooltip("If true, the map will continuously follow the marker until user input disables it.")]
    public bool followMarker = true;

    // --- Tile assembly code ---
    private class TileInfo
    {
        public Sprite sprite;
        public int origX;
        public int origY;
        public int gridX;
        public int gridY;
        public TileInfo(Sprite sprite, int origX, int origY)
        {
            this.sprite = sprite;
            this.origX = origX;
            this.origY = origY;
        }
    }

    private void Start()
    {
        // Load all tile sprites from Resources/map.
        Sprite[] tileSprites = Resources.LoadAll<Sprite>("map");
        if (tileSprites == null || tileSprites.Length == 0)
        {
            Debug.LogError("No tile sprites found in Resources/map!");
            return;
        }

        List<TileInfo> tiles = new List<TileInfo>();
        HashSet<int> uniqueXs = new HashSet<int>();
        HashSet<int> uniqueYs = new HashSet<int>();

        foreach (Sprite sprite in tileSprites)
        {
            string[] parts = sprite.name.Split('_');
            if (parts.Length < 3)
            {
                Debug.LogWarning("Invalid tile name: " + sprite.name);
                continue;
            }
            if (!int.TryParse(parts[1], out int origX) || !int.TryParse(parts[2], out int origY))
            {
                Debug.LogWarning("Invalid grid indices in: " + sprite.name);
                continue;
            }
            uniqueXs.Add(origX);
            uniqueYs.Add(origY);
            tiles.Add(new TileInfo(sprite, origX, origY));
        }

        List<int> sortedXs = new List<int>(uniqueXs);
        sortedXs.Sort();
        List<int> sortedYs = new List<int>(uniqueYs);
        sortedYs.Sort();

        foreach (TileInfo tile in tiles)
        {
            tile.gridX = sortedXs.IndexOf(tile.origX);
            tile.gridY = sortedYs.IndexOf(tile.origY);
        }

        int numColumns = sortedXs.Count;
        int numRows = sortedYs.Count;

        foreach (TileInfo tile in tiles)
        {
            GameObject tileGO = Instantiate(tilePrefab, mapContent);
            Image tileImage = tileGO.GetComponent<Image>();
            if (tileImage != null)
            {
                tileImage.sprite = tile.sprite;
                tileImage.sprite.texture.wrapMode = TextureWrapMode.Clamp;
                tileImage.sprite.texture.filterMode = FilterMode.Point;
            }

            RectTransform rt = tileGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Top-left anchoring.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);

                float uiX = 0f;
                float uiY = 0f;
                bool isRightColumn = (tile.gridX == numColumns - 1);
                bool isBottomRow = (tile.gridY == numRows - 1);

                if (!isRightColumn && !isBottomRow)
                {
                    uiX = mapOffset.x + tile.gridX * interiorTileWidth;
                    uiY = mapOffset.y - tile.gridY * interiorTileHeight;
                }
                else if (isRightColumn && !isBottomRow)
                {
                    uiX = mapOffset.x + (numColumns - 1) * interiorTileWidth;
                    uiY = mapOffset.y - tile.gridY * interiorTileHeight;
                }
                else if (!isRightColumn && isBottomRow)
                {
                    uiX = mapOffset.x + tile.gridX * interiorTileWidth;
                    uiY = mapOffset.y - (numRows - 1) * interiorTileHeight;
                }
                else if (isRightColumn && isBottomRow)
                {
                    float rightBoundary = mapOffset.x + (numColumns - 1) * interiorTileWidth + rightTileWidth;
                    float bottomBoundary = mapOffset.y - (numRows - 1) * interiorTileHeight - bottomTileHeight;
                    uiX = rightBoundary - (cornerTileWidth * 0.5f);
                    uiY = bottomBoundary + (cornerTileHeight * 0.5f);
                    rt.localScale = new Vector3(0.5f, 0.5f, 1f);
                }

                rt.anchoredPosition = new Vector2(uiX, uiY);
                rt.sizeDelta = new Vector2(tile.sprite.rect.width, tile.sprite.rect.height);
            }
        }

        // Retrieve the marker created by MapCoordinateOverlay from its singleton.
        if (MapCoordinateOverlay.Instance != null)
        {
            markerRectTransform = MapCoordinateOverlay.Instance.CurrentMarker;
        }
        else
        {
            Debug.LogWarning("MapCoordinateOverlay singleton instance not found!");
        }
    }

    private void Update()
    {
        // Handle both mouse and touch input.
        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleTouch();
        }
        else
        {
            HandleMousePanning();
            HandleMouseZooming();
        }

        // If followMarker is enabled, recenter the map every frame.
        // (If the user pans/zooms, we disable followMarker.)
        if (followMarker && markerRectTransform != null)
        {
            RecenterMap();
        }
    }

    // --- Mouse Input Handlers ---
    private void HandleMousePanning()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButton(0))
        {
            // User is panning manually; disable automatic follow.
            followMarker = false;

            Vector2 delta = (Vector2)Input.mousePosition - lastMousePosition;
            mapContent.anchoredPosition += delta * panSpeed * Time.deltaTime;
            lastMousePosition = Input.mousePosition;
        }
    }

    private void HandleMouseZooming()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // User is zooming manually; disable automatic follow.
            followMarker = false;

            float currentScale = mapContent.localScale.x;
            float newScale = Mathf.Clamp(currentScale + scroll * zoomSpeed, minZoom, maxZoom);

            RectTransform parentRect = mapContent.parent as RectTransform;
            Vector2 pointerLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, null, out pointerLocalPos);

            Vector2 diff = pointerLocalPos - mapContent.anchoredPosition;
            mapContent.anchoredPosition = pointerLocalPos - diff * (newScale / currentScale);
            mapContent.localScale = new Vector3(newScale, newScale, 1f);
        }
    }

    // --- Touch Input Handler ---
    private void HandleTouch()
    {
        if (Input.touchCount == 1)
        {
            // Single-finger drag panning.
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                // User is panning manually; disable automatic follow.
                followMarker = false;
                mapContent.anchoredPosition += touch.deltaPosition * panSpeed * Time.deltaTime;
            }
        }
        else if (Input.touchCount >= 2)
        {
            // Two-finger pinch zooming.
            // User is zooming manually; disable automatic follow.
            followMarker = false;

            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
            float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
            float touchDeltaMag = (touch0.position - touch1.position).magnitude;
            float deltaMagnitudeDiff = touchDeltaMag - prevTouchDeltaMag;

            float currentScale = mapContent.localScale.x;
            float newScale = Mathf.Clamp(currentScale + deltaMagnitudeDiff * zoomSpeed * Time.deltaTime, minZoom, maxZoom);

            // Use the midpoint of the touches as the zoom focus.
            Vector2 midPoint = (touch0.position + touch1.position) * 0.5f;
            RectTransform parentRect = mapContent.parent as RectTransform;
            Vector2 pointerLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, midPoint, null, out pointerLocalPos);
            Vector2 diff = pointerLocalPos - mapContent.anchoredPosition;
            mapContent.anchoredPosition = pointerLocalPos - diff * (newScale / currentScale);
            mapContent.localScale = new Vector3(newScale, newScale, 1f);
        }
    }

    /// <summary>
    /// Recenter the map so that the marker appears exactly at the center of the mapContent's parent.
    /// </summary>
    public void RecenterMap()
    {
        if (markerRectTransform == null)
        {
            Debug.LogWarning("Marker RectTransform not assigned for recentering!");
            return;
        }

        RectTransform parentRect = mapContent.parent as RectTransform;
        if (parentRect == null)
        {
            Debug.LogWarning("mapContent does not have a RectTransform parent!");
            return;
        }

        // Calculate the center of the viewport relative to its pivot
        Vector2 viewportCenter = new Vector2(
            parentRect.rect.width * (0.5f - parentRect.pivot.x),
            parentRect.rect.height * (0.5f - parentRect.pivot.y)
        );

        // Calculate the position of the marker in the map's local space, adjusted for scale
        Vector2 markerPosInMap = new Vector2(
            markerRectTransform.anchoredPosition.x * mapContent.localScale.x,
            markerRectTransform.anchoredPosition.y * mapContent.localScale.y
        );

        // Set the map's position so the marker is at the viewport's center
        mapContent.anchoredPosition = viewportCenter - markerPosInMap;
    }

    /// <summary>
    /// Call this method (for example via a UI button) to manually recenter the map and re-enable follow mode.
    /// </summary>
    public void RecenterMapButton()
    {
        // Re-enable follow mode and recenter once.
        followMarker = true;
        RecenterMap();
    }
}


