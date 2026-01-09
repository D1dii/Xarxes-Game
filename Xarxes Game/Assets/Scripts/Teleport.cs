using UnityEngine;

public partial class Teleport : MonoBehaviour
{
    // Drag your 'Destination' object into this slot in the Inspector
    public Transform destination;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger is the Player
        if (other.CompareTag("Player"))
        {
            // Move the player to the destination position
            other.transform.position = destination.position;

            // Optional: Match the destination's rotation
            other.transform.rotation = destination.rotation;
        }
    }
}