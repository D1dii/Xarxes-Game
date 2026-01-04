using UnityEngine;

public class RotatingHammer : MonoBehaviour
{
    [Header("Pendulum Swing Settings")]
    public float speed = 1.5f;   
    public float limit = 75f;    

    [Header("Hammer Impact Settings")]
    public float pushForce = 500f; 

    private float randomOffset = 0f;
    private Rigidbody hammerRb;
    

    void Awake()
    {
        hammerRb = GetComponent<Rigidbody>();

    }
    void FixedUpdate()
    {

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