using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;

public class TCPSocket : MonoBehaviour
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
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);
        serverSocket.Bind(localEndPoint);
        serverSocket.Listen(10);
        clientThread.Start();

        // Accept Connection
        Socket clientSocket = serverSocket.Accept();


        // Send Information
        string text = "Hello from server";
        byte[] data = new byte[1024];
        data = System.Text.Encoding.UTF8.GetBytes(text);
        clientSocket.Send(data);

        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {
            
            int receivedDataLength = clientSocket.Receive(bufferData);
            if (receivedDataLength > 0)
            {
                string receivedText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                Debug.Log("Received from client: " + receivedText);

                string ping = "Hello from server";
                byte[] dataPing = new byte[1024];
                dataPing = System.Text.Encoding.UTF8.GetBytes(ping);
                clientSocket.Send(dataPing);
            }
            Thread.Sleep(10);
        }

    }

    public void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);
        clientSocket.Connect(serverEndPoint);

        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {
            int receivedDataLength = clientSocket.Receive(bufferData);
            if (receivedDataLength > 0)
            {
                string receivedText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                Debug.Log("Received from server: " + receivedText);

                string replyText = "Hello from client";
                byte[] replyData = System.Text.Encoding.UTF8.GetBytes(replyText);
                clientSocket.Send(replyData);

            }

            Thread.Sleep(10);
        }
    }

    

}
