using UnityEngine;
using System.Net.Sockets;
using System.Net;

public class TCPSocket : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        ServerProcess();
    }

    // Update is called once per frame
    public void Update()
    {
        
    }

    public void ServerProcess()
    {
        Socket newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);
        newSocket.Bind(localEndPoint);
        newSocket.Listen(10);
    }
}
