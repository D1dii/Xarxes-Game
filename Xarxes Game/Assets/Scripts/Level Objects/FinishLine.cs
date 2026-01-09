using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Call the function on the Manager
            Object.FindFirstObjectByType<LevelManager>().PlayerReachedFinish(other.name);
        }
    }
}