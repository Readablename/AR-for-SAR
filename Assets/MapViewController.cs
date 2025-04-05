using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using UnityEngine;

public class MapViewController : MonoBehaviour
{
    [Header("Positioning")]
    [SerializeField] private float minDistance = 0.8f;
    [SerializeField] private float maxDistance = 1.0f;
    [SerializeField] private float maxViewDegrees = 10.0f;
    [SerializeField] private Vector3 positionOffset = new Vector3(0, -0.1f, 0);

    [Header("Movement Settings")]
    [SerializeField] private float moveLerpTime = 0.3f;
    [SerializeField] private float rotateLerpTime = 0.3f;
    [SerializeField] private float scaleLerpTime = 0.3f;

    // Reference to map assembler to avoid FindObjectOfType calls every frame
    private InteractiveMapAssembler mapAssembler;
    private Transform cameraTransform;
    private Canvas mapCanvas;
    private RadialView radialView;
    private SolverHandler solverHandler;

    void Start()
    {
        cameraTransform = Camera.main.transform;
        mapCanvas = GetComponent<Canvas>();

        // Get reference to the map assembler
        mapAssembler = FindObjectOfType<InteractiveMapAssembler>();

        // Configure solvers for positioning
        SetupSolvers();

        // Make sure we have the correct Canvas settings
        ConfigureCanvas();

        // Apply position offset - since RadialView doesn't have Offset property in your version
        transform.localPosition += positionOffset;

        // Subscribe to GPS updates
        var gpsClient = FindObjectOfType<GPSSocketClient>();
        if (gpsClient != null)
        {
            gpsClient.OnGPSDataUpdated += OnGPSDataReceived;
            Debug.Log("MapViewController subscribed to GPS updates");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events when destroyed
        var gpsClient = FindObjectOfType<GPSSocketClient>();
        if (gpsClient != null)
        {
            gpsClient.OnGPSDataUpdated -= OnGPSDataReceived;
        }
    }

    private void OnGPSDataReceived(GPSData data)
    {
        if (mapAssembler == null)
        {
            mapAssembler = FindObjectOfType<InteractiveMapAssembler>();
            if (mapAssembler == null) return;
        }

        // Only recenter if follow marker is already true
        // This way, when the user manually pans, followMarker gets set to false
        // and we don't override it until they press the recenter button
        if (mapAssembler.followMarker)
        {
            mapAssembler.RecenterMap();
        }
    }

    private void SetupSolvers()
    {
        // Add and configure SolverHandler if not already present
        solverHandler = gameObject.GetComponent<SolverHandler>();
        if (solverHandler == null)
            solverHandler = gameObject.AddComponent<SolverHandler>();

        solverHandler.TrackedTargetType = TrackedObjectType.Head;

        // Add and configure RadialView if not already present
        radialView = gameObject.GetComponent<RadialView>();
        if (radialView == null)
            radialView = gameObject.AddComponent<RadialView>();

        // Apply your specific settings
        radialView.MinDistance = minDistance;
        radialView.MaxDistance = maxDistance;
        radialView.MinViewDegrees = 0f;
        radialView.MaxViewDegrees = maxViewDegrees;

        // Movement time settings
        radialView.MoveLerpTime = moveLerpTime;
        radialView.RotateLerpTime = rotateLerpTime;
        radialView.ScaleLerpTime = scaleLerpTime;
    }

    private void ConfigureCanvas()
    {
        if (mapCanvas == null) return;

        // Ensure we have world space rendering for HoloLens
        mapCanvas.renderMode = RenderMode.WorldSpace;

        // Adjust canvas scale if needed
        if (transform.localScale.magnitude < 0.001f)
        {
            transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            Debug.Log("Adjusted canvas scale to be visible in world space");
        }
    }

    // You can call this method from a button press or other event
    // to adjust the distance from the user
    public void AdjustDistance(float newDistance)
    {
        if (radialView != null)
        {
            radialView.MinDistance = newDistance;
            radialView.MaxDistance = newDistance;
        }
    }
}