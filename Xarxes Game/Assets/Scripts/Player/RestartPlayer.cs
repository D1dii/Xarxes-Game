using UnityEngine;

public class RestartPlayer : MonoBehaviour
{
    
    public Vector3 respawnPoint; 
    
    private Rigidbody rb; 

    void Start()
    {
        respawnPoint = transform.position;
        rb = GetComponent<Rigidbody>();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DeathZone"))
        {
            RestartPosition();
        }
    }

    public void UpdateCheckpoint(Vector3 newPosition)
    {
        respawnPoint = newPosition;
    }

    public void RestartPosition()
    {
        transform.position = respawnPoint;

        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero;
        }
    }
}