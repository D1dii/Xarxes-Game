using UnityEngine;

public class NetworkObject : MonoBehaviour
{

    public int id;

    public struct NetworkTransform
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
    }
    public NetworkTransform netTransform;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        NetworkManager.instance.RegisterObject(this);
    }

    // Update is called once per frame
    public void Update()
    {
        netTransform.position = transform.position;
        netTransform.rotation = transform.eulerAngles;
        netTransform.scale = transform.localScale;
    }

    public void UpdateTransform(Vector3 position, Vector3 rotation, Vector3 scale)
    {
        transform.position = position;
        transform.eulerAngles = rotation;
        transform.localScale = scale;
    }
}
