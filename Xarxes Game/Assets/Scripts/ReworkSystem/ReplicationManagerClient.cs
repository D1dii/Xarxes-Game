using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class ReplicationManagerClient
{
    public void ReadWorldState(byte[] data, int headerSize)
    {
        using (var ms = new MemoryStream(data))
        {
            ms.Seek(headerSize, SeekOrigin.Begin); 
            var formatter = new BinaryFormatter();

            int objectCount = (int)formatter.Deserialize(ms);

            for (int i = 0; i < objectCount; i++)
            {
                int netId = (int)formatter.Deserialize(ms);

                
                float px = (float)formatter.Deserialize(ms);
                float py = (float)formatter.Deserialize(ms);
                float pz = (float)formatter.Deserialize(ms);
                Vector3 position = new Vector3(px, py, pz);

                
                float rx = (float)formatter.Deserialize(ms);
                float ry = (float)formatter.Deserialize(ms);
                float rz = (float)formatter.Deserialize(ms);
                float rw = (float)formatter.Deserialize(ms);
                Quaternion rotation = new Quaternion(rx, ry, rz, rw);

                
                GameObject obj = NetManager.instance.GetNetworkObjectById(netId);
                if (obj != null)
                {
                    
                    TransformNetObj script = obj.GetComponent<TransformNetObj>();
                    if (script != null)
                    {
                        script.UpdateState(position, rotation);
                    }
                }
            }
        }
    }
}