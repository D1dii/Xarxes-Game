using UnityEngine;

public class Rotator : MonoBehaviour
{
    // A public variable to easily change the rotation speed in the Inspector
    public float rotationSpeed = 100f;

    // Define the axis of rotation (e.g., Vector3.up for Y-axis)
    public Vector3 rotationAxis = Vector3.up;

    // Update is called once per frame
    void Update()
    {
        // Rotate the object around the specified axis 
        // using the defined speed and the time elapsed since the last frame.
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}