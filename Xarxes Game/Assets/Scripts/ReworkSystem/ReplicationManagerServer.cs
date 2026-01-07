using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using static NetManager;

public class ReplicationManagerServer
{

    public void ObjectModifiedReceived(byte[] packetReceived, int receivedDataLength, int headerSize)
    {
        using (var ms = new MemoryStream(packetReceived, headerSize, receivedDataLength - headerSize))
        {
            var formatter = new BinaryFormatter();
            int netId = (int)formatter.Deserialize(ms);
            int clientNetId = (int)formatter.Deserialize(ms);


            float px = (float)formatter.Deserialize(ms); float py = (float)formatter.Deserialize(ms); float pz = (float)formatter.Deserialize(ms);

            float rx = (float)formatter.Deserialize(ms); float ry = (float)formatter.Deserialize(ms); float rz = (float)formatter.Deserialize(ms); float rw = (float)formatter.Deserialize(ms);

            Vector3 newPos = new Vector3(px, py, pz);
            Quaternion newRot = new Quaternion(rx, ry, rz, rw);

            if (!HasTemporalOwner(netId, clientNetId))
            {
                SendObjectState(netId, clientNetId, newPos, newRot);
                SyncModifiedObject(netId, newPos, newRot);
            }
            else
            {
                DenyObjectModification(netId, clientNetId);
            }

            
        }


    }

    public bool HasTemporalOwner(int netId, int clientNetId)
    {
        GameObject netObj = NetManager.instance.GetNetworkObjectById(netId);
        if (netObj != null)
        {
            TransformNetObj transformNetObj = netObj.GetComponent<TransformNetObj>();
            if (transformNetObj != null)
            {
                if (transformNetObj.ownerClientId == -1)
                {
                    transformNetObj.ownerClientId = clientNetId;
                    return false;
                }
            }
        }
        return true;
    }

    public void SyncModifiedObject(int netId, Vector3 position, Quaternion rotation)
    {
        GameObject netObj = NetManager.instance.GetNetworkObjectById(netId);
        if (netObj != null)
        {
            TransformNetObj transformNetObj = netObj.GetComponent<TransformNetObj>();
            if (transformNetObj != null)
            {
                transformNetObj.UpdateState(position, rotation);
                NetManager.instance.StartCoroutine(ResetOwnership(transformNetObj));
            }
        }
    }

    public IEnumerator ResetOwnership(TransformNetObj objectModified)
    {
       yield return new WaitForSeconds(0.5f);
       objectModified.ownerClientId = -1;
    }

    public void SendObjectState(int netId, int clientNetId, Vector3 position, Quaternion rotation)
    {
        int packetId = AcknowledgementManager.instance.AssignPacketID();
        byte[] packet = BuildObjectStatePacket(packetId, netId, position, rotation);

        foreach (var client in NetManager.instance.clientProxies)
        {
            if (clientNetId != client.netId)
            {
                NetManager.instance.serverSocket.SendTo(packet, client.GetEndPoint());
            }
            
        }
    }

    public void DenyObjectModification(int netId, int clientNetId)
    {
        GameObject netObj = NetManager.instance.GetNetworkObjectById(netId);
        if (netObj != null)
        {
            TransformNetObj transformNetObj = netObj.GetComponent<TransformNetObj>();
            if (transformNetObj != null)
            {
                int packetId = AcknowledgementManager.instance.AssignPacketID();
                byte[] packet = BuildObjectStatePacket(packetId,netId, transformNetObj.transform.position, transformNetObj.transform.rotation);
                foreach (var client in NetManager.instance.clientProxies)
                {
                    if (clientNetId == client.netId)
                    {
                        NetManager.instance.serverSocket.SendTo(packet, client.GetEndPoint());
                    }
                }
            }
        }
    }

    private byte[] BuildObjectStatePacket(int packetId, int netId, Vector3 position, Quaternion rotation)
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(ms, packetId);
            formatter.Serialize(ms, (byte)PacketType.WorldState);
            formatter.Serialize(ms, netId);
            formatter.Serialize(ms, position.x);
            formatter.Serialize(ms, position.y);
            formatter.Serialize(ms, position.z);
            formatter.Serialize(ms, rotation.x);
            formatter.Serialize(ms, rotation.y);
            formatter.Serialize(ms, rotation.z);
            formatter.Serialize(ms, rotation.w);
            return ms.ToArray();
        }
    }
}