using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.tvOS;

public class ClientManager : MonoBehaviour
{
    public int port = 9050;
    public string ip = "127.0.0.1";
    public IPEndPoint clientEndPoint;

    private Queue<byte[]> sendQueue = new Queue<byte[]>();
    private Queue<byte[]> receiveQueue = new Queue<byte[]>();

    public void Update()
    {
        if (receiveQueue.Count > 0)
        {
            byte[] receivedData = receiveQueue.Dequeue();
            // Process the received data as needed
        }
    }

    public void ClientProcess()
    {

        while (!NetManager.instance.cancelReceive)
        {
            if (sendQueue.Count > 0)
            {
                byte[] sendData = sendQueue.Dequeue();
                NetManager.instance.clientSocket.SendTo(sendData, clientEndPoint);

                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] buffer = new byte[4096];
                int received = NetManager.instance.clientSocket.ReceiveFrom(buffer, ref remoteEP);
                if (received > 0)
                {
                    byte[] receivedData = new byte[received];
                    System.Buffer.BlockCopy(buffer, 0, receivedData, 0, received);
                    receiveQueue.Enqueue(receivedData);
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
