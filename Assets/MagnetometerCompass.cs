using System.Collections;
using UnityEngine;
using TMPro;  // Import TextMeshPro
using System;
using System.Runtime.InteropServices;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
#endif

public class MagnetometerCompass : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    HL2ResearchMode researchMode;
#endif

    private float[] magSampleData = null;
    private Vector3 magneticField;

    public TextMeshProUGUI compassText;  // Assign in Inspector

    void Start()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode = new HL2ResearchMode();
        researchMode.InitializeMagSensor();
        researchMode.StartMagSensorLoop();
        compassText.text = "Heading";
#endif
    }

    void Update()
    {
#if ENABLE_WINMD_SUPPORT
        if (researchMode.MagSampleUpdated())
        {
            magSampleData = researchMode.GetMagSample();
            if (magSampleData.Length == 3)
            {
                magneticField = new Vector3(magSampleData[0], magSampleData[1], magSampleData[2]);

                // Calculate compass heading
                float heading = Mathf.Atan2(magneticField.x, magneticField.z) * Mathf.Rad2Deg;
                if (heading < 0) heading += 360;

                Debug.Log("Compass Heading: " + heading.ToString("F1") + "°");

                // Display heading on TextMeshPro
                if (compassText != null)
                {
                    compassText.text = "Compass Heading: " + heading.ToString("F1") + "°";
                }
            }
        }
#endif
    }

    public void StopSensors()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode.StopAllSensorDevice();
#endif
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) StopSensors();
    }
}


