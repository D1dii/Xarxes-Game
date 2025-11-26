using System.Net.Sockets;
using System.Net;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;


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
        PlayerInput = 2
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ServerProcess()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress address = IPAddress.Parse(serverIP);
        IPEndPoint endPoint = new IPEndPoint(address, port);
        serverSocket.Bind(endPoint);

        if (serverManager != null)
        {
            serverManager.serverEndPoint = endPoint;
        }
    }

    public void ClientProcess()
    {
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress address = IPAddress.Parse(serverIP);
        IPEndPoint endPoint = new IPEndPoint(address, port);

        if (clientManager != null)
        {
            clientManager.clientEndPoint = endPoint;
        }
    }

    public void OnPacketReceived(byte[] inputPacket, int receivedDataLength, EndPoint fromAddress)
    {
        if(mode == NetMode.Server || mode == NetMode.Host)
        {
            int packetId;
            PacketType packetType;
            (packetId, packetType) = DeserializePacketIdentification(inputPacket);

            if (packetType == PacketType.Hello)
            {
                Debug.Log("Paquete Hello recibido del cliente con IP: " + fromAddress.ToString());

            }
            else if (packetType == PacketType.PlayerInput)
            {

            }
        }
        else if (mode == NetMode.Client)
        {
            int packetId;
            PacketType packetType;
            (packetId, packetType) = DeserializePacketIdentification(inputPacket);

            if (packetType == PacketType.Welcome)
            {
                Debug.Log("Paquete Welcome recibido del servidor.");
            }
            else if (packetType == PacketType.PlayerInput)
            {

            }
        }
    }

    public (int packetId, PacketType packetType) DeserializePacketIdentification(byte[] packet)
    {
        if (packet == null || packet.Length == 0)
            throw new ArgumentException("El paquete está vacío o es nulo.");

        using (var ms = new MemoryStream(packet))
        {
            var formatter = new BinaryFormatter();

            int packetId = (int)formatter.Deserialize(ms);

            byte typeValue = (byte)formatter.Deserialize(ms);
            PacketType packetType = (PacketType)typeValue;

            return (packetId, packetType);
        }
    }

    public void SpawnClientProxy(string clientIP, int netID, int port)
    {

    }
}
