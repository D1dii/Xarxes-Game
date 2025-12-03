using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class PlayerTransformData
{
    public Vector3 position;
    public Quaternion rotation;
}

public class PlayerNetwork : NetObj
{

    public bool isLocalPlayer = false;

    public struct TargetData
    {
        public Vector3 position;
        public Quaternion rotation;
    }
    public TargetData targetTransform;

    public float sendDataTimer = 0f;
    public float sendDataInterval = 0.05f; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        
    }

    // Update is called once per frame
    public void Update()
    {

        sendDataTimer += Time.deltaTime;

        if (isLocalPlayer && sendDataTimer >= sendDataInterval)
        {
            SendData();
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetTransform.position, Time.deltaTime * 10);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetTransform.rotation, Time.deltaTime * 10);
        }
    }

    public void SendData()
    {
        PlayerTransformData data = new PlayerTransformData
        {
            position = transform.position,
            rotation = transform.rotation
        };

        int packetId = 1;

        byte[] packet = BuildPlayerInputPacket(packetId, data);

        if (NetManager.instance.mode == NetManager.NetMode.Client)
        {
            NetManager.instance.clientManager.SendPacket(packet, NetManager.instance.clientManager.serverEndPoint);
        }
        else if (NetManager.instance.mode == NetManager.NetMode.Host)
        {
            NetManager.instance.SendPlayerInputToProxies(packet);
        }
    }

    public void ReceiveData(Vector3 targetPosition, Quaternion targetRotation)
    {
        targetTransform.position = targetPosition;
        targetTransform.rotation = targetRotation;
    }

    public byte[] BuildPlayerInputPacket(int packetId, PlayerTransformData data)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId); 
            formatter.Serialize(ms, (byte)NetManager.PacketType.PlayerInput);
            formatter.Serialize(ms, netID);
            formatter.Serialize(ms, data.position.x);
            formatter.Serialize(ms, data.position.y);
            formatter.Serialize(ms, data.position.z);
            formatter.Serialize(ms, data.rotation.x);
            formatter.Serialize(ms, data.rotation.y);
            formatter.Serialize(ms, data.rotation.z);
            formatter.Serialize(ms, data.rotation.w);
            return ms.ToArray();
        }
    }
}
