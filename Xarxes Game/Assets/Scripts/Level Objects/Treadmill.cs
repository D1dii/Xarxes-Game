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
        if (other.gameObject.CompareTag("Player"))
        {
            Rigidbody playerRb = other.gameObject.GetComponent<Rigidbody>();

            if (playerRb != null)
            {
                Vector3 worldDirection = transform.TransformDirection(pushDirection);

                playerRb.AddForce(worldDirection * pushForce, ForceMode.Acceleration);
            }
        }
    }
}