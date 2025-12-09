using UnityEngine;

public class TransformNetObj : NetObj
{
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isInitialized = false;
    public float lerpSpeed = 10f;

    void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public void Update()
    {
    
        if (NetManager.instance.mode == NetManager.NetMode.Client ||
           (NetManager.instance.mode == NetManager.NetMode.Host)) 
        {
            if (isInitialized)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
            }
        }
    }

    // Esta función será llamada por el ReplicationManagerClient
    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        targetPosition = pos;
        targetRotation = rot;
        isInitialized = true;
    }
}