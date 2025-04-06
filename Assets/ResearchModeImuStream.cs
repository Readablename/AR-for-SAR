
//This script is taken from this github repo by petergu684
//https://github.com/petergu684/HoloLens2-ResearchMode-Unity/blob/master/UnitySample/Assets/Scripts/ResearchModeImuStream.cs

using System.Collections;
using UnityEngine;
using TMPro;

#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
#endif

public class ResearchModeImuStream : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    HL2ResearchMode researchMode;
#endif

    public Compass CompassUI;

    private float[] accelSampleData = null;
    private Vector3 accelVector;

    private float[] gyroSampleData = null;
    private Vector3 gyroEulerAngle;

    private float[] magSampleData = null;

    public TextMeshProUGUI AccelText = null;
    public TextMeshProUGUI GyroText = null;
    public TextMeshProUGUI MagText = null;

    void Start()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode = new HL2ResearchMode();
        researchMode.InitializeAccelSensor();
        researchMode.InitializeGyroSensor();
        researchMode.InitializeMagSensor();

        researchMode.StartAccelSensorLoop();
        researchMode.StartGyroSensorLoop();
        researchMode.StartMagSensorLoop();
#endif
    }

    void LateUpdate()
    {
#if ENABLE_WINMD_SUPPORT
        if (researchMode.AccelSampleUpdated())
        {
            accelSampleData = researchMode.GetAccelSample();
            if (accelSampleData.Length == 3)
            {
                AccelText.text = $"Accel : {accelSampleData[0]:F3}, {accelSampleData[1]:F3}, {accelSampleData[2]:F3}";
            }
        }

        if (researchMode.GyroSampleUpdated())
        {
            gyroSampleData = researchMode.GetGyroSample();
            if (gyroSampleData.Length == 3)
            {
                GyroText.text = $"Gyro  : {gyroSampleData[0]:F3}, {gyroSampleData[1]:F3}, {gyroSampleData[2]:F3}";
            }
        }

        if (researchMode.MagSampleUpdated())
        {
            magSampleData = researchMode.GetMagSample();
            if (magSampleData.Length == 3)
            {
                MagText.text = $"Mag   : {magSampleData[0]:F3}, {magSampleData[1]:F3}, {magSampleData[2]:F3}";
                CompassUI.UpdateMagnetometerSample(magSampleData);
            }
        }
#endif
        accelVector = CreateAccelVector(accelSampleData);
        gyroEulerAngle = CreateGyroEulerAngle(gyroSampleData);
    }

    private Vector3 CreateAccelVector(float[] accelSample)
    {
        if (accelSample?.Length == 3)
        {
            return new Vector3(
                accelSample[2],
                -accelSample[0],
                -accelSample[1]
            );
        }
        return Vector3.zero;
    }

    private Vector3 CreateGyroEulerAngle(float[] gyroSample)
    {
        if (gyroSample?.Length == 3)
        {
            return new Vector3(
                gyroSample[2],
                gyroSample[0],
                gyroSample[1]
            );
        }
        return Vector3.zero;
    }

    public void StopSensorsEvent()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode.StopAllSensorDevice();
#endif
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) StopSensorsEvent();
    }
}



