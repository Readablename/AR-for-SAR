using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

[RequireComponent(typeof(ObjectManipulator))]
public class MapMRTKHelper : MonoBehaviour
{
    private InteractiveMapAssembler mapAssembler;
    private ObjectManipulator objectManipulator;

    void Start()
    {
        // Find the map assembler
        mapAssembler = FindObjectOfType<InteractiveMapAssembler>();

        // Get reference to the object manipulator
        objectManipulator = GetComponent<ObjectManipulator>();

        // Subscribe to manipulation events
        if (objectManipulator != null && mapAssembler != null)
        {
            objectManipulator.OnManipulationStarted.AddListener((eventData) => {
                mapAssembler.followMarker = false;
                Debug.Log("MRTK manipulation started - followMarker disabled");
            });
        }
    }
}