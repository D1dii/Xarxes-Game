using System.Collections.Generic;
using System.Net;
using UnityEngine;
using static NetManager;

public class AcknowledgementManager : MonoBehaviour
{

    public static AcknowledgementManager instance;

    public int currentPacketId = 0;
    public Dictionary<int, float> pendingAcknowledgment = new Dictionary<int, float>();
    public Dictionary<int, byte[]> sentPackets = new Dictionary<int, byte[]>();
    public Dictionary<int, EndPoint> packetDestinations = new Dictionary<int, EndPoint>();
    public float acknowledgmentTimeout = 2.0f; // seconds

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < currentPacketId; i++)
        {
            CheckAcknowledgmentTime(i);
        }
    }

    public void AddPendingAcknowledgment(int packetId, byte[] packetInfo, EndPoint destination)
    {
        if (!pendingAcknowledgment.ContainsKey(packetId))
        {
            pendingAcknowledgment[packetId] = Time.time;
            sentPackets[packetId] = packetInfo;
            packetDestinations[packetId] = destination;
        }
    }

    public void RemovePendingAcknowledgment(int packetId)
    {
        if (pendingAcknowledgment.ContainsKey(packetId))
        {
            pendingAcknowledgment.Remove(packetId);
            sentPackets.Remove(packetId);
        }
    }

    public void CheckAcknowledgmentTime(int packetId)
    {
        if (pendingAcknowledgment.ContainsKey(packetId))
        {
            float sentTime = pendingAcknowledgment[packetId];
            if (Time.time - sentTime > acknowledgmentTimeout)
            {
                Debug.LogWarning($"Paquete {packetId} no fue reconocido en el tiempo límite.");
                pendingAcknowledgment.Remove(packetId);
                ResendMissingPacket(packetId);
            }
        }
    }

    public void ResendMissingPacket(int packetId)
    {
        if (sentPackets.ContainsKey(packetId))
        {
            byte[] packetData = sentPackets[packetId];
            EndPoint toAddress = packetDestinations[packetId];
            if (NetManager.instance.mode == NetMode.Server || NetManager.instance.mode == NetMode.Host)
            {
                NetManager.instance.serverManager.SendPacket(packetData, toAddress);
            }
            else if (NetManager.instance.mode == NetMode.Client)
            {
                NetManager.instance.clientManager.SendPacket(packetData, toAddress);
            }

            pendingAcknowledgment[packetId] = Time.time;
        }
    }

    public int AssignPacketID()
    {
        return currentPacketId++;
    }
}
