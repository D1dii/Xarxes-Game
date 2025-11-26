using UnityEngine;

public class ClientProxy
{
    string ip;
    int port;
    int netId;

    public ClientProxy(string ip, int port, int netId)
    {
        this.ip = ip;
        this.port = port;
        this.netId = netId;
    }

}
