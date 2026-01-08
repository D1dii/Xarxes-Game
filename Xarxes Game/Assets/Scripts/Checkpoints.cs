using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private bool isActivated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            RestartPlayer playerScript = other.GetComponent<RestartPlayer>();

            if (playerScript != null)
            {
                playerScript.UpdateCheckpoint(transform.position);

                isActivated = true;              
            }
        }
    }
}