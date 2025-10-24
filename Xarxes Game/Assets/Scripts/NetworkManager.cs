using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System;


public struct NetworkTransform
{
    public int id;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
}


public class NetworkManager : MonoBehaviour
{

    public static NetworkManager instance;

    Thread serverThread;
    Thread clientThread;

    public List<NetworkObject> registeredObjects;

    bool cancelReceive = false;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
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
        clientThread = new Thread(ClientProcess);
        serverThread = new Thread(ServerProcess);
        serverThread.Start();
    }

    // Update is called once per frame
    public void Update()
    {

    }

    public void OnDestroy()
    {
        cancelReceive = true;
        serverThread.Join();
        clientThread.Join();
    }

    public void RegisterObject(NetworkObject netObject)
    {
        registeredObjects.Add(netObject);
    }

    public void ServerProcess()
    {
        // Create Server Socket
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        EndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);
        serverSocket.Bind(localEndPoint);
        clientThread.Start();



        byte[] bufferData = new byte[4096];
        while (!cancelReceive)
        {

            int receivedDataLength = serverSocket.ReceiveFrom(bufferData, 0, ref localEndPoint);
            if (receivedDataLength > 0)
            {
                DeserializeData(bufferData);

                byte[] transformData = SerializeData();
                serverSocket.SendTo(transformData, localEndPoint);

            }
            Thread.Sleep(33);
        }

    }

    public void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);

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
                DeserializeData(bufferData);
            }

            Thread.Sleep(33);
        }

    }

   

    public byte[] SerializeData()
    {
        MemoryStream stream = new MemoryStream();
        BinaryFormatter formatter = new BinaryFormatter();
        try
        {
            formatter.Serialize(stream, registeredObjects.Count);

            foreach (NetworkObject netObject in registeredObjects)
            {
                formatter.Serialize(stream, netObject.id);
                formatter.Serialize(stream, netObject.netTransform.position.x);
                formatter.Serialize(stream, netObject.netTransform.position.y);
                formatter.Serialize(stream, netObject.netTransform.position.z);
                formatter.Serialize(stream, netObject.netTransform.rotation.x);
                formatter.Serialize(stream, netObject.netTransform.rotation.y);
                formatter.Serialize(stream, netObject.netTransform.rotation.z);
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

    public void DeserializeData(byte[] objectAsBytes)
    {
        
        MemoryStream stream = new MemoryStream();
        stream.Write(objectAsBytes, 0, objectAsBytes.Length);
        stream.Seek(0, SeekOrigin.Begin);
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
                transformDeserialized.scale.x = (float)formatter.Deserialize(stream);
                transformDeserialized.scale.y = (float)formatter.Deserialize(stream);
                transformDeserialized.scale.z = (float)formatter.Deserialize(stream);

                registeredObjects[i].UpdateTransform(transformDeserialized.position, transformDeserialized.rotation, transformDeserialized.scale);

            }

        }
        catch (SerializationException e)
        {
            Debug.Log("Deserialization Failed : " + e.Message);
        }
        stream.Close();
    }
}