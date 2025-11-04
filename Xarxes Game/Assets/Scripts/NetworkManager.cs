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

    public List<NetworkObject> registeredObjects;

    public bool cancelReceive = false;
    public int port = 9050;

    public string serverIP = "127.0.0.1";

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
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
        }
        else if (role == NetworkRole.Host)
        {
            serverThread = new Thread(ServerProcess);
            clientThread = new Thread(ClientProcess);
            serverThread.Start();
            clientThread.Start();
        }
    }

    // Update is called once per frame
    public void Update()
    {

    }

    public void OnDestroy()
    {
        cancelReceive = true;
        if (serverThread != null && serverThread.IsAlive)
            serverThread.Abort();
        if (clientThread != null && clientThread.IsAlive)
            clientThread.Abort();

    }

    public void RegisterObject(NetworkObject netObject)
    {
        registeredObjects.Add(netObject);
        // Atención: aquí no se asigna un ID "global" sincronizado entre máquinas.
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
                        DeserializeData(bufferData, receivedDataLength);
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
                    Debug.LogWarning($"NetworkManager: recibido transform para id={transformDeserialized.id} pero no existe localmente.");
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