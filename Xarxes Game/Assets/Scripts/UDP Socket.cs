using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;

public class UDPSocket : MonoBehaviour
{
    Thread serverThread;
    Thread clientThread;

    bool cancelReceive = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        clientThread = new Thread(ClientProcess);
        serverThread = new Thread(ServerProcess);
        serverThread.Start();
    }

    // Update is called once per frame
    public void Update()
    {

    }

    public void OnDestroy()
    {
        cancelReceive = true;
        serverThread.Join();
        clientThread.Join();
    }

    public void ServerProcess()
    {
        // Create Server Socket
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        EndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);
        serverSocket.Bind(localEndPoint);
        clientThread.Start();

        

        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {

            int receivedDataLength = serverSocket.ReceiveFrom(bufferData, 0, ref localEndPoint);
            if (receivedDataLength > 0)
            {
                string receivedText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                Debug.Log("Received from client: " + receivedText);

                string ping = "Hello from server";
                byte[] dataPing = new byte[1024];
                dataPing = System.Text.Encoding.UTF8.GetBytes(ping);
                serverSocket.SendTo(dataPing, localEndPoint);


            }
            Thread.Sleep(10);
        }

    }

    public void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);

        // Send Information
        string text = "Hello from Client";
        byte[] data = new byte[1024];
        data = System.Text.Encoding.UTF8.GetBytes(text);
        clientSocket.SendTo(data, serverEndPoint);

        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedDataLength = clientSocket.ReceiveFrom(bufferData, 0, ref sender);
            if (receivedDataLength > 0)
            {
                string receivedText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                Debug.Log("Received from server: " + receivedText);

                string replyText = "Hello from client";
                byte[] replyData = System.Text.Encoding.UTF8.GetBytes(replyText);
                clientSocket.SendTo(replyData, serverEndPoint);

            }

            Thread.Sleep(10);
        }
    }
}
