using System.Net;
using UnityEngine;

public class ClientProxy
{
    public string ip;
    public int port;
    public int netId;

    public ClientProxy(string ip, int port, int netId)
    {
        this.ip = ip;
        this.port = port;
        this.netId = netId;
    }

    public IPEndPoint GetEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(ip), port);
    }



}
