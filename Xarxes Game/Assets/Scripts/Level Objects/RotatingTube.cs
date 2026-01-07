using UnityEngine;

public class RotatingTube : MonoBehaviour
{
   
    private void OnRenderObject()
    {
        transform.Rotate(new Vector3(30, 0, 0) * Time.deltaTime);
    }
}

