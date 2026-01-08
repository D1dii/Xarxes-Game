using UnityEngine;

public class TreadmillPlatform : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The force applied to the player per frame.")]
    public float pushForce = 50f;

    [Tooltip("The direction the treadmill pushes (relative to the platform).")]
    public Vector3 pushDirection = Vector3.back;

    void OnCollisionStay(Collision other)
    {
        // Check if the object is the player (make sure your player has the "Player" tag)
        if (other.gameObject.CompareTag("Player"))
        {
            Rigidbody playerRb = other.gameObject.GetComponent<Rigidbody>();

            if (playerRb != null)
            {
                // Convert local direction to world space so it rotates with the platform
                Vector3 worldDirection = transform.TransformDirection(pushDirection);

                // Apply ForceMode.Acceleration to ignore mass, making it feel snappy
                playerRb.AddForce(worldDirection * pushForce, ForceMode.Acceleration);
            }
        }
    }
}