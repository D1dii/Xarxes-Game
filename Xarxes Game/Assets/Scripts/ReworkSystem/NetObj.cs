using UnityEngine;

public class NetObj : MonoBehaviour
{
    public int netID = -1;

    protected virtual void Start()
    {
        if (NetManager.instance != null)
        {
            if (!NetManager.instance.networkObjects.Contains(this))
            {
                NetManager.instance.networkObjects.Add(this);

                // Asignar ID si soy Host/Server y no tengo
                if ((NetManager.instance.mode == NetManager.NetMode.Server ||
                     NetManager.instance.mode == NetManager.NetMode.Host) && netID <= 0)
                {
                    netID = NetManager.instance.AssignNetID();
                }
            }
        }
    }

    protected virtual void OnDestroy()
    {
        if (NetManager.instance != null)
        {
            NetManager.instance.networkObjects.Remove(this);
        }
    }
}
