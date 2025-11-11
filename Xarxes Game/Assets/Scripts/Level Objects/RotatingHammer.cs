using UnityEngine;

public class RotatingHammer : MonoBehaviour
{
    [Header("Pendulum Swing Settings")]
    public float speed = 1.5f;   
    public float limit = 75f;    
    public bool randomStart = false; 

    [Header("Hammer Impact Settings")]
    public float pushForce = 500f; 

    private float randomOffset = 0f;
    private Rigidbody hammerRb;
    
    private NetworkObject netObj;

    void Awake()
    {
        hammerRb = GetComponent<Rigidbody>();
        netObj = GetComponent<NetworkObject>();


        if (randomStart)
        {
            randomOffset = Random.Range(0f, 10f);
        }
    }

    void FixedUpdate()
    {

        if (netObj.isLocalPlayer == false) return;

        float angle = Mathf.Sin((Time.time + randomOffset) * speed) * limit;

        Quaternion targetRotation = Quaternion.Euler(angle, 0, 0);

        if (transform.parent != null)
        {
            hammerRb.MoveRotation(transform.parent.rotation * targetRotation);
        }
        else
        {
            hammerRb.MoveRotation(targetRotation);
        }
    }
}