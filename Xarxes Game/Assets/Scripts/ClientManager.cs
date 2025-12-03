using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.tvOS;
using System.Threading;
using static NetManager;

public class ClientManager : MonoBehaviour
{
    public int port = 9050;
    public string ip = "127.0.0.1";
    public IPEndPoint clientEndPoint;
    public IPEndPoint serverEndPoint;

    public int localNetId = 0;

    private Queue<byte[]> sendQueue = new Queue<byte[]>();

    public Thread clientThread;

    public PlayerNetwork localPlayer;

    public void Start()
    {
        clientThread = new Thread(new ThreadStart(ClientProcess));
    }

    public void OnDestroy()
    {
        if (clientThread != null && clientThread.IsAlive)
        {
            NetManager.instance.cancelReceive = true;
            clientThread.Join();
        }
    }

    public void ClientProcess()
    {

        while (!NetManager.instance.cancelReceive)
        {
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[4096];
            int receivedDataLength = NetManager.instance.clientSocket.ReceiveFrom(buffer, ref remoteEP);
            if (receivedDataLength > 0)
            {
                byte[] receivedData = new byte[receivedDataLength];
                NetManager.instance.OnPacketReceived(receivedData, receivedDataLength, remoteEP);
            }
        }
    }

    public void SendPacket(byte[] sendData, EndPoint serverIP)
    {
        NetManager.instance.clientSocket.SendTo(sendData, serverIP);
    }

    public void SendHelloMessage(int packetID)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetID);
            formatter.Serialize(ms, (byte)PacketType.Hello);

            byte[] packet = ms.ToArray();
            SendPacket(packet, serverEndPoint);
        }
    }

    public void WelcomeReceived(byte[] inputPacket, int receivedDataLength, int headerSize)
    {
        if (inputPacket == null || receivedDataLength == 0) return;

        try
        {
            using (var ms = new MemoryStream(inputPacket, headerSize, receivedDataLength - headerSize))
            {
                var formatter = new BinaryFormatter();

                int assignedNetId = (int)formatter.Deserialize(ms);
                int count = (int)formatter.Deserialize(ms);

                localNetId = assignedNetId;
                NetManager.instance.localNetID = localNetId;
                localPlayer.netID = localNetId;

                Debug.Log($"Welcome recibido. netId asignado={localNetId}, clientes={count}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error al deserializar WelcomePacket: " + ex);
        }
    }

    public void NewClientReceived(byte[] inputPacket, int receivedDataLength, int headerSize)
    {
        if (inputPacket == null || receivedDataLength == 0) return;
        try
        {
            using (var ms = new MemoryStream(inputPacket, headerSize, receivedDataLength - headerSize))
            {
                var formatter = new BinaryFormatter();
                string ip = (string)formatter.Deserialize(ms);
                int p = (int)formatter.Deserialize(ms);
                int netId = (int)formatter.Deserialize(ms);

                // Spawn new remote player
                NetManager.instance.InstantiateRemotePlayer(netId);

                Debug.Log($"Nuevo cliente conectado. netId={netId}, ip={ip}, port={p}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error al deserializar NewClientPacket: " + ex);
        }
    }

    public void PlayerInputReceived(byte[] inputPacket, int receivedDataLength, int headerSize)
    {
        if (inputPacket == null || receivedDataLength == 0) return;
        try
        {
            using (var ms = new MemoryStream(inputPacket, headerSize, receivedDataLength - headerSize))
            {
                var formatter = new BinaryFormatter();
                int netId = (int)formatter.Deserialize(ms);
                Vector3 position;
                position.x = (float)formatter.Deserialize(ms);
                position.y = (float)formatter.Deserialize(ms);
                position.z = (float)formatter.Deserialize(ms);
                Quaternion rotation;
                rotation.x = (float)formatter.Deserialize(ms);
                rotation.y = (float)formatter.Deserialize(ms);
                rotation.z = (float)formatter.Deserialize(ms);
                rotation.w = (float)formatter.Deserialize(ms);

                GameObject remotePlayer = NetManager.instance.GetNetworkObjectById(netId);
                if (remotePlayer != null)
                {
                    remotePlayer.GetComponent<PlayerNetwork>().ReceiveData(position, rotation);
                }

            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error al deserializar PlayerInputPacket: " + ex);
        }
    }


}
