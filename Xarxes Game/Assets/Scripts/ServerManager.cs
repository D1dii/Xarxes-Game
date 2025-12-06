using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;


public class ServerManager : MonoBehaviour
{

    public int port = 9050;
    public string serverIP = "127.0.0.1";
    public IPEndPoint serverEndPoint;

    private Queue<byte[]> sendQueue = new Queue<byte[]>();

    public Thread serverThread;

    private struct ReceivedPacket
    {
        public byte[] data;
        public int length;
        public EndPoint from;
    }

    private readonly Queue<ReceivedPacket> receiveQueue = new Queue<ReceivedPacket>();
    private readonly object receiveQueueLock = new object();

    public void Awake()
    {
        serverThread = new Thread(new ThreadStart(ServerProcess));
        serverThread.IsBackground = true;
    }

    public void OnDestroy()
    {
        NetManager.instance.cancelReceive = true;

        try
        {
            NetManager.instance.serverSocket?.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Warning cerrando socket en OnDestroy: {ex.Message}");
        }

        if (serverThread != null && serverThread.IsAlive)
        {
            bool exited = serverThread.Join(1000);
            if (!exited)
            {
                Debug.LogWarning("El hilo del servidor no respondió al cierre dentro del timeout.");
            }
        }
    }

    public void ServerProcess()
    {
        // Usamos Available en lugar de Poll para comprobar rápidamente si hay datos sin esperar largos timeouts
        while (!NetManager.instance.cancelReceive)
        {
            Socket sock = NetManager.instance?.serverSocket;
            if (sock == null)
            {
                Thread.Sleep(100);
                continue;
            }

            try
            {
                // Comprobar bytes disponibles de forma rápida (no bloqueante)
                int available = 0;
                try
                {
                    available = sock.Available;
                }
                catch (SocketException)
                {
                    // si Available falla, tratar como no hay datos y continuar
                    available = 0;
                }
                catch (ObjectDisposedException)
                {
                    // socket cerrado desde otro hilo
                    if (NetManager.instance.cancelReceive) break;
                    available = 0;
                }

                if (available == 0)
                {
                    if (NetManager.instance.cancelReceive) break;
                    // Evitar busy-loop: pequeña espera
                    Thread.Sleep(10);
                    continue;
                }

                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] buffer = new byte[4096];

                int receivedDataLength = sock.ReceiveFrom(buffer, ref remoteEP);
                if (receivedDataLength > 0)
                {
                    byte[] receivedData = new byte[receivedDataLength];
                    Buffer.BlockCopy(buffer, 0, receivedData, 0, receivedDataLength);

                    var packet = new ReceivedPacket
                    {
                        data = receivedData,
                        length = receivedDataLength,
                        from = remoteEP
                    };

                    lock (receiveQueueLock)
                    {
                        receiveQueue.Enqueue(packet);
                    }
                }
            }
            catch (SocketException sex)
            {
                // Registra el código para depuración; maneja explícitamente los no críticos
                Debug.LogWarning($"SocketException en ServerProcess (Código={sex.SocketErrorCode}): {sex.Message}");

                if (sex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Timeout (si ocurre): no crítico, continuar
                    if (NetManager.instance.cancelReceive) break;
                    continue;
                }

                if (sex.SocketErrorCode == SocketError.ConnectionReset ||
                    sex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    // ICMP "port unreachable" u host inaccesible: registrar y continuar
                    if (NetManager.instance.cancelReceive) break;
                    continue;
                }

                if (NetManager.instance.cancelReceive) break;

                Debug.LogError($"SocketException en ServerProcess: {sex}");
                Thread.Sleep(10);
            }
            catch (ObjectDisposedException)
            {
                // Socket cerrado desde otro hilo: salir
                Debug.LogWarning("Socket fue cerrado, saliendo de ServerProcess.");
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Excepción en ServerProcess: {ex}");
                if (NetManager.instance.cancelReceive) break;
                Thread.Sleep(10);
            }

            // Mantener una pequeña espera para evitar uso excesivo de CPU
            Thread.Sleep(10);
        }
    }

    public void Update()
    {
        List<ReceivedPacket> pending = null;
        lock (receiveQueueLock)
        {
            if (receiveQueue.Count > 0)
            {
                pending = new List<ReceivedPacket>(receiveQueue.Count);
                while (receiveQueue.Count > 0)
                    pending.Add(receiveQueue.Dequeue());
            }
        }

        if (pending != null)
        {
            foreach (var p in pending)
            {
                try
                {
                    NetManager.instance.OnPacketReceived(p.data, p.length, p.from);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Excepción procesando paquete en hilo principal: {ex}");
                }
            }
        }
    }

    public void SendPacket(byte[] sendData, EndPoint clientIP)
    {
        try
        {
            NetManager.instance.serverSocket.SendTo(sendData, clientIP);
        }
        catch (SocketException sex)
        {
            Debug.LogWarning($"SocketException en SendPacket: {sex.SocketErrorCode} - {sex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error enviando paquete: {ex.Message}");
        }
    }
}
