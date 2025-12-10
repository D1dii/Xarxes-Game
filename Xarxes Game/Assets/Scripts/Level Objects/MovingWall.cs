using UnityEngine;

public class MovingWall : MonoBehaviour
{

    public Vector3 startPos;
    public Vector3 endPos;

    public float speed = 1f;


    private void Awake()
    {

    }

    // Update is called once per frame
    void Update()
    {
        float t = Mathf.PingPong(Time.time * speed, 1f);
        transform.localPosition = Vector3.Lerp(startPos, endPos, t);
    }
}
