using UnityEngine;

public class MovingWall : MonoBehaviour
{

    public Vector3 startPos;
    public Vector3 endPos;

    public float speed = 1f;

    private NetworkObject netObj;

    private void Awake()
    {
        netObj = GetComponent<NetworkObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!netObj.isLocalPlayer) return;
        float t = Mathf.PingPong(Time.time * speed, 1f);
        transform.localPosition = Vector3.Lerp(startPos, endPos, t);
    }
}
