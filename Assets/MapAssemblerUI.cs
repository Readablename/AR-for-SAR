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

    private Vector2 lastMousePosition;

    // Helper class for tile information.
    private class TileInfo
    {
        public Sprite sprite;
        // Original grid indices (from file names)
        public int origX;
        public int origY;
        // New sequential grid indices.
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

        // Parse grid indices from each sprite name (expects format "map_x_y").
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

        // Create sorted lists of unique grid values.
        List<int> sortedXs = new List<int>(uniqueXs);
        sortedXs.Sort();
        List<int> sortedYs = new List<int>(uniqueYs);
        sortedYs.Sort();

        // Reassign each tile a new sequential grid index based on the sorted order.
        foreach (TileInfo tile in tiles)
        {
            tile.gridX = sortedXs.IndexOf(tile.origX);
            tile.gridY = sortedYs.IndexOf(tile.origY);
        }

        int numColumns = sortedXs.Count;
        int numRows = sortedYs.Count;

        // Place each tile using our hard-coded layout rules.
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
                // Use top-left anchoring.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);

                float uiX = 0f;
                float uiY = 0f;

                bool isRightColumn = (tile.gridX == numColumns - 1);
                bool isBottomRow = (tile.gridY == numRows - 1);

                if (!isRightColumn && !isBottomRow)
                {
                    // Interior tile.
                    uiX = mapOffset.x + tile.gridX * interiorTileWidth;
                    uiY = mapOffset.y - tile.gridY * interiorTileHeight;
                }
                else if (isRightColumn && !isBottomRow)
                {
                    // Right column tile (non-corner).
                    uiX = mapOffset.x + (numColumns - 1) * interiorTileWidth;
                    uiY = mapOffset.y - tile.gridY * interiorTileHeight;
                }
                else if (!isRightColumn && isBottomRow)
                {
                    // Bottom row tile (non-corner).
                    uiX = mapOffset.x + tile.gridX * interiorTileWidth;
                    uiY = mapOffset.y - (numRows - 1) * interiorTileHeight;
                }
                else if (isRightColumn && isBottomRow)
                {
                    // Bottom-right (corner) tile needs to be scaled down by 0.5 in both X and Y.
                    float rightBoundary = mapOffset.x + (numColumns - 1) * interiorTileWidth + rightTileWidth;
                    float bottomBoundary = mapOffset.y - (numRows - 1) * interiorTileHeight - bottomTileHeight;

                    uiX = rightBoundary - (cornerTileWidth * 0.5f);
                    uiY = bottomBoundary + (cornerTileHeight * 0.5f);

                    rt.localScale = new Vector3(0.5f, 0.5f, 1f); // Scale down to fit correctly
                }


                rt.anchoredPosition = new Vector2(uiX, uiY);
                // Set the tile's size to its native sprite size.
                rt.sizeDelta = new Vector2(tile.sprite.rect.width, tile.sprite.rect.height);
            }
        }
    }

    private void Update()
    {
        HandlePanning();
        HandleZooming();
    }

    // Panning: drag with left mouse button.
    private void HandlePanning()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - lastMousePosition;
            mapContent.anchoredPosition += delta * panSpeed * Time.deltaTime;
            lastMousePosition = Input.mousePosition;
        }
    }

    // Zooming: use mouse scroll wheel.
    private void HandleZooming()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float currentScale = mapContent.localScale.x;
            float newScale = Mathf.Clamp(currentScale + scroll * zoomSpeed, minZoom, maxZoom);

            // Convert the mouse screen position to the parent's local coordinate system.
            RectTransform parentRect = mapContent.parent as RectTransform;
            Vector2 pointerLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, null, out pointerLocalPos);

            // Calculate the difference between the pointer position and the current map position.
            Vector2 diff = pointerLocalPos - mapContent.anchoredPosition;

            // Adjust the anchored position so the pointer remains over the same map point after scaling.
            mapContent.anchoredPosition = pointerLocalPos - diff * (newScale / currentScale);

            // Apply the new scale.
            mapContent.localScale = new Vector3(newScale, newScale, 1f);
        }
    }

}
