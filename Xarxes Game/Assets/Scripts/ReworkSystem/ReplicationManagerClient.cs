using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class ReplicationManagerClient
{
    public void ReadWorldState(byte[] data, int receivedDataLength, int headerSize)
    {
        using (var ms = new MemoryStream(data, headerSize, receivedDataLength - headerSize))
        {
            var formatter = new BinaryFormatter();

            int netId = (int)formatter.Deserialize(ms);
            

            Vector3 position;
            position.x = (float)formatter.Deserialize(ms);
            position.y = (float)formatter.Deserialize(ms);
            position.z = (float)formatter.Deserialize(ms);

            Quaternion rotation;
            rotation.x = (float)formatter.Deserialize(ms);
            rotation.y = (float)formatter.Deserialize(ms);
            rotation.z = (float)formatter.Deserialize(ms);
            rotation.w = (float)formatter.Deserialize(ms);

            GameObject netObj = NetManager.instance.GetNetworkObjectById(netId);
            if (netObj != null)
            {
                TransformNetObj transformNetObj = netObj.GetComponent<TransformNetObj>();
                if (transformNetObj != null)
                {
                    transformNetObj.UpdateState(position, rotation);
                }
            }
        }
    }
}