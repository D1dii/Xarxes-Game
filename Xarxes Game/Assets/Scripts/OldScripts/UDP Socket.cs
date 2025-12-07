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



public class UDPSocket : MonoBehaviour
{
    Thread serverThread;
    Thread clientThread;

    bool cancelReceive = false;

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
                string receivedText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);

                DTO trainerDeserialized = DeserializeData(bufferData);

                Debug.Log("Trainer Name: " + trainerDeserialized.playerName);
                Debug.Log("Trainer level: " + trainerDeserialized.level);
                Debug.Log("Owned Pokemons: " + trainerDeserialized.ownedPokemons[0].name + ", " + trainerDeserialized.ownedPokemons[1].name);

                cancelReceive = true;

                //string ping = "Hello from server";
                //byte[] dataPing = new byte[1024];
                //dataPing = System.Text.Encoding.UTF8.GetBytes(ping);
                //serverSocket.SendTo(dataPing, localEndPoint);


            }
            Thread.Sleep(10);
        }

    }

    public void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);

        // Send Information

        DTO trainerToSerialize = CreateTrainer();

        byte[] dataTrainer = SerializeData(trainerToSerialize);
        clientSocket.SendTo(dataTrainer, serverEndPoint);

        //string text = "Hello from Client";
        //byte[] data = new byte[1024];
        //data = System.Text.Encoding.UTF8.GetBytes(text);
        //clientSocket.SendTo(data, serverEndPoint);

        byte[] bufferData = new byte[4096];
        //while (!cancelReceive)
        //{
        //    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        //    int receivedDataLength = clientSocket.ReceiveFrom(bufferData, 0, ref sender);
        //    if (receivedDataLength > 0)
        //    {
        //        string receivedText = System.Text.Encoding.UTF8.GetString(bufferData, 0, receivedDataLength);
        //        Debug.Log("Received from server: " + receivedText);

        //        string replyText = "Hello from client";
        //        byte[] replyData = System.Text.Encoding.UTF8.GetBytes(replyText);
        //        clientSocket.SendTo(replyData, serverEndPoint);

        //    }

        //    Thread.Sleep(10);
        //}
    }

    public DTO CreateTrainer()
    {
        DTO trainer = new DTO();
        trainer.ownedPokemons = new List<Pokemon>();

        trainer.playerName = "Juan Alberto";
        trainer.level = 50;

        Pokemon firstPokemon = new Pokemon();
        firstPokemon.name = "Charizard";

        Pokemon secondPokemon = new Pokemon();
        secondPokemon.name = "Pikachu";

        trainer.ownedPokemons.Add(firstPokemon);
        trainer.ownedPokemons.Add(secondPokemon);

        return trainer;
    }

    public byte[] SerializeData(DTO objectToSerialize)
    {
        MemoryStream stream = new MemoryStream();
        BinaryFormatter formatter = new BinaryFormatter();
        try
        {
            formatter.Serialize(stream, objectToSerialize.playerName);
            formatter.Serialize(stream, objectToSerialize.level);

            formatter.Serialize(stream, objectToSerialize.ownedPokemons.Count);

            foreach (Pokemon pokemon in objectToSerialize.ownedPokemons)
            {
                formatter.Serialize(stream, pokemon.name);
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

    public DTO DeserializeData(byte[] objectAsBytes)
    {
        DTO objectThatWasDeserialized = new DTO();
        MemoryStream stream = new MemoryStream();
        stream.Write(objectAsBytes, 0, objectAsBytes.Length);
        stream.Seek(0, SeekOrigin.Begin);
        BinaryFormatter formatter = new BinaryFormatter();
        try
        {
            objectThatWasDeserialized.playerName = (string)formatter.Deserialize(stream);
            objectThatWasDeserialized.level = (int)formatter.Deserialize(stream);

            int pokemonCount = (int)formatter.Deserialize(stream);

            objectThatWasDeserialized.ownedPokemons = new List<Pokemon>();

            for (int i = 0; i < pokemonCount; i++)
            {
                Pokemon pokemon = new Pokemon();
                pokemon.name = (string)formatter.Deserialize(stream);
                objectThatWasDeserialized.ownedPokemons.Add(pokemon);
            }
        }
        catch (SerializationException e)
        {
            Debug.Log("Deserialization Failed : " + e.Message);
        }
        stream.Close();
        return objectThatWasDeserialized;
    }
}

public class Pokemon
{
    public string name;
}

public struct DTO
{
    public string playerName;
    public int level;
    public List<Pokemon> ownedPokemons;
}
