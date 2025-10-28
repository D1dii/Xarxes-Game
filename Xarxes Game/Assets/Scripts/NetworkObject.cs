using TMPro;
using UnityEngine;

public class NetworkObject : MonoBehaviour
{

    public int id;

    public struct NetworkTransform
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
    public NetworkTransform netTransform;
    public Vector3 targetPosition;
    public Quaternion targetRotation;
    public Vector3 targetScale;

    public bool isLocalPlayer = false;

    void Awake()
    {
        netTransform.position = transform.position;
        netTransform.rotation = transform.rotation;
        netTransform.scale = transform.localScale;
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        targetScale = transform.localScale;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        NetworkManager.instance.RegisterObject(this);
    }

    // Update is called once per frame
    public void Update()
    {

        if (!isLocalPlayer)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 10);
        }
        else
        {
            netTransform.position = transform.position;
            netTransform.rotation = transform.rotation;
            netTransform.scale = transform.localScale;
        }

        
    }

    public void UpdateTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {

        netTransform.position = position;
        netTransform.rotation = rotation;
        netTransform.scale = scale;

        targetPosition = position;
        targetRotation = rotation;
        targetScale = scale;
    }
}
