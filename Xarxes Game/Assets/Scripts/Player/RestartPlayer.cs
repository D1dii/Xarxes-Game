using UnityEngine;

public class RestartPlayer : MonoBehaviour
{

    public Vector3 startPos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startPos = transform.position;
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DeathZone"))
        {
            RestartPosition();
        }
    }
    public void RestartPosition()
    {
        transform.position = startPos;
    }
}
