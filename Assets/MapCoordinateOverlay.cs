using UnityEngine;
using UnityEngine.UI;
using System;

public class MapCoordinateOverlay : MonoBehaviour
{
    [Header("Map & Marker References")]
    public RectTransform mapContent;
    public RectTransform markerPrefab;

    [Header("Map Corner Coordinates (Real World in Degrees)")]
    public Vector2 topLeftLatLon = new Vector2(63.46134f, 10.28052f);     // NW corner
    public Vector2 topRightLatLon = new Vector2(63.46134f, 10.64514f);    // NE corner
    public Vector2 bottomLeftLatLon = new Vector2(63.35814f, 10.28052f);  // SW corner
    public Vector2 bottomRightLatLon = new Vector2(63.35814f, 10.64514f); // SE corner

    [Header("Map Corner UI Positions")]
    public Vector2 topLeftAnchoredPos = new Vector2(-500f, 500f);
    public Vector2 topRightAnchoredPos = new Vector2(500f, 500f);
    public Vector2 bottomLeftAnchoredPos = new Vector2(-500f, -500f);
    public Vector2 bottomRightAnchoredPos = new Vector2(500f, -500f);

    [Header("Test Marker Settings")]
    public bool testMarkerOnStart = true;
    public float testMarkerLat = 63.41192f;
    public float testMarkerLon = 10.40953f;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool addTestGridMarkers = false;
    public int gridDensity = 5;

    private void Start()
    {
        if (addTestGridMarkers)
        {
            AddTestGrid();
        }

        if (testMarkerOnStart)
        {
            PlaceMarkerAtLatLon(testMarkerLat, testMarkerLon);
        }
    }

    public RectTransform PlaceMarkerAtLatLon(float lat, float lon)
    {
        Vector2 anchoredPos = LatLonToMapAnchoredPos(lat, lon);

        if (showDebugInfo)
        {
            Debug.Log($"Placing marker at Lat/Lon: ({lat}, {lon}) → UI pos: {anchoredPos}");
        }

        RectTransform marker = Instantiate(markerPrefab, mapContent);
        marker.anchoredPosition = anchoredPos;
        marker.SetAsLastSibling();

        Canvas markerCanvas = marker.GetComponent<Canvas>();
        if (markerCanvas == null)
        {
            markerCanvas = marker.gameObject.AddComponent<Canvas>();
        }
        markerCanvas.overrideSorting = true;
        markerCanvas.sortingOrder = 100;

        return marker;
    }

    /// <summary>
    /// Converts geographical coordinates to UI position using bilinear interpolation of all four corners.
    /// This handles non-uniform distortion in maps.
    /// </summary>
    private Vector2 LatLonToMapAnchoredPos(float lat, float lon)
    {
        // Normalize the input coordinates within the map's bounds (0-1 range)
        float latRange = topLeftLatLon.x - bottomLeftLatLon.x;
        float lonRange = topRightLatLon.y - topLeftLatLon.y;

        // Make sure we're handling the correct direction of latitude (higher is north)
        float latNorm = (lat - bottomLeftLatLon.x) / latRange;
        float lonNorm = (lon - topLeftLatLon.y) / lonRange;

        // Clamp values to ensure they're within the map bounds
        latNorm = Mathf.Clamp01(latNorm);
        lonNorm = Mathf.Clamp01(lonNorm);

        // Bilinear interpolation for both x and y positions
        Vector2 topInterpolated = Vector2.Lerp(topLeftAnchoredPos, topRightAnchoredPos, lonNorm);
        Vector2 bottomInterpolated = Vector2.Lerp(bottomLeftAnchoredPos, bottomRightAnchoredPos, lonNorm);

        // Final vertical interpolation between top and bottom
        Vector2 finalPos = Vector2.Lerp(bottomInterpolated, topInterpolated, latNorm);

        return finalPos;
    }

    /// <summary>
    /// Converts UI position to geographical coordinates using inverse bilinear interpolation
    /// </summary>
    public Vector2 MapAnchoredPosToLatLon(Vector2 anchoredPos)
    {
        // Find the horizontal lines for the y-coordinate
        float topDist = DistancePointToLine(anchoredPos, topLeftAnchoredPos, topRightAnchoredPos);
        float bottomDist = DistancePointToLine(anchoredPos, bottomLeftAnchoredPos, bottomRightAnchoredPos);
        float totalDist = topDist + bottomDist;

        // Vertical interpolation factor
        float latNorm = totalDist > 0 ? bottomDist / totalDist : 0;

        // Find the longitude using interpolation on the correct horizontal line
        Vector2 leftSidePos = Vector2.Lerp(bottomLeftAnchoredPos, topLeftAnchoredPos, latNorm);
        Vector2 rightSidePos = Vector2.Lerp(bottomRightAnchoredPos, topRightAnchoredPos, latNorm);

        float lonNorm = InverseLerp(leftSidePos, rightSidePos, anchoredPos);

        // Convert normalized values back to actual lat/lon
        float latRange = topLeftLatLon.x - bottomLeftLatLon.x;
        float lonRange = topRightLatLon.y - topLeftLatLon.y;

        float lat = bottomLeftLatLon.x + (latNorm * latRange);
        float lon = topLeftLatLon.y + (lonNorm * lonRange);

        return new Vector2(lat, lon);
    }

    // Helper function to find distance from a point to a line
    private float DistancePointToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float lineLength = line.magnitude;

        if (lineLength == 0f) return Vector2.Distance(point, lineStart);

        float projection = Vector2.Dot(point - lineStart, line) / (lineLength * lineLength);
        projection = Mathf.Clamp01(projection);

        Vector2 projectedPoint = lineStart + projection * line;
        return Vector2.Distance(point, projectedPoint);
    }

    // Helper function for inverse linear interpolation
    private float InverseLerp(Vector2 a, Vector2 b, Vector2 point)
    {
        Vector2 ab = b - a;
        Vector2 ap = point - a;

        float abDot = Vector2.Dot(ab, ab);
        if (abDot <= 0.0001f) return 0f;

        float t = Vector2.Dot(ap, ab) / abDot;
        return Mathf.Clamp01(t);
    }

    // Add test grid to verify mapping accuracy
    private void AddTestGrid()
    {
        for (int i = 0; i <= gridDensity; i++)
        {
            for (int j = 0; j <= gridDensity; j++)
            {
                float latNorm = (float)i / gridDensity;
                float lonNorm = (float)j / gridDensity;

                float latRange = topLeftLatLon.x - bottomLeftLatLon.x;
                float lonRange = topRightLatLon.y - topLeftLatLon.y;

                float lat = bottomLeftLatLon.x + (latNorm * latRange);
                float lon = topLeftLatLon.y + (lonNorm * lonRange);

                var marker = PlaceMarkerAtLatLon(lat, lon);
                marker.GetComponent<Image>().color = new Color(1f, 0.5f, 0.5f, 0.7f);
                marker.transform.localScale = Vector3.one * 0.5f;
            }
        }
    }
}