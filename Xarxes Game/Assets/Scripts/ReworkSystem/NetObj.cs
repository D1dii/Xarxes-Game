using UnityEngine;

public class NetObj : MonoBehaviour
{

    public int netID = -1;

    
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isInitialized = false;

    
    public float lerpSpeed = 10f;

    public void Awake()
    {
       
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public void Update()
    {
        if (NetManager.instance.mode == NetManager.NetMode.Client ||
           (NetManager.instance.mode == NetManager.NetMode.Host && !IsServerControlled()))
        {
            if (isInitialized)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
            }
        }
    }

    
    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        targetPosition = pos;
        targetRotation = rot;
        isInitialized = true;
    }

    
    private bool IsServerControlled()
    {
        return true;
    }

}
