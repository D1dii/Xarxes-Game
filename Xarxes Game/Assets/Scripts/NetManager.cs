using System.Net.Sockets;
using System.Net;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;


public class NetManager : MonoBehaviour
{

    public static NetManager instance;

    public int port = 9050;
    public string serverIP = "127.0.0.1";

    public bool cancelReceive = false;

    public ClientManager clientManager;
    public ServerManager serverManager;

    public Socket clientSocket;
    public Socket serverSocket;

    public List<ClientProxy> clientProxies = new List<ClientProxy>();
    public List<NetObj> networkObjects = new List<NetObj>();

    public GameObject localPlayerPrefab;
    public GameObject remotePlayerPrefab;

    public int nextNetID = 1;

    public int localNetID = 0;

    public enum NetMode
    {
        Client,
        Server,
        Host
    }
    public NetMode mode = NetMode.Host;

    public enum PacketType : byte
    {
        Hello = 0,
        Welcome = 1,
        PlayerInput = 2,
        NewClient = 3
    }

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void OnDestroy()
    {
        cancelReceive = true;
        try
        {
            clientSocket?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error cerrando clientSocket en NetManager.OnDestroy: {ex.Message}");
        }

        try
        {
            serverSocket?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error cerrando serverSocket en NetManager.OnDestroy: {ex.Message}");
        }

        if (clientManager != null && clientManager.clientThread != null && clientManager.clientThread.IsAlive)
        {
            bool exited = clientManager.clientThread.Join(1000);
            if (!exited) Debug.LogWarning("clientThread no respondió al cierre dentro del timeout.");
        }

        if (serverManager != null && serverManager.serverThread != null && serverManager.serverThread.IsAlive)
        {
            bool exited = serverManager.serverThread.Join(1000);
            if (!exited) Debug.LogWarning("serverThread no respondió al cierre dentro del timeout.");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        if (mode == NetMode.Server)
        {
            clientManager.gameObject.SetActive(false);
            ServerProcess();
        }
        else if (mode == NetMode.Client)
        {
            serverManager.gameObject.SetActive(false);
            ClientProcess();
            InstantiateNewLocalPlayer();
        }
        else if (mode == NetMode.Host)
        {
            ServerProcess();
            ClientProcess();
            localNetID = AssignNetID();
            InstantiateNewLocalPlayer();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ServerProcess()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress address = IPAddress.Parse(serverIP);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, port);
        serverSocket.Bind(endPoint);
        

        if (serverManager != null)
        {
            serverManager.serverEndPoint = endPoint;
            clientManager.serverEndPoint = endPoint;
            serverManager.serverThread.Start();

        }
    }

    public void ClientProcess()
    {
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        clientSocket.Bind(new IPEndPoint(IPAddress.Any, 0));

        IPAddress address = IPAddress.Parse(serverIP);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

        if (clientManager != null)
        {
            clientManager.clientEndPoint = (IPEndPoint)clientSocket.LocalEndPoint;
            clientManager.serverEndPoint = serverEndPoint;
            clientManager.clientThread.Start();
        }
    }

    public GameObject GetNetworkObjectById(int netID)
    {
        foreach (var netObj in networkObjects)
        {
            if (netObj.netID == netID)
            {
                return netObj.gameObject;
            }
        }
        return null;
    }

    public void OnPacketReceived(byte[] inputPacket, int receivedDataLength, EndPoint fromAddress)
    {
        if(mode == NetMode.Server || mode == NetMode.Host)
        {
            int packetId;
            PacketType packetType;
            int headerSize;
            (packetId, packetType, headerSize) = DeserializePacketIdentification(inputPacket);

            if (packetType == PacketType.Hello)
            {
                Debug.Log("Paquete Hello recibido del cliente con IP: " + fromAddress.ToString());

                CreateNewClientProxy(fromAddress);

                byte[] newClientPacket = BuildNewClientPacket(2, clientProxies[clientProxies.Count - 1]);
                foreach (var client in clientProxies)
                {
                    serverManager.SendPacket(newClientPacket, client.GetEndPoint());
                }

            }
            else if (packetType == PacketType.PlayerInput)
            {
                clientManager.PlayerInputReceived(inputPacket, receivedDataLength, headerSize);
            }
        }
        else if (mode == NetMode.Client)
        {
            int packetId;
            PacketType packetType;
            int headerSize;
            (packetId, packetType, headerSize) = DeserializePacketIdentification(inputPacket);

            if (packetType == PacketType.Welcome)
            {
                Debug.Log("Paquete Welcome recibido del servidor.");
                clientManager.WelcomeReceived(inputPacket, receivedDataLength, headerSize);
            }
            else if (packetType == PacketType.NewClient)
            {
                clientManager.NewClientReceived(inputPacket, receivedDataLength, headerSize);
            }
            else if (packetType == PacketType.PlayerInput)
            {
                clientManager.PlayerInputReceived(inputPacket, receivedDataLength, headerSize);
            }
        }
    }

    public void CreateNewClientProxy(EndPoint fromAddress)
    {
        var remoteEP = fromAddress as IPEndPoint;
        string ipString = remoteEP.Address.ToString();
        int port = remoteEP.Port;
        int assignedNetID = AssignNetID();
        ClientProxy newClient = new ClientProxy(ipString, port, assignedNetID);
        clientProxies.Add(newClient);
        InstantiateRemotePlayer(assignedNetID);

        var clientsForPacket = new List<ClientProxy>(clientProxies);

        if (mode == NetMode.Host && clientManager != null && clientManager.clientEndPoint != null)
        {
            try
            {
                var hostAddr = clientManager.clientEndPoint;
                // Añadimos el proxy del host para que los nuevos clientes creen el remote player del host
                var hostProxy = new ClientProxy(hostAddr.Address.ToString(), hostAddr.Port, localNetID);
                clientsForPacket.Add(hostProxy);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"No se pudo añadir proxy del host al WelcomePacket: {ex.Message}");
            }
        }

        byte[] welcomePacket = BuildWelcomePacket(1, assignedNetID, clientsForPacket);
        serverManager.SendPacket(welcomePacket, newClient.GetEndPoint());
    }

    public void SendPlayerInputToProxies(byte[] inputPacket)
    {
        foreach (var client in clientProxies)
        {
            serverManager.SendPacket(inputPacket, client.GetEndPoint());
        }
    }

    public void InstantiateRemotePlayer(int netID)
    {
        var remotePlayer = Instantiate(remotePlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
        var netObj = remotePlayer.GetComponent<PlayerNetwork>();
        netObj.netID = netID;
        netObj.isLocalPlayer = false;
        networkObjects.Add(netObj);
    }

    public void InstantiateNewLocalPlayer()
    {
        var localPlayer = Instantiate(localPlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
        var netObj = localPlayer.GetComponent<PlayerNetwork>();
        netObj.netID = localNetID;
        netObj.isLocalPlayer = true;
        networkObjects.Add(netObj);
        clientManager.localPlayer = netObj;
        if (mode == NetMode.Client)
        {
            clientManager.SendHelloMessage(1);
        }
            
    }

    public byte[] BuildWelcomePacket(int packetId, int assignedNetId, List<ClientProxy> existingClients)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.Welcome);
            formatter.Serialize(ms, assignedNetId);
            formatter.Serialize(ms, existingClients.Count);
            foreach (var c in existingClients)
            {
                formatter.Serialize(ms, c.ip);
                formatter.Serialize(ms, c.port);
                formatter.Serialize(ms, c.netId);
            }
            return ms.ToArray();
        }
    }

    public byte[] BuildNewClientPacket(int packetId, ClientProxy newClient)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.NewClient);
            formatter.Serialize(ms, newClient.ip);
            formatter.Serialize(ms, newClient.port);
            formatter.Serialize(ms, newClient.netId);
            return ms.ToArray();
        }
    }

    public (int packetId, PacketType packetType, int headerSize) DeserializePacketIdentification(byte[] packet)
    {
        if (packet == null || packet.Length == 0)
            throw new ArgumentException("El paquete está vacío o es nulo.");

        using (var ms = new MemoryStream(packet))
        {
            var formatter = new BinaryFormatter();

            int packetId = (int)formatter.Deserialize(ms);

            byte typeValue = (byte)formatter.Deserialize(ms);
            PacketType packetType = (PacketType)typeValue;

            int headerSize = (int)ms.Position;

            return (packetId, packetType, headerSize);
        }
    }

    public int AssignNetID()
    {
        return nextNetID++;
    }

}
