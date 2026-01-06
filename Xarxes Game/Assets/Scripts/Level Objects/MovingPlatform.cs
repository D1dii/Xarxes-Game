using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2.0f;

    private Vector3 target;

    void Start()
    {
        // Start by moving towards Point B
        target = pointB.position;
    }

    void Update()
    {
        // Move the platform towards the target position
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // Check if the platform has reached the target
        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            // Switch target between Point A and Point B
            target = (target == pointA.position) ? pointB.position : pointA.position;
        }
    }

    // This part ensures the player moves WITH the platform
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.SetParent(transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.SetParent(null);
        }
    }
}
