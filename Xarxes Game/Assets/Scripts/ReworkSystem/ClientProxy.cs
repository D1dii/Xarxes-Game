using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class ClientProxy
{
    public string ip;
    public int port;
    public int netId;

    public float startTime;
    public float welcomeTime;
    public float endTime;
    public float deltaTime;

    public ClientProxy(string ip, int port, int netId)
    {
        this.ip = ip;
        this.port = port;
        this.netId = netId;
    }

    public void CalculateDeltaTime(byte[] inputPacket, int receivedDataLength, int headerSize)
    {
        if (inputPacket == null || receivedDataLength == 0) return;
        try
        {
            using (var ms = new MemoryStream(inputPacket, headerSize, receivedDataLength - headerSize))
            {
                var formatter = new BinaryFormatter();
                welcomeTime = (float)formatter.Deserialize(ms);
                endTime = (float)DateTime.UtcNow.Ticks;
            }

            float ticksPerSecond = (float)TimeSpan.TicksPerSecond;
            float startSec = startTime / ticksPerSecond;
            float welcomeSec = welcomeTime / ticksPerSecond;
            float endSec = endTime / ticksPerSecond;

            deltaTime = ((startSec + endSec) * 0.5f) - welcomeSec;


        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error al Calcular DeltaTime: " + ex);
        }
    }

    public IPEndPoint GetEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(ip), port);
    }



}
