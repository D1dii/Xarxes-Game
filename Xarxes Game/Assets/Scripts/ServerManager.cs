using System.Net;
using UnityEngine;

public class ServerManager : MonoBehaviour
{

    public int port = 9050;
    public string serverIP = "127.0.0.1";
    public IPEndPoint serverEndPoint;

    public void ServerProcess()
    {

        while (!NetManager.instance.cancelReceive)
        {

        }
    }
}
