using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using static NetManager;
using UnityEngine.UIElements;

public class TransformNetObj : NetObj
{
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private Quaternion targetRotation;
    [SerializeField] private bool isInitialized = false;
    [SerializeField] private bool hasReceivedData = false;
    [SerializeField] private bool hasSentData = false;
    [SerializeField] private float lerpSpeed = 10f;

    [SerializeField] private float sendDataTimer = 0f;
    [SerializeField] private float sendDataInterval = 0.25f;

    [SerializeField] private Vector3 currentPosition;
    [SerializeField] private Quaternion currentRotation;

    [SerializeField] private bool canSend = false;
    [SerializeField] private float sendCooldownTimer = 0f;
    [SerializeField] private float sendCooldown = 1f;

    public int ownerClientId = -1;

    void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        currentPosition = transform.position;
        currentRotation = transform.rotation;
        isInitialized = false;
    }



    public void Update()
    {
        if (!canSend)
        {
            sendCooldownTimer += Time.deltaTime;
            if (sendCooldownTimer >= sendCooldown)
            {
                canSend = true;
                sendCooldownTimer = 0f;
            }
        }

        

        if (NetManager.instance.mode == NetManager.NetMode.Client ||
           (NetManager.instance.mode == NetManager.NetMode.Host))
        {
            if (isInitialized)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
                

                if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
                {
                    transform.position = targetPosition;
                    transform.rotation = targetRotation;
                    currentPosition = targetPosition;
                    currentRotation = targetRotation;
                    isInitialized = false;
                    hasReceivedData = false;
                    
                }

            }
        }

        if (!canSend) return;

        sendDataTimer += Time.deltaTime;

        if (sendDataTimer >= sendDataInterval && ownerClientId == -1)
        {
            if (currentPosition != transform.position || currentRotation != transform.rotation)
            {
                SendModifyObject();
            }
            sendDataTimer = 0f;
            currentPosition = transform.position;
            currentRotation = transform.rotation;
        }




    }

    private void SendModifyObject()
    {
        
        if (NetManager.instance.mode == NetManager.NetMode.Client)
        {
            int packetId = AcknowledgementManager.instance.AssignPacketID();
            byte[] packet = BuildModifyObjectPacket(packetId);
            NetManager.instance.clientManager.SendPacket(packet, NetManager.instance.clientManager.serverEndPoint);
            AcknowledgementManager.instance.AddPendingAcknowledgment(packetId, packet, NetManager.instance.clientManager.serverEndPoint);
        }
        else if (NetManager.instance.mode == NetManager.NetMode.Host)
        {
            foreach (var client in NetManager.instance.clientProxies)
            {
                if (netID != client.netId)
                {
                    int packetId = AcknowledgementManager.instance.AssignPacketID();
                    byte[] packet = BuildModifyObjectPacket(packetId);
                    NetManager.instance.serverSocket.SendTo(packet, client.GetEndPoint());
                    AcknowledgementManager.instance.AddPendingAcknowledgment(packetId, packet, client.GetEndPoint());
                }
            }
        }
    }

    private byte[] BuildModifyObjectPacket(int packetId)
    {

        if (NetManager.instance.mode == NetManager.NetMode.Host)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, packetId);
                formatter.Serialize(ms, (byte)PacketType.WorldState);
                formatter.Serialize(ms, netID);
                formatter.Serialize(ms, transform.position.x);
                formatter.Serialize(ms, transform.position.y);
                formatter.Serialize(ms, transform.position.z);
                formatter.Serialize(ms, transform.rotation.x);
                formatter.Serialize(ms, transform.rotation.y);
                formatter.Serialize(ms, transform.rotation.z);
                formatter.Serialize(ms, transform.rotation.w);
                return ms.ToArray();
            }
        }
        else if (NetManager.instance.mode == NetManager.NetMode.Client)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, packetId);
                formatter.Serialize(ms, (byte)PacketType.ModifyObstacle);
                formatter.Serialize(ms, netID);
                formatter.Serialize(ms, NetManager.instance.clientManager.localNetId);
                formatter.Serialize(ms, transform.position.x);
                formatter.Serialize(ms, transform.position.y);
                formatter.Serialize(ms, transform.position.z);
                formatter.Serialize(ms, transform.rotation.x);
                formatter.Serialize(ms, transform.rotation.y);
                formatter.Serialize(ms, transform.rotation.z);
                formatter.Serialize(ms, transform.rotation.w);
                return ms.ToArray();
            }
        }
        return null;


    }

    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        targetPosition = pos;
        targetRotation = rot;
        isInitialized = true;
        hasReceivedData = true;
        
    }
}