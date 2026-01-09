using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;


public class NetManager : MonoBehaviour
{

    public static NetManager instance;

    public int port = 9050;
    public string serverIP = "127.0.0.1";

    public bool cancelReceive = false;

    public float worldStateRate = 0.1f; 
    private float worldStateTimer = 0f;

    public ClientManager clientManager;
    public ServerManager serverManager;

    public Socket clientSocket;
    public Socket serverSocket;

    public List<ClientProxy> clientProxies = new List<ClientProxy>();
    public List<NetObj> networkObjects = new List<NetObj>();

    public GameObject localPlayerPrefab;
    public GameObject remotePlayerPrefab;

    public ReplicationManagerServer replicationServer = new ReplicationManagerServer();
    public ReplicationManagerClient replicationClient = new ReplicationManagerClient();

    public int nextNetID = 1;

    public int localNetID = 0;

    public float startTime = 0f;

    public AcknowledgementManager ackManager;


    public enum NetMode
    {
        Client,
        Server,
        Host
    }
    public NetMode mode = NetMode.Host;

    public enum PacketType : byte
    {
        Hello = 0,
        Welcome = 1,
        PlayerInput = 2,
        NewClient = 3,
        WorldState = 4,
        ModifyObstacle = 5,
        TimeSync = 6,
        DeltaTime = 7,
        Acknowledgement = 8
    }

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void OnDestroy()
    {
        cancelReceive = true;
        try
        {
            clientSocket?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error cerrando clientSocket en NetManager.OnDestroy: {ex.Message}");
        }

        try
        {
            serverSocket?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error cerrando serverSocket en NetManager.OnDestroy: {ex.Message}");
        }

        if (clientManager != null && clientManager.clientThread != null && clientManager.clientThread.IsAlive)
        {
            bool exited = clientManager.clientThread.Join(1000);
            if (!exited) Debug.LogWarning("clientThread no respondió al cierre dentro del timeout.");
        }

        if (serverManager != null && serverManager.serverThread != null && serverManager.serverThread.IsAlive)
        {
            bool exited = serverManager.serverThread.Join(1000);
            if (!exited) Debug.LogWarning("serverThread no respondió al cierre dentro del timeout.");
        }
    }

    public void JoinAsHost()
    {
        mode = NetMode.Host;
        StartCoroutine(LoadSceneAndInitiate("FirstLevel1", true));
    }

    public void JoinAsClient()
    {
        mode = NetMode.Client;
        StartCoroutine(LoadSceneAndInitiate("FirstLevel1", true));
    }

    public void InitializeLobby()
    {
        if (mode == NetMode.Server)
        {
            clientManager.gameObject.SetActive(false);
            startTime = (float)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            ServerProcess();
        }
        else if (mode == NetMode.Client)
        {
            serverManager.gameObject.SetActive(false);
            StartCoroutine(JoinAsClientCoroutine(2f));
        }
        else if (mode == NetMode.Host)
        {
            startTime = (float)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            ServerProcess();
            ClientProcess();
            localNetID = AssignNetID();
            InstantiateNewLocalPlayer();
        }
    }

    public System.Collections.IEnumerator LoadSceneAndInitiate(string sceneName, bool async)
    {



        if (async)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                yield break;
            }
            while (!op.isDone)
                yield return null;
        }
        else
        {
            try
            {
                SceneManager.LoadScene(sceneName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Excepción LoadScene('{sceneName}'): {e}");
                yield break;
            }
            yield return null;
        }


        InitializeLobby();
    }

    public void ServerProcess()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress address = IPAddress.Parse(serverIP);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
        serverSocket.Bind(endPoint);

        IPEndPoint localEP = (IPEndPoint)serverSocket.LocalEndPoint;

        if (serverManager != null)
        {
            serverManager.serverEndPoint = localEP;
            clientManager.serverEndPoint = localEP;
            serverManager.serverThread.Start();

        }
    }

    public void ClientProcess(int localPort = 0)
    {
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        if (localPort > 0)
            clientSocket.Bind(new IPEndPoint(IPAddress.Any, localPort));
        else
            clientSocket.Bind(new IPEndPoint(IPAddress.Any, 0));

        IPAddress address;
        try
        {
            address = IPAddress.Parse(serverIP);
        }
        catch (Exception)
        {
            // si serverIP no es válido, fallback a loopback
            address = IPAddress.Loopback;
        }
        IPEndPoint serverEndPoint = new IPEndPoint(address, port);

        if (clientManager != null)
        {
            clientManager.clientEndPoint = (IPEndPoint)clientSocket.LocalEndPoint;
            clientManager.serverEndPoint = serverEndPoint;
            clientManager.clientThread.Start();
        }
    }

    public IEnumerator JoinAsClientCoroutine(float timeoutSeconds = 2f)
    {
        // Solo aplicable si estamos en modo Cliente
        if (mode != NetMode.Client)
        {
            Debug.LogWarning("JoinAsClient sólo funciona en modo Client.");
            yield break;
        }

        byte[] helloPacket = BuildHelloPacket(1);

        UdpClient udp = null;
        udp = new UdpClient();
        udp.EnableBroadcast = true;
        // Bind al puerto ephemeral local para recibir respuesta
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, port);

        // Enviamos Hello por broadcast
        udp.Send(helloPacket, helloPacket.Length, broadcastEP);

        // configuramos timeout
        var async = udp.BeginReceive(null, null);
        float start = Time.time;
        bool received = false;
        byte[] receivedBytes = null;
        IPEndPoint remoteEP = null;

        while (Time.time - start < timeoutSeconds)
        {
            if (async.IsCompleted)
            {
                try
                {
                    receivedBytes = udp.EndReceive(async, ref remoteEP);
                    received = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Error recibiendo respuesta de discovery: " + ex.Message);
                    received = false;
                }
                break;
            }
            yield return null;
        }

        if (!received)
        {
            Debug.Log("No se encontraron hosts en la LAN.");
            yield break;
        }

        // Comprobamos que el paquete recibido es un Welcome
        try
        {
            int packetId;
            PacketType packetType;
            int headerSize;
            (packetId, packetType, headerSize) = DeserializePacketIdentification(receivedBytes);

            if (packetType != PacketType.Welcome)
            {
                Debug.LogWarning($"Respuesta recibida no es Welcome (tipo {packetType}). Ignorando.");
                yield break;
            }

            // Guardamos la IP del servidor descubierto y arrancamos el proceso cliente normal
            serverIP = remoteEP.Address.ToString();
            Debug.Log($"Host encontrado en {serverIP}:{remoteEP.Port} - iniciando ClientProcess");

            int discoveryLocalPort = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
            try { udp.Close(); } catch { }
            udp = null;

            // Iniciamos el proceso cliente para crear sockets y threads
            ClientProcess(discoveryLocalPort);

            // Reenviamos el paquete Welcome al ClientManager para que procese el registro
            if (clientManager != null)
            {
                clientManager.WelcomeReceived(receivedBytes, receivedBytes.Length, headerSize);
                SendAcknowledgment(packetId, remoteEP);
            }

            // Instanciamos jugador local
            InstantiateNewLocalPlayer();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error procesando paquete Welcome del host: " + ex.Message);
            yield break;
        }
        finally
        {
            if (udp != null)
            {
                try { udp.Close(); } catch { }
            }
        }
    }

    public GameObject GetNetworkObjectById(int netID)
    {
        foreach (var netObj in networkObjects)
        {
            if (netObj.netID == netID)
            {
                return netObj.gameObject;
            }
        }
        return null;
    }

    public void OnPacketReceived(byte[] inputPacket, int receivedDataLength, EndPoint fromAddress)
    {
        if(mode == NetMode.Server || mode == NetMode.Host)
        {
            int packetId;
            PacketType packetType;
            int headerSize;
            (packetId, packetType, headerSize) = DeserializePacketIdentification(inputPacket);

            

            if (packetType == PacketType.Hello)
            {
                Debug.Log("Paquete Hello recibido del cliente con IP: " + fromAddress.ToString());

                CreateNewClientProxy(fromAddress);

                
                foreach (var client in clientProxies)
                {
                    int packetIdForNewClient = ackManager.AssignPacketID();
                    byte[] newClientPacket = BuildNewClientPacket(packetIdForNewClient, clientProxies[clientProxies.Count - 1]);
                    serverManager.SendPacket(newClientPacket, client.GetEndPoint());
                    ackManager.AddPendingAcknowledgment(packetIdForNewClient, newClientPacket, client.GetEndPoint());
                }

            }
            else if (packetType == PacketType.PlayerInput)
            {
                clientManager.PlayerInputReceived(inputPacket, receivedDataLength, headerSize);
            }
            else if (packetType == PacketType.ModifyObstacle)
            {
                replicationServer.ObjectModifiedReceived(inputPacket, receivedDataLength, headerSize);
                SendAcknowledgment(packetId, fromAddress);
            }
            else if (packetType == PacketType.TimeSync)
            {
                foreach (var client in clientProxies)
                {
                    if (client.GetEndPoint().Equals(fromAddress))
                    {
                        client.CalculateDeltaTime(inputPacket, receivedDataLength, headerSize);
                        int packetIdForDeltaTime = ackManager.AssignPacketID();
                        byte[] deltaTimePacket = BuildDeltaTimePacket(packetIdForDeltaTime, client);
                        serverManager.SendPacket(deltaTimePacket, client.GetEndPoint());
                        ackManager.AddPendingAcknowledgment(packetIdForDeltaTime, deltaTimePacket, client.GetEndPoint());
                        SendAcknowledgment(packetId, fromAddress);
                    }
                }


            }
            else if (packetType == PacketType.Acknowledgement)
            {
                ackManager.RemovePendingAcknowledgment(packetId);
            }


        }
        else if (mode == NetMode.Client)
        {
            int packetId;
            PacketType packetType;
            int headerSize;
            (packetId, packetType, headerSize) = DeserializePacketIdentification(inputPacket);

            if (packetType == PacketType.NewClient)
            {
                clientManager.NewClientReceived(inputPacket, receivedDataLength, headerSize);
                SendAcknowledgment(packetId, fromAddress);
            }
            else if (packetType == PacketType.PlayerInput)
            {
                clientManager.PlayerInputReceived(inputPacket, receivedDataLength, headerSize);
            }
            else if (packetType == PacketType.WorldState)
            {
                replicationClient.ReadWorldState(inputPacket, receivedDataLength, headerSize);
            }
            else if (packetType == PacketType.DeltaTime)
            {
                clientManager.DeltaTimeReceived(inputPacket, receivedDataLength, headerSize);
                SendAcknowledgment(packetId, fromAddress);
            }
            else if (packetType == PacketType.Acknowledgement)
            {
                ackManager.RemovePendingAcknowledgment(packetId);
            }

        }
    }

    public void CreateNewClientProxy(EndPoint fromAddress)
    {
        var remoteEP = fromAddress as IPEndPoint;
        string ipString = remoteEP.Address.ToString();
        int port = remoteEP.Port;
        int assignedNetID = AssignNetID();
        ClientProxy newClient = new ClientProxy(ipString, port, assignedNetID);
        newClient.startTime = (float)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        clientProxies.Add(newClient);
        InstantiateRemotePlayer(assignedNetID);

        var clientsForPacket = new List<ClientProxy>(clientProxies);

        if (mode == NetMode.Host && clientManager != null && clientManager.clientEndPoint != null)
        {
            try
            {
                var hostAddr = clientManager.clientEndPoint;
                // Añadimos el proxy del host para que los nuevos clientes creen el remote player del host
                var hostProxy = new ClientProxy(hostAddr.Address.ToString(), hostAddr.Port, localNetID);
                clientsForPacket.Add(hostProxy);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"No se pudo añadir proxy del host al WelcomePacket: {ex.Message}");
            }
        }

        int packetId = ackManager.AssignPacketID();
        byte[] welcomePacket = BuildWelcomePacket(packetId, assignedNetID, clientsForPacket);
        serverManager.SendPacket(welcomePacket, newClient.GetEndPoint());
        ackManager.AddPendingAcknowledgment(packetId, welcomePacket, newClient.GetEndPoint());
    }

    

    public void InstantiateRemotePlayer(int netID)
    {
        var remotePlayer = Instantiate(remotePlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
        var netObj = remotePlayer.GetComponent<PlayerNetwork>();
        netObj.netID = netID;
        netObj.isLocalPlayer = false;
        networkObjects.Add(netObj);
    }

    public void InstantiateNewLocalPlayer()
    {
        var localPlayer = Instantiate(localPlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
        var netObj = localPlayer.GetComponent<PlayerNetwork>();
        netObj.netID = localNetID;
        netObj.isLocalPlayer = true;
        networkObjects.Add(netObj);
        clientManager.localPlayer = netObj;
        
            
    }

    public void SyncNetworkObjectsInScene()
    {
        foreach (var netObj in networkObjects)
        {
            netObj.SyncWithServer(startTime, clientManager.deltaTime);
        }
    }

    public byte[] BuildHelloPacket(int packetId)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.Hello);
            return ms.ToArray();
        }
    }

    public byte[] BuildWelcomePacket(int packetId, int assignedNetId, List<ClientProxy> existingClients)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.Welcome);
            formatter.Serialize(ms, assignedNetId);
            formatter.Serialize(ms, existingClients.Count);
            foreach (var c in existingClients)
            {
                formatter.Serialize(ms, c.ip);
                formatter.Serialize(ms, c.port);
                formatter.Serialize(ms, c.netId);
            }
            return ms.ToArray();
        }
    }

    public byte[] BuildNewClientPacket(int packetId, ClientProxy newClient)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.NewClient);
            formatter.Serialize(ms, newClient.ip);
            formatter.Serialize(ms, newClient.port);
            formatter.Serialize(ms, newClient.netId);
            return ms.ToArray();
        }
    }

    public byte[] BuildDeltaTimePacket(int packetId, ClientProxy clientProxy)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.DeltaTime);
            formatter.Serialize(ms, clientProxy.deltaTime);
            formatter.Serialize(ms, startTime);
            return ms.ToArray();
        }
    }

    public byte[] BuildAcknowledgementPacket(int packetId)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.Acknowledgement);
            return ms.ToArray();
        }
    }

    public void SendAcknowledgment(int packetId, EndPoint address)
    {
        byte[] ackPacket = BuildAcknowledgementPacket(packetId);
        if (mode == NetMode.Server || mode == NetMode.Host)
        {
            serverManager.SendPacket(ackPacket, address);
        }
        else if (mode == NetMode.Client)
        {
            clientManager.SendPacket(ackPacket, address);
        }
    }

    public (int packetId, PacketType packetType, int headerSize) DeserializePacketIdentification(byte[] packet)
    {
        if (packet == null || packet.Length == 0)
            throw new ArgumentException("El paquete está vacío o es nulo.");

        using (var ms = new MemoryStream(packet))
        {
            var formatter = new BinaryFormatter();

            int packetId = (int)formatter.Deserialize(ms);

            byte typeValue = (byte)formatter.Deserialize(ms);
            PacketType packetType = (PacketType)typeValue;

            int headerSize = (int)ms.Position;

            return (packetId, packetType, headerSize);
        }
    }

    

    public int AssignNetID()
    {
        return nextNetID++;
    }

    


}
