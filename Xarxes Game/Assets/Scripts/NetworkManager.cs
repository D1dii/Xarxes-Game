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
        //netObject.id = registeredObjects.Count() - 1;
    }

    public void ServerProcess()
    {
        // Create Server Socket
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
        serverSocket.Bind(localEndPoint);
        



        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedDataLength = serverSocket.ReceiveFrom(bufferData, 0, ref sender);
            if (receivedDataLength > 0)
            {
                DeserializeData(bufferData, receivedDataLength);

                byte[] transformData = SerializeData();
                serverSocket.SendTo(transformData, sender);

                

            }
            Thread.Sleep(33);
        }
        serverSocket.Close();

    }

    public void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);

        // Send Information

        byte[] bufferData = new byte[4096];

        while (!cancelReceive)
        {
            byte[] transformData = SerializeData();
            clientSocket.SendTo(transformData, serverEndPoint);

            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedDataLength = clientSocket.ReceiveFrom(bufferData, 0, ref sender);
            if (receivedDataLength > 0)
            {
                DeserializeData(bufferData, receivedDataLength);
            }

            Thread.Sleep(33);
        }
        clientSocket.Close();
    }

   

    public byte[] SerializeData()
    {
        MemoryStream stream = new MemoryStream();
        BinaryFormatter formatter = new BinaryFormatter();
        try
        {

            List<NetworkObject> toSend;
            toSend = registeredObjects.Where(o => o.isLocalPlayer).ToList();

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

                NetworkObject target = null;
                target = registeredObjects.FirstOrDefault(o => o.id == transformDeserialized.id);

                if (target != null)
                {
                    // No queremos sobrescribir datos locales del propietario; UpdateTransform actualiza target* para interpolar
                    if (!target.isLocalPlayer)
                    {
                        target.UpdateTransform(transformDeserialized.position, transformDeserialized.rotation, transformDeserialized.scale);
                    }
                }
                else
                {
                    // Opcional: registrar log si no se encuentra el id
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