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
