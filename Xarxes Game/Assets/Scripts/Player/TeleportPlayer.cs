using UnityEngine;

public partial class TeleportPlayer : MonoBehaviour
{
    public Transform destination;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = destination.position;

            other.transform.rotation = destination.rotation;
        }
    }
}