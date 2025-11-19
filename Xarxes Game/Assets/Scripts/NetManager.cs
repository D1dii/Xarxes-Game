using System.Net.Sockets;
using System.Net;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


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

    public void OnPacketReceive(byte[] inputPacket)
    {

    }
}
