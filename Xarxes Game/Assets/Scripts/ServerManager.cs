using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ServerManager : MonoBehaviour
{

    public int port = 9050;
    public string serverIP = "127.0.0.1";
    public IPEndPoint serverEndPoint;

    private Queue<byte[]> sendQueue = new Queue<byte[]>();

    public void ServerProcess()
    {

        while (!NetManager.instance.cancelReceive)
        {
            if (sendQueue.Count > 0)
            {
                byte[] sendData = sendQueue.Dequeue();
                NetManager.instance.clientSocket.SendTo(sendData, serverEndPoint);

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
    }

    public void DataToSend(byte[] sendData)
    {
        if (sendData != null && sendData.Length > 0)
        {
            sendQueue.Enqueue(sendData);
        }
    }
}
