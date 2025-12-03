using System.Collections.Generic;
using System.Net;
using UnityEngine;
using System.Threading;

public class ServerManager : MonoBehaviour
{

    public int port = 9050;
    public string serverIP = "127.0.0.1";
    public IPEndPoint serverEndPoint;

    private Queue<byte[]> sendQueue = new Queue<byte[]>();

    public Thread serverThread;

    public void Start()
    {
        serverThread = new Thread(new ThreadStart(ServerProcess));
    }

    public void OnDestroy()
    {
        if (serverThread != null && serverThread.IsAlive)
        {
            NetManager.instance.cancelReceive = true;
            serverThread.Join();
        }
    }

    public void ServerProcess()
    {

        while (!NetManager.instance.cancelReceive)
        {

            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[4096];
            int receivedDataLength = NetManager.instance.serverSocket.ReceiveFrom(buffer, ref remoteEP);
            if (receivedDataLength > 0)
            {
                byte[] receivedData = new byte[receivedDataLength];
                NetManager.instance.OnPacketReceived(receivedData, receivedDataLength, remoteEP);
            }
        }
    }

    public void SendPacket(byte[] sendData, EndPoint clientIP)
    {
        NetManager.instance.serverSocket.SendTo(sendData, clientIP);
    }
}
