using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class ReplicationManagerServer
{
    public void SendWorldState()
    {
        
        byte[] packet = BuildWorldStatePacket();

        
        foreach (var client in NetManager.instance.clientProxies)
        {
            NetManager.instance.serverManager.SendPacket(packet, client.GetEndPoint());
        }
    }

    private byte[] BuildWorldStatePacket()
    {
        using (var ms = new MemoryStream())
        {
            var formatter = new BinaryFormatter();

            
            formatter.Serialize(ms, 0); 
            formatter.Serialize(ms, (byte)NetManager.PacketType.WorldState);

            
            formatter.Serialize(ms, NetManager.instance.networkObjects.Count);

            foreach (var netObj in NetManager.instance.networkObjects)
            {
                
                formatter.Serialize(ms, netObj.netID);

               
                TransformNetObj tObj = netObj.GetComponent<TransformNetObj>();

                if (tObj != null)
                {
                    Vector3 pos = tObj.transform.position;
                    Quaternion rot = tObj.transform.rotation;

                    formatter.Serialize(ms, pos.x);
                    formatter.Serialize(ms, pos.y);
                    formatter.Serialize(ms, pos.z);

                    formatter.Serialize(ms, rot.x);
                    formatter.Serialize(ms, rot.y);
                    formatter.Serialize(ms, rot.z);
                    formatter.Serialize(ms, rot.w);
                }
               
            }
            return ms.ToArray();
        }
    }
}