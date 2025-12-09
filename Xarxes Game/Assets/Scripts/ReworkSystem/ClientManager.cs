using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.tvOS;
using static NetManager;

public class ClientManager : MonoBehaviour
{

    public static ClientManager instance;

    public int port = 9050;
    public string ip = "127.0.0.1";
    public IPEndPoint clientEndPoint;
    public IPEndPoint serverEndPoint;

    public int localNetId = 0;

    public Thread clientThread;

    public PlayerNetwork localPlayer;
    private struct ReceivedPacket
    {
        public byte[] data;
        public int length;
        public EndPoint from;
    }

    private readonly Queue<ReceivedPacket> receiveQueue = new Queue<ReceivedPacket>();


    public void Awake()
    {

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }

        clientThread = new Thread(new ThreadStart(ClientProcess));
        clientThread.IsBackground = true;
    }

    public void OnDestroy()
    {
        NetManager.instance.cancelReceive = true;

        try
        {
            NetManager.instance.clientSocket?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Warning cerrando socket en OnDestroy (client): {ex.Message}");
        }

        if (clientThread != null && clientThread.IsAlive)
        {
            bool exited = clientThread.Join(1000);
            if (!exited)
            {
                Debug.LogWarning("El hilo del cliente no respondió al cierre dentro del timeout.");
            }
        }
    }

    public void ClientProcess()
    {

        try
        {
            if (NetManager.instance?.clientSocket != null)
            {
                // No dependemos de ReceiveTimeout; usaremos Available para chequeos rápidos
                NetManager.instance.clientSocket.ReceiveTimeout = 20;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"No se pudo establecer ReceiveTimeout en client: {ex.Message}");
        }

        while (!NetManager.instance.cancelReceive)
        {
            Socket sock = NetManager.instance?.clientSocket;
            if (sock == null)
            {
                Thread.Sleep(100);
                continue;
            }

            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[4096];
            try
            {
                int available = 0;
                try
                {
                    available = sock.Available;
                }
                catch (SocketException)
                {
                    available = 0;
                }
                catch (ObjectDisposedException)
                {
                    if (NetManager.instance.cancelReceive) break;
                    available = 0;
                }

                if (available == 0)
                {
                    if (NetManager.instance.cancelReceive) break;
                    // pequeña espera para evitar busy-loop
                    Thread.Sleep(10);
                    continue;
                }

                int receivedDataLength = sock.ReceiveFrom(buffer, ref remoteEP);
                if (receivedDataLength > 0)
                {
                    byte[] receivedData = new byte[receivedDataLength];
                    Buffer.BlockCopy(buffer, 0, receivedData, 0, receivedDataLength);

                    var packet = new ReceivedPacket
                    {
                        data = receivedData,
                        length = receivedDataLength,
                        from = remoteEP
                    };

                    receiveQueue.Enqueue(packet);
                }
            }
            catch (SocketException sex)
            {
                if (sex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }

                if (NetManager.instance.cancelReceive) break;

                Debug.LogError($"SocketException en ClientProcess: {sex.Message}");
                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Excepción en ClientProcess: {ex}");
                if (NetManager.instance.cancelReceive) break;
                Thread.Sleep(10);
            }

            // pequeña espera para evitar uso excesivo de CPU
            Thread.Sleep(10);
        }
    }

    public void Update()
    {
        List<ReceivedPacket> pending = null;
        if (receiveQueue.Count > 0)
        {
            pending = new List<ReceivedPacket>(receiveQueue.Count);
            while (receiveQueue.Count > 0)
                pending.Add(receiveQueue.Dequeue()); 
        }

        if (pending != null)
        {
            foreach (var p in pending)
            {
                try
                {
                    NetManager.instance.OnPacketReceived(p.data, p.length, p.from);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Excepción procesando paquete en hilo principal (client): {ex}");
                }
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
                if (NetManager.instance != null)
                    NetManager.instance.localNetID = localNetId;

                if (localPlayer != null)
                {
                    localPlayer.netID = localNetId;
                }
                else
                {
                    // Log informativo: el jugador local se instanciará después y deberá tomar el netID desde NetManager.instance.localNetID
                    Debug.Log("Welcome recibido antes de instanciar jugador local. netId guardado y se aplicará al instanciar.");
                }

                Debug.Log($"Welcome recibido. netId asignado={localNetId}, clientes={count}");

                for (int i = 0; i < count; i++)
                {
                    string existingIp = (string)formatter.Deserialize(ms);
                    int existingPort = (int)formatter.Deserialize(ms);
                    int existingNetId = (int)formatter.Deserialize(ms);

                    // No instanciar el propio cliente
                    if (existingNetId != localNetId)
                    {
                        // Evitar duplicados si ya existe (por si acaso)
                        var existingObj = NetManager.instance.GetNetworkObjectById(existingNetId);
                        if (existingObj == null)
                        {
                            NetManager.instance.InstantiateRemotePlayer(existingNetId);
                        }
                    }

                    Debug.Log($"Cliente existente: netId={existingNetId}, ip={existingIp}, port={existingPort}");
                }
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
                if (netId != localNetId)
                {
                    NetManager.instance.InstantiateRemotePlayer(netId);
                }
                    

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

    

    public void SendModifyObstacle(int objectNetId, Vector3 newPosition, Quaternion newRotation)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, 0); 
            formatter.Serialize(ms, (byte)PacketType.ModifyObstacle); 

            
            formatter.Serialize(ms, objectNetId);

            formatter.Serialize(ms, newPosition.x); formatter.Serialize(ms, newPosition.y); formatter.Serialize(ms, newPosition.z);
            formatter.Serialize(ms, newRotation.x); formatter.Serialize(ms, newRotation.y); formatter.Serialize(ms, newRotation.z); formatter.Serialize(ms, newRotation.w);

            byte[] packet = ms.ToArray();
            SendPacket(packet, serverEndPoint);
        }
    }
}
