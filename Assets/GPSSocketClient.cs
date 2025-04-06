using UnityEngine;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class GPSData
{
    public float latitude;
    public float longitude;
    public float altitude;
    public long timestamp;
    public bool valid;
}

public class GPSSocketClient : MonoBehaviour
{
    [SerializeField] private string serverIP = "10.24.9.128"; // Your Android's IP
    [SerializeField] private string serverPort = "8085";
    [SerializeField] private float connectionRetryInterval = 5.0f;

    public event Action<GPSData> OnGPSDataUpdated;

    public GPSData CurrentGPSData { get; private set; }
    public bool IsConnected { get; private set; }
    public DateTime LastUpdateTime { get; private set; }
    public string StatusMessage { get; private set; }

#if !UNITY_EDITOR && UNITY_WSA
    private Windows.Networking.Sockets.StreamSocket socket;
    private bool isConnecting = false;
#endif

    private void Start()
    {
        // Initialize data
        CurrentGPSData = new GPSData();
        IsConnected = false;
        StatusMessage = "Initializing...";

#if !UNITY_EDITOR && UNITY_WSA
        // Start connection process
        ConnectToServer();
#else
        Debug.LogWarning("GPS Socket Client only works on HoloLens, not in Unity Editor");
#endif
    }

    private void OnDestroy()
    {
#if !UNITY_EDITOR && UNITY_WSA
        DisconnectFromServer();
#endif
    }

#if !UNITY_EDITOR && UNITY_WSA
    private async void ConnectToServer()
    {
        if (isConnecting) return;
        
        isConnecting = true;
        StatusMessage = "Connecting...";
        Debug.Log($"Attempting to connect to GPS server at {serverIP}:{serverPort}");

        try
        {
            // Create and connect socket
            socket = new Windows.Networking.Sockets.StreamSocket();
            
            // Set timeout to avoid hanging
            socket.Control.KeepAlive = false;
            
            // Connect to the server
            var hostName = new Windows.Networking.HostName(serverIP);
            await socket.ConnectAsync(hostName, serverPort);
            
            IsConnected = true;
            StatusMessage = "Connected";
            Debug.Log("Connected to GPS server");
            
            // Start receiving data
            ReceiveData();
        }
        catch (Exception e)
        {
            IsConnected = false;
            StatusMessage = $"Connection failed: {e.Message}";
            Debug.LogError($"Failed to connect to GPS server: {e.Message}");
            
            // Try to reconnect after delay
            StartCoroutine(RetryConnection());
        }
        finally
        {
            isConnecting = false;
        }
    }

    private IEnumerator RetryConnection()
    {
        yield return new WaitForSeconds(connectionRetryInterval);
        ConnectToServer();
    }

    private async void ReceiveData()
    {
        try
        {
            using (var reader = new Windows.Storage.Streams.DataReader(socket.InputStream))
            {
                reader.InputStreamOptions = Windows.Storage.Streams.InputStreamOptions.Partial;
                
                while (IsConnected)
                {
                    // Read data from server
                    uint bytesRead = await reader.LoadAsync(8192); // Buffer size
                    
                    if (bytesRead > 0)
                    {
                        // Read the JSON data as string
                        string jsonData = reader.ReadString(bytesRead);
                        Debug.Log($"Received GPS data: {jsonData}");
                        
                        try
                        {
                            // Parse the JSON data
                            GPSData newData = JsonUtility.FromJson<GPSData>(jsonData);
                            
                            // Update stored data
                            CurrentGPSData = newData;
                            LastUpdateTime = DateTime.Now;
                            
                            // Trigger the event
                            OnGPSDataUpdated?.Invoke(CurrentGPSData);
                            
                            Debug.Log($"GPS Updated: Lat={CurrentGPSData.latitude}, Lon={CurrentGPSData.longitude}, Alt={CurrentGPSData.altitude}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to parse GPS data: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Connection closed
                        Debug.Log("Connection closed by server");
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error receiving GPS data: {e.Message}");
        }
        
        // If we get here, connection is lost
        IsConnected = false;
        StatusMessage = "Disconnected";
        
        // Try to reconnect
        DisconnectFromServer();
        StartCoroutine(RetryConnection());
    }

    private void DisconnectFromServer()
    {
        if (socket != null)
        {
            socket.Dispose();
            socket = null;
        }
        
        IsConnected = false;
    }
#endif
}