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

    // Puerto separado para discovery (evita conflicto con el puerto de juego)
    public int discoveryPort = 9051;

    public string serverIP = "127.0.0.1";

    public GameObject playerPrefab;

    // Máximo payload UDP IPv4 (65535 - IP header - UDP header)
    private const int MaxUdpPacketSize = 65507;

    // --- NUEVO: gestionar clientes conocidos (endpoints) en el servidor para poder broadcast
    private readonly HashSet<EndPoint> connectedClients = new HashSet<EndPoint>();
    private readonly object clientsLock = new object();

    // --- NUEVO: petición de ownership desde cliente
    // Cuando quieras pedir ownership desde tu código de "agarrar", llama NetworkManager.instance.RequestOwnership(objectId)
    public int requestingOwnershipId = -1;

    // --- NUEVO: petición de release desde cliente
    public int requestingReleaseId = -1;

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

            // ahora usamos sentinel -1 para objetos sin id asignado
            var target = registeredObjects.FirstOrDefault(o => o.isLocalPlayer && o.id == -1);
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
        // Registrar objetos de la escena en todos los roles para evitar "ghosts" en cliente
        SearchNetworkObjectsOnScene();

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

    public void SearchNetworkObjectsOnScene()
    {
        // Asegurarse de tener la lista inicializada
        if (registeredObjects == null)
            registeredObjects = new List<NetworkObject>();

        // Buscar todos los NetworkObject activos en la escena
        var found = FindObjectsOfType<NetworkObject>();

        // Añadir nuevos encontrados sin duplicar y registrar ids existentes
        foreach (var netObj in found)
        {
            if (!registeredObjects.Contains(netObj))
            {
                registeredObjects.Add(netObj);
            }

            // Si ya tiene id válido, registrar en el diccionario
            if (netObj.id >= 0)
            {
                objectsById[netObj.id] = netObj;
            }
        }

        // Reconstruir conjunto de ids usados y ajustar nextObjectId para evitar colisiones
        var usedIds = new HashSet<int>(registeredObjects.Where(o => o.id >= 0).Select(o => o.id));
        if (usedIds.Count > 0)
        {
            int maxUsed = usedIds.Max();
            if (nextObjectId <= maxUsed)
                nextObjectId = maxUsed + 1;
        }

        // Si somos servidor o host, asignar ids a todos los objetos sin id
        if (role == NetworkRole.Server || role == NetworkRole.Host)
        {
            foreach (var obj in registeredObjects.Where(o => o.id < 0).ToList())
            {
                int assigned = AllocateNetId();
                AssignIdToLocalObject(obj, assigned);
                obj.isLocalPlayer = true; // marcar como local para que el servidor los envíe
                Debug.Log($"[NetworkManager] Assigned id {assigned} to scene object '{obj.gameObject.name}'");
            }
        }
        else
        {
            // Cliente: no asignamos ids autoritativos aquí; simplemente registramos objetos de la escena
            Debug.Log($"[NetworkManager] Cliente: registrados {registeredObjects.Count} NetworkObjects en escena; nextObjectId={nextObjectId}");
        }
    }

    public void JoinAsHost()
    {
        role = NetworkRole.Host;
        StartServerDiscovery();
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
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, discoveryPort);
            discoverySocket.EnableBroadcast = true;
            try
            {
                discoverySocket.Bind(localEndPoint);
                Debug.Log($"[Discovery] escuchando en {localEndPoint}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Discovery] Bind fallo: " + ex);
                return;
            }

            byte[] buffer = new byte[1024];
            while (!cancelReceive)
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);


                if (discoverySocket.Poll(100 * 1000, SelectMode.SelectRead))
                {
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
                Thread.Sleep(33);
            }
            discoverySocket.Close();
        }
    }


    public void DiscoverServer(Action<string> onServerFound)
    {
        clientDiscoveryThread = new Thread(() =>
        {
            using (Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                clientSocket.EnableBroadcast = true;
                IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

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
                clientSocket.Close();
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

        netObj.id = -1;

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

    // NUEVO: petición de ownership llamada desde el código de interacción (grabbing)
    public void RequestOwnership(int objectId)
    {
        requestingOwnershipId = objectId;
    }

    // NUEVO: petición de release (soltar) llamada desde gameplay
    public void RequestReleaseOwnership(int objectId)
    {
        requestingReleaseId = objectId;
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
            // Ajustar buffers del sistema para evitar truncado en recepción
            try
            {
                serverSocket.ReceiveBufferSize = MaxUdpPacketSize;
                serverSocket.SendBufferSize = MaxUdpPacketSize;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Server] No se pudo ajustar Send/ReceiveBufferSize: " + ex.Message);
            }

            Debug.Log($"[Server] escuchando en {localEndPoint}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Server] Bind fallo: " + ex);
            return;
        }

        byte[] bufferData = new byte[MaxUdpPacketSize];
        while (!cancelReceive)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                if (serverSocket.Poll(100 * 1000, SelectMode.SelectRead))
                {
                    int receivedDataLength = serverSocket.ReceiveFrom(bufferData, 0, ref sender);
                    if (receivedDataLength > 0)
                    {
                        Debug.Log($"[Server] recibido {receivedDataLength} bytes desde {sender}");

                        // registrar cliente conocido para broadcasting
                        lock (clientsLock)
                        {
                            connectedClients.Add(sender);
                        }

                        // Manejar peticiones ASCII simples
                        string maybeText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                        if (BufferIsText(bufferData, receivedDataLength, "REQUEST_ID"))
                        {
                            int newId = AllocateNetId();
                            string response = $"ASSIGN_ID:{newId}";
                            byte[] respBytes = System.Text.Encoding.UTF8.GetBytes(response);
                            serverSocket.SendTo(respBytes, sender);
                            Debug.Log($"[Server] asignado ID {newId} a {sender}");
                            continue; // no procesar transforms en este paquete
                        }
                        else if (maybeText.StartsWith("REQUEST_OWNERSHIP:"))
                        {
                            var parts = maybeText.Split(':');
                            if (parts.Length == 2 && int.TryParse(parts[1], out int requestedId))
                            {
                                if (objectsById.TryGetValue(requestedId, out var obj))
                                {
                                    // asignar ownerAddress al endpoint que pidió ownership
                                    obj.ownerAddress = sender.ToString();
                                    // Si el servidor ya controlaba el objeto, dejar de marcarlo para envío local del servidor:
                                    obj.isLocalPlayer = false;

                                    // enviar confirmación al cliente
                                    string response = $"OWNERSHIP_GRANTED:{requestedId}";
                                    byte[] respBytes = System.Text.Encoding.UTF8.GetBytes(response);
                                    serverSocket.SendTo(respBytes, sender);
                                    Debug.Log($"[Server] Ownership granted for id={requestedId} to {sender}");
                                }
                                else
                                {
                                    Debug.LogWarning($"[Server] REQUEST_OWNERSHIP: objeto id={requestedId} no encontrado");
                                }
                            }
                            continue;
                        }
                        else if (maybeText.StartsWith("RELEASE_OWNERSHIP:"))
                        {
                            var parts = maybeText.Split(':');
                            if (parts.Length == 2 && int.TryParse(parts[1], out int releasedId))
                            {
                                if (objectsById.TryGetValue(releasedId, out var obj))
                                {
                                    // Asegurar que el que pide release es realmente el owner registrado (o permitir liberación por admin)
                                    if (obj.ownerAddress == sender.ToString())
                                    {
                                        obj.ownerAddress = null;
                                        // servidor recupera control y volverá a enviar transforms
                                        obj.isLocalPlayer = true;

                                        // Notificar a todos los clientes que la ownership fue liberada (para que dejen de enviar)
                                        string notification = $"OWNERSHIP_RELEASED:{releasedId}";
                                        byte[] notifBytes = System.Text.Encoding.UTF8.GetBytes(notification);
                                        List<EndPoint> snapshot;
                                        lock (clientsLock)
                                        {
                                            snapshot = connectedClients.ToList();
                                        }
                                        foreach (var clientEP in snapshot)
                                        {
                                            try
                                            {
                                                serverSocket.SendTo(notifBytes, clientEP);
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.LogWarning($"[Server] Error enviando OWNERSHIP_RELEASED a {clientEP}: {ex.Message}");
                                            }
                                        }

                                        Debug.Log($"[Server] Ownership released for id={releasedId} by {sender}");
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[Server] RELEASE_OWNERSHIP de id={releasedId} ignorado: sender no es owner.");
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[Server] RELEASE_OWNERSHIP: objeto id={releasedId} no encontrado");
                                }
                            }
                            continue;
                        }

                        // Deserializar datos (pasa sender para que el servidor valide si el sender es owner)
                        DeserializeData(bufferData, receivedDataLength, sender);

                        // Después de procesar, enviar transforms actualizados a todos los clientes conocidos (broadcast)
                        byte[] transformData = SerializeData();
                        if (transformData != null && transformData.Length > 0)
                        {
                            if (transformData.Length > MaxUdpPacketSize)
                            {
                                Debug.LogWarning($"[Server] Transform data demasiado grande para UDP ({transformData.Length} bytes). No se enviará.");
                            }
                            else
                            {
                                List<EndPoint> snapshot;
                                lock (clientsLock)
                                {
                                    snapshot = connectedClients.ToList();
                                }
                                foreach (var clientEP in snapshot)
                                {
                                    try
                                    {
                                        serverSocket.SendTo(transformData, clientEP);
                                        Debug.Log($"[Server] enviado {transformData.Length} bytes a {clientEP}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"[Server] Error enviando a {clientEP}: {ex.Message}");
                                    }
                                }
                            }
                        }
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

            // Ajustar buffers del socket para permitir paquetes grandes
            try
            {
                clientSocket.ReceiveBufferSize = MaxUdpPacketSize;
                clientSocket.SendBufferSize = MaxUdpPacketSize;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Client] No se pudo ajustar Send/ReceiveBufferSize: " + ex.Message);
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

            byte[] bufferData = new byte[MaxUdpPacketSize];

            while (!cancelReceive)
            {
                // Enviar transforms locales
                byte[] transformData = SerializeData();
                try
                {
                    if (transformData != null && transformData.Length > 0)
                    {
                        if (transformData.Length > MaxUdpPacketSize)
                        {
                            Debug.LogWarning($"[Client] Transform data demasiado grande para UDP ({transformData.Length} bytes). No se enviará.");
                        }
                        else
                        {
                            clientSocket.SendTo(transformData, serverEndPoint);
                            Debug.Log($"[Client] enviado {transformData.Length} bytes a {serverEndPoint}");
                        }
                    }

                    if (requestingId)
                    {
                        byte[] idRequestData = System.Text.Encoding.UTF8.GetBytes("REQUEST_ID");
                        clientSocket.SendTo(idRequestData, serverEndPoint);
                        requestingId = false;
                    }

                    // NUEVO: peticiones de ownership iniciadas por el gameplay
                    if (requestingOwnershipId >= 0)
                    {
                        string req = $"REQUEST_OWNERSHIP:{requestingOwnershipId}";
                        byte[] reqBytes = System.Text.Encoding.UTF8.GetBytes(req);
                        clientSocket.SendTo(reqBytes, serverEndPoint);
                        Debug.Log($"[Client] REQUEST_OWNERSHIP enviado para id={requestingOwnershipId}");
                        requestingOwnershipId = -1;
                    }

                    // NUEVO: peticiones de release iniciadas por gameplay
                    if (requestingReleaseId >= 0)
                    {
                        string req = $"RELEASE_OWNERSHIP:{requestingReleaseId}";
                        byte[] reqBytes = System.Text.Encoding.UTF8.GetBytes(req);
                        clientSocket.SendTo(reqBytes, serverEndPoint);
                        Debug.Log($"[Client] RELEASE_OWNERSHIP enviado para id={requestingReleaseId}");
                        requestingReleaseId = -1;
                    }
                }
                catch (SocketException se)
                {
                    Debug.LogWarning("[Client] SendTo fallo: " + se.Message);
                }

                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    if (clientSocket.Poll(100 * 1000, SelectMode.SelectRead))
                    {
                        int receivedDataLength = clientSocket.ReceiveFrom(bufferData, 0, ref sender);
                        if (receivedDataLength > 0)
                        {
                            Debug.Log($"[Client] recibido {receivedDataLength} bytes desde {sender}");

                            // Comprobar si es un mensaje ASSIGN_ID:<id> o OWNERSHIP_GRANTED:<id> (ASCII)
                            string possibleAssign = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
                            if (possibleAssign.StartsWith("ASSIGN_ID:"))
                            {
                                var parts = possibleAssign.Split(':');
                                if (parts.Length == 2 && int.TryParse(parts[1], out int parsedId))
                                {
                                    // aplicar inmediatamente en hilo principal
                                    EnqueueMainThreadAction(() =>
                                    {
                                        var target = registeredObjects.FirstOrDefault(o => o.isLocalPlayer && o.id == -1);
                                        if (target != null)
                                        {
                                            AssignIdToLocalObject(target, parsedId);
                                            Debug.Log($"[Client] Asignado id {parsedId} al objeto local (hilo principal).");
                                        }
                                        else
                                        {
                                            assignedIdFromServer = parsedId;
                                        }
                                    });
                                }
                                else
                                {
                                    Debug.LogWarning("[Client] ASSIGN_ID formato inválido");
                                }
                            }
                            else if (possibleAssign.StartsWith("OWNERSHIP_GRANTED:"))
                            {
                                var parts = possibleAssign.Split(':');
                                if (parts.Length == 2 && int.TryParse(parts[1], out int grantedId))
                                {
                                    // Marcar localmente que este cliente ahora es propietario de ese objeto: permite el envío en SerializeData
                                    EnqueueMainThreadAction(() =>
                                    {
                                        var target = registeredObjects.FirstOrDefault(o => o.id == grantedId);
                                        if (target != null)
                                        {
                                            target.isLocalPlayer = true;
                                            Debug.Log($"[Client] Ownership granted for id={grantedId}. Ahora el cliente lo controla.");
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"[Client] OWNERSHIP_GRANTED para id={grantedId} pero no está instanciado localmente aún.");
                                        }
                                    });
                                }
                                else
                                {
                                    Debug.LogWarning("[Client] OWNERSHIP_GRANTED formato inválido");
                                }
                            }
                            else if (possibleAssign.StartsWith("OWNERSHIP_RELEASED:"))
                            {
                                var parts = possibleAssign.Split(':');
                                if (parts.Length == 2 && int.TryParse(parts[1], out int releasedId))
                                {
                                    EnqueueMainThreadAction(() =>
                                    {
                                        var target = registeredObjects.FirstOrDefault(o => o.id == releasedId);
                                        if (target != null)
                                        {
                                            // servidor recuperó control: dejar de enviar transforms desde el cliente
                                            target.isLocalPlayer = false;
                                            Debug.Log($"[Client] Ownership released for id={releasedId}. Cliente deja de controlar.");
                                        }
                                    });
                                }
                            }
                            else
                            {
                                // Mensaje binario: transforms (vienen del servidor normalmente)
                                DeserializeData(bufferData, receivedDataLength, sender);
                            }
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
            // Sólo enviar objetos locales que tengan id válido (>= 0)
            List<NetworkObject> toSend = registeredObjects.Where(o => o.isLocalPlayer && o.id >= 0).ToList();

            // Logging para depuración
            if (toSend.Count == 0)
            {
                // enviamos 0 para que el receptor sepa que no hay transforms
                formatter.Serialize(stream, 0);
                Debug.Log("[SerializeData] no hay objetos locales (con id) para enviar.");
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

        // Si excede el máximo UDP, no lo enviamos (evita excepción en ReceiveFrom)
        if (objectAsBytes.Length > MaxUdpPacketSize)
        {
            Debug.LogWarning($"[SerializeData] Paquete serializado demasiado grande para UDP ({objectAsBytes.Length} bytes). Considera reducir datos o fragmentar.");
            stream.Close();
            return null;
        }

        stream.Close();
        return objectAsBytes;
    }

    // Modificado: ahora recibe EndPoint sender para validar ownership cuando es servidor.
    public void DeserializeData(byte[] objectAsBytes, int length, EndPoint sender)
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

                // Ignorar ids inválidos
                if (transformDeserialized.id < 0)
                {
                    Debug.LogWarning($"[DeserializeData] Ignorado transform con id inválido: {transformDeserialized.id}");
                    continue;
                }

                NetworkObject target = null;
                target = registeredObjects.FirstOrDefault(o => o.id == transformDeserialized.id);

                if (target != null)
                {
                    // Si estamos en el cliente: aplicamos siempre lo que venga del servidor (sender es el server)
                    if (role == NetworkRole.Client)
                    {
                        if (!target.isLocalPlayer)
                        {
                            target.UpdateTransform(transformDeserialized.position, transformDeserialized.rotation, transformDeserialized.scale);
                        }
                    }
                    else // estamos en el servidor/host
                    {
                        // Validar que la actualización venga del propietario (si existe)
                        string senderStr = sender?.ToString();
                        if (!string.IsNullOrEmpty(target.ownerAddress))
                        {
                            if (senderStr == target.ownerAddress)
                            {
                                // update aceptado: el propietario remoto movió el objeto
                                target.netTransform.position = transformDeserialized.position;
                                target.netTransform.rotation = transformDeserialized.rotation;
                                target.netTransform.scale = transformDeserialized.scale;

                                // actualizar la representación que verán otros
                                target.targetPosition = transformDeserialized.position;
                                target.targetRotation = transformDeserialized.rotation;
                                target.targetScale = transformDeserialized.scale;
                            }
                            else
                            {
                                Debug.LogWarning($"[DeserializeData] Update de id={transformDeserialized.id} rechazado: sender {senderStr} no es owner {target.ownerAddress}");
                            }
                        }
                        else
                        {
                            // Si no hay owner remoto, y el objeto no es controlado por el servidor -> permitir actualización?
                            // Por defecto rechazamos actualizaciones para objetos que el servidor controla (isLocalPlayer == true)
                            if (!target.isLocalPlayer)
                            {
                                // objeto remoto no controlado por servidor, aceptar update (p.e. desde otro servidor/host)
                                target.UpdateTransform(transformDeserialized.position, transformDeserialized.rotation, transformDeserialized.scale);
                            }
                            else
                            {
                                Debug.Log($"[DeserializeData] Ignorado update para id={transformDeserialized.id} porque el servidor es autoritativo.");
                            }
                        }
                    }
                }
                else
                {
                    var captured = transformDeserialized; // capture local copy
                    EnqueueMainThreadAction(() =>
                    {
                        // Primero intentar reutilizar un NetworkObject ya presente en la escena sin id (placeholder)
                        var reusable = registeredObjects.FirstOrDefault(o => o.id == -1 && !o.isLocalPlayer);
                        if (reusable != null)
                        {
                            reusable.id = captured.id;
                            reusable.netTransform.position = captured.position;
                            reusable.netTransform.rotation = captured.rotation;
                            reusable.netTransform.scale = captured.scale;
                            reusable.targetPosition = captured.position;
                            reusable.targetRotation = captured.rotation;
                            reusable.targetScale = captured.scale;
                            objectsById[captured.id] = reusable;
                            Debug.Log($"[NetworkManager] Reutilizado objeto de escena para id={captured.id} (evitado ghost).");
                            return;
                        }

                        // Comprobar de nuevo en hilo principal para evitar duplicados
                        if (objectsById.ContainsKey(captured.id))
                        {
                            var existing = objectsById[captured.id];
                            if (existing != null)
                            {
                                existing.UpdateTransform(captured.position, captured.rotation, captured.scale);
                                Debug.Log($"[NetworkManager] Actualizado objeto existente id={captured.id} en lugar de instanciar duplicado.");
                                return;
                            }
                        }

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