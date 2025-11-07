using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.GraphicsBuffer;


public struct NetworkTransform
{
    public int id;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}


public class NetworkManager : MonoBehaviour
{

    public enum NetworkRole { Server, Client, Host }
    public NetworkRole role = NetworkRole.Host;

    public static NetworkManager instance;

    Thread serverThread;
    Thread clientThread;

    Thread serverDiscoveryThread;
    Thread clientDiscoveryThread;

    public List<NetworkObject> registeredObjects;
    private Dictionary<int, NetworkObject> objectsById = new Dictionary<int, NetworkObject>();
    private int nextObjectId = 0;
    private bool requestingId = false;
    public int assignedIdFromServer = -1;

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    public bool cancelReceive = false;
    public int port = 9050;

    public string serverIP = "127.0.0.1";

    public GameObject playerPrefab;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            registeredObjects = new List<NetworkObject>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Update()
    {
        // Aplicar id asignado por servidor en hilo principal (sin tocar transforms)
        if (assignedIdFromServer != -1)
        {
            int idToAssign = assignedIdFromServer;
            assignedIdFromServer = -1;

            var target = registeredObjects.FirstOrDefault(o => o.isLocalPlayer && o.id == 0);
            if (target != null)
            {
                AssignIdToLocalObject(target, idToAssign);
                Debug.Log($"[NetworkManager] Applied assigned id {idToAssign} to local object on main thread.");
            }
            else
            {
                Debug.LogWarning("[NetworkManager] Received assigned id but no local object without id found.");
            }
        }

        while (mainThreadActions.Count > 0)
        {
            try
            {
                var action = mainThreadActions.Dequeue();
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError("[NetworkManager] Error ejecutando acción en main thread: " + e);
            }
        }
    }

    public void StartServerDiscovery()
    {
        serverDiscoveryThread = new Thread(ServerDiscoveryProcess);
        serverDiscoveryThread.IsBackground = true;
        serverDiscoveryThread.Start();
    }

    public void InitiateManager()
    {
        if (role == NetworkRole.Server)
        {
            serverThread = new Thread(ServerProcess);
            serverThread.Start();
        }
        else if (role == NetworkRole.Client)
        {
            clientThread = new Thread(ClientProcess);
            clientThread.Start();
            InstantiateNewPlayer();
        }
        else if (role == NetworkRole.Host)
        {
            serverThread = new Thread(ServerProcess);
            clientThread = new Thread(ClientProcess);
            serverThread.Start();
            clientThread.Start();
            InstantiateNewPlayer();
        }
    }

    public void JoinAsHost()
    {
        role = NetworkRole.Host;
        StartServerDiscovery(); // Inicia el hilo de descubrimiento
        StartCoroutine(LoadSceneAndInitiate("FirstLevel1", true));
    }

    public void JoinAsClient()
    {
        role = NetworkRole.Client;
        DiscoverServer(ip =>
        {
            if (!string.IsNullOrEmpty(ip))
            {
                serverIP = ip;
                Debug.Log($"[NetworkManager] IP del servidor detectada: {serverIP}");
                StartCoroutine(LoadSceneAndInitiate("FirstLevel1", true));
            }
            else
            {
                Debug.LogError("[NetworkManager] No se encontró ningún servidor en la red local.");
            }
        });
    }

    private void ServerDiscoveryProcess()
    {
        using (Socket discoverySocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            discoverySocket.EnableBroadcast = true;
            discoverySocket.Bind(localEndPoint);

            byte[] buffer = new byte[1024];
            while (!cancelReceive)
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int length = 0;
                try
                {
                    length = discoverySocket.ReceiveFrom(buffer, ref sender);
                }
                catch (SocketException) { continue; }
                string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, length);

                if (msg == "DISCOVER_SERVER")
                {
                    string response = "SERVER_HERE";
                    byte[] respBytes = System.Text.Encoding.UTF8.GetBytes(response);
                    discoverySocket.SendTo(respBytes, sender);
                    Debug.Log($"[Discovery] Respondido a {sender}");
                }
            }
        }
    }

    public void DiscoverServer(Action<string> onServerFound)
    {
        clientDiscoveryThread = new Thread(() =>
        {
            using (Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                clientSocket.EnableBroadcast = true;
                IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, port);

                byte[] discoverMsg = System.Text.Encoding.UTF8.GetBytes("DISCOVER_SERVER");
                clientSocket.SendTo(discoverMsg, broadcastEP);

                byte[] buffer = new byte[1024];
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);

                clientSocket.ReceiveTimeout = 3000; // 3 segundos
                try
                {
                    int length = clientSocket.ReceiveFrom(buffer, ref sender);
                    string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
                    if (msg == "SERVER_HERE")
                    {
                        string serverIp = ((IPEndPoint)sender).Address.ToString();
                        Debug.Log($"[Discovery] Servidor encontrado en {serverIp}");
                        // Ejecutar en hilo principal
                        EnqueueMainThreadAction(() => onServerFound?.Invoke(serverIp));
                    }
                }
                catch (SocketException)
                {
                    Debug.LogWarning("[Discovery] No se encontró servidor.");
                    EnqueueMainThreadAction(() => onServerFound?.Invoke(null));
                }
            }
        });
        clientDiscoveryThread.IsBackground = true;
        clientDiscoveryThread.Start();
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

        
        InitiateManager();
    }

    public void OnDestroy()
    {
        cancelReceive = true;
        if (serverThread != null && serverThread.IsAlive)
            serverThread.Join();
        if (clientThread != null && clientThread.IsAlive)
            clientThread.Join();
        if (serverDiscoveryThread != null && serverDiscoveryThread.IsAlive)
            serverDiscoveryThread.Join();
        if (clientDiscoveryThread != null && clientDiscoveryThread.IsAlive)
            clientDiscoveryThread.Join();
    }


    public void InstantiateNewPlayer()
    {
        var playerObj = Instantiate(playerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
        var netObj = playerObj.GetComponent<NetworkObject>();
        netObj.isLocalPlayer = true;
        registeredObjects.Add(netObj);

        if (role == NetworkRole.Server || role == NetworkRole.Host)
        {
            int assignedId = AllocateNetId();
            AssignIdToLocalObject(netObj, assignedId);
            Debug.Log($"[NetworkManager] Nuevo jugador local (servidor/host) con id={assignedId}");
        }
        else if (role == NetworkRole.Client)
        {
            requestingId = true;
        }

    }

    // asigna id autoritativamente en servidor
    public int AllocateNetId()
    {
        return nextObjectId++;
    }

    // asignación en hilo principal
    public void AssignIdToLocalObject(NetworkObject obj, int assignedId)
    {
        // actualizar objeto y mapa
        obj.id = assignedId;
        objectsById[assignedId] = obj;
    }

    public void RequestNetID()
    {
        requestingId = true;
    }

    public static bool BufferIsText(byte[] buffer, int length, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        if (length != bytes.Length) return false; // exact match - evita confundir con paquetes binarios
        for (int i = 0; i < bytes.Length; i++)
            if (buffer[i] != bytes[i]) return false;
        return true;
    }

    private void EnqueueMainThreadAction(Action a)
    {
        mainThreadActions.Enqueue(a);
    }

    public void ServerProcess()
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
        try
        {
            serverSocket.Bind(localEndPoint);
            Debug.Log($"[Server] escuchando en {localEndPoint}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Server] Bind fallo: " + ex);
            return;
        }

        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                int receivedDataLength = serverSocket.ReceiveFrom(bufferData, 0, ref sender);
                if (receivedDataLength > 0)
                {
                    Debug.Log($"[Server] recibido {receivedDataLength} bytes desde {sender}");
                    
                    if (BufferIsText(bufferData, receivedDataLength, "REQUEST_ID"))
                    {
                        int newId = AllocateNetId();
                        string response = $"ASSIGN_ID:{newId}";
                        byte[] respBytes = System.Text.Encoding.UTF8.GetBytes(response);
                        serverSocket.SendTo(respBytes, sender);
                        Debug.Log($"[Server] asignado ID {newId} a {sender}");
                        continue; // no procesar transforms en este paquete
                    }

                    DeserializeData(bufferData, receivedDataLength);

                    byte[] transformData = SerializeData();
                    if (transformData != null && transformData.Length > 0)
                    {
                        serverSocket.SendTo(transformData, sender);
                        Debug.Log($"[Server] enviado {transformData.Length} bytes a {sender}");
                    }
                }
            }
            catch (SocketException se)
            {
                Debug.LogWarning($"[Server] SocketException: {se.SocketErrorCode} - {se.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Server] Exception: {e}");
            }

            Thread.Sleep(33);
        }
        serverSocket.Close();

    }

    public void ClientProcess()
    {
        try
        {
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Evitar que Windows convierta ICMP "port unreachable" en excepción que cierra el socket
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                clientSocket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Client] IOControl SIO_UDP_CONNRESET no disponible: " + ex.Message);
            }

            IPAddress ip;
            try
            {
                ip = IPAddress.Parse(serverIP);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Client] serverIP inválida: " + ex.Message);
                return;
            }
            IPEndPoint serverEndPoint = new IPEndPoint(ip, port);
            Debug.Log($"[Client] intentará conectar a {serverEndPoint}");

            byte[] bufferData = new byte[4096];

            while (!cancelReceive)
            {
                byte[] transformData = SerializeData();
                try
                {
                    if (transformData != null && transformData.Length > 0)
                    {
                        clientSocket.SendTo(transformData, serverEndPoint);
                        Debug.Log($"[Client] enviado {transformData.Length} bytes a {serverEndPoint}");
                    }

                    if (requestingId)
                    {
                        byte[] idRequestData = System.Text.Encoding.UTF8.GetBytes("REQUEST_ID");
                        clientSocket.SendTo(idRequestData, serverEndPoint);
                        requestingId = false;
                    }
                }
                catch (SocketException se)
                {
                    Debug.LogWarning("[Client] SendTo fallo: " + se.Message);
                }

                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    int receivedDataLength = clientSocket.ReceiveFrom(bufferData, 0, ref sender);
                    if (receivedDataLength > 0)
                    {
                        Debug.Log($"[Client] recibido {receivedDataLength} bytes desde {sender}");

                        // Comprobar si es un mensaje ASSIGN_ID:<id> (ASCII)
                        string possibleAssign = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                        if (possibleAssign.StartsWith("ASSIGN_ID:"))
                        {
                            var parts = possibleAssign.Split(':');
                            if (parts.Length == 2 && int.TryParse(parts[1], out int parsedId))
                            {
                                assignedIdFromServer = parsedId;
                                Debug.Log($"[Client] Recibido ASSIGN_ID:{parsedId}");
                            }
                            else
                            {
                                Debug.LogWarning("[Client] ASSIGN_ID formato inválido");
                            }
                        }
                        else
                        {
                            DeserializeData(bufferData, receivedDataLength);
                        }
                    }
                }
                catch (SocketException se)
                {
                    if (se.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        // Ignorar ICMP Port Unreachable (10054) y continuar
                        Debug.LogWarning("[Client] ICMP Port Unreachable recibido y ignorado.");
                    }
                    else
                    {
                        Debug.LogError("[Client] ReceiveFrom fallo: " + se);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Client] Exception en ReceiveFrom: " + ex);
                }

                Thread.Sleep(33);
            }
            clientSocket.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Client] ClientProcess fallo: " + ex);
        }
    }



    public byte[] SerializeData()
    {
        MemoryStream stream = new MemoryStream();
        BinaryFormatter formatter = new BinaryFormatter();
        try
        {

            List<NetworkObject> toSend = registeredObjects.Where(o => o.isLocalPlayer).ToList();

            // Logging para depuración
            if (toSend.Count == 0)
            {
                // enviamos 0 para que el receptor sepa que no hay transforms
                formatter.Serialize(stream, 0);
                Debug.Log("[SerializeData] no hay objetos locales para enviar.");
            }
            else
            {
                Debug.Log($"[SerializeData] enviando {toSend.Count} objetos. ids: {string.Join(",", toSend.Select(o => o.id.ToString()))}");
                formatter.Serialize(stream, toSend.Count);

                foreach (NetworkObject netObject in toSend)
                {
                    formatter.Serialize(stream, netObject.id);
                    formatter.Serialize(stream, netObject.netTransform.position.x);
                    formatter.Serialize(stream, netObject.netTransform.position.y);
                    formatter.Serialize(stream, netObject.netTransform.position.z);
                    formatter.Serialize(stream, netObject.netTransform.rotation.x);
                    formatter.Serialize(stream, netObject.netTransform.rotation.y);
                    formatter.Serialize(stream, netObject.netTransform.rotation.z);
                    formatter.Serialize(stream, netObject.netTransform.rotation.w);
                    formatter.Serialize(stream, netObject.netTransform.scale.x);
                    formatter.Serialize(stream, netObject.netTransform.scale.y);
                    formatter.Serialize(stream, netObject.netTransform.scale.z);
                }
            }

        }
        catch (SerializationException e)
        {
            Debug.Log("Serialization Failed : " + e.Message);
        }
        byte[] objectAsBytes = stream.ToArray();
        stream.Close();
        return objectAsBytes;
    }

    public void DeserializeData(byte[] objectAsBytes, int length)
    {

        MemoryStream stream = new MemoryStream(objectAsBytes, 0, length);
        BinaryFormatter formatter = new BinaryFormatter();
        try
        {

            int transformCount = (int)formatter.Deserialize(stream);
            Debug.Log($"[DeserializeData] recibidos {transformCount} transforms");

            for (int i = 0; i < transformCount; i++)
            {
                NetworkTransform transformDeserialized = new NetworkTransform();
                transformDeserialized.id = (int)formatter.Deserialize(stream);
                transformDeserialized.position.x = (float)formatter.Deserialize(stream);
                transformDeserialized.position.y = (float)formatter.Deserialize(stream);
                transformDeserialized.position.z = (float)formatter.Deserialize(stream);
                transformDeserialized.rotation.x = (float)formatter.Deserialize(stream);
                transformDeserialized.rotation.y = (float)formatter.Deserialize(stream);
                transformDeserialized.rotation.z = (float)formatter.Deserialize(stream);
                transformDeserialized.rotation.w = (float)formatter.Deserialize(stream);
                transformDeserialized.scale.x = (float)formatter.Deserialize(stream);
                transformDeserialized.scale.y = (float)formatter.Deserialize(stream);
                transformDeserialized.scale.z = (float)formatter.Deserialize(stream);

                Debug.Log($"[DeserializeData] transform id={transformDeserialized.id} pos={transformDeserialized.position}");

                NetworkObject target = null;
                target = registeredObjects.FirstOrDefault(o => o.id == transformDeserialized.id);

                if (target != null)
                {
                    if (!target.isLocalPlayer)
                    {
                        target.UpdateTransform(transformDeserialized.position, transformDeserialized.rotation, transformDeserialized.scale);
                    }
                }
                else
                {
                    var captured = transformDeserialized; // capture local copy
                    EnqueueMainThreadAction(() =>
                    {
                        if (playerPrefab == null)
                        {
                            Debug.LogError("[NetworkManager] playerPrefab no asignado — no se puede instanciar objeto remoto.");
                            return;
                        }

                        var go = Instantiate(playerPrefab, captured.position, captured.rotation);
                        var netObj = go.GetComponent<NetworkObject>();
                        if (netObj == null)
                        {
                            Debug.LogError("[NetworkManager] playerPrefab no contiene NetworkObject.");
                            Destroy(go);
                            return;
                        }

                        netObj.isLocalPlayer = false;
                        netObj.id = captured.id;
                        netObj.netTransform.position = captured.position;
                        netObj.netTransform.rotation = captured.rotation;
                        netObj.netTransform.scale = captured.scale;
                        netObj.targetPosition = captured.position;
                        netObj.targetRotation = captured.rotation;
                        netObj.targetScale = captured.scale;

                        // registrar localmente
                        registeredObjects.Add(netObj);
                        objectsById[captured.id] = netObj;

                        Debug.Log($"[NetworkManager] Instanciado objeto remoto para id={captured.id} en hilo principal.");
                    });
                }
            }

        }
        catch (SerializationException e)
        {
            Debug.Log("Deserialization Failed : " + e.Message);
        }
        stream.Close();
    }
}