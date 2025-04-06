using UnityEngine;
using TMPro;

public class Compass : MonoBehaviour
{
    public TextMeshProUGUI HeadingText;

    private float[] magSampleData = null;

    public void UpdateMagnetometerSample(float[] magData)
    {
        if (magData?.Length == 3)
        {
            magSampleData = magData;

            
            Vector3 magDevice = new Vector3(magData[0], magData[1], magData[2]);

            
            Quaternion deviceRotation = Camera.main.transform.rotation;
            Vector3 magWorld = deviceRotation * magDevice;

            
            Vector3 flatMag = Vector3.ProjectOnPlane(magWorld, Vector3.up);

            
            float heading = Mathf.Atan2(flatMag.x, flatMag.z) * Mathf.Rad2Deg;
            if (heading < 0) heading += 360;

            
            HeadingText.text = $"Heading: {heading:F1}°";
        }
    }
}

