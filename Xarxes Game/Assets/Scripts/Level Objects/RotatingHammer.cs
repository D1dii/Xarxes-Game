using System;
using UnityEngine;

public class RotatingHammer : MonoBehaviour
{
    [Header("Pendulum Swing Settings")]
    public float speed = 1.5f;   
    public float limit = 75f;    

    [Header("Hammer Impact Settings")]
    public float pushForce = 500f; 

    private Rigidbody hammerRb;

    private double timeOffset = 0.0;

    private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);


    void Awake()
    {
        hammerRb = GetComponent<Rigidbody>();

    }

    public void SetOffset(double deltaTime)
    {
        timeOffset = deltaTime;
    }

    void FixedUpdate()
    {
        float clampedSpeed = Mathf.Max(0.0001f, speed);

        double clientNow = (DateTime.UtcNow - unixEpoch).TotalSeconds;
        double correctedTime = clientNow + timeOffset;

        double origin = 0.0;
        double elapsed = correctedTime - origin;
        if (elapsed < 0.0) elapsed = 0.0;

        double modTime = elapsed * (double)clampedSpeed;

        double twoPi = Math.PI * 2.0;
        double modTimeWrapped = modTime % twoPi;
        if (modTimeWrapped < 0.0) modTimeWrapped += twoPi;

        float angle = (float)(Math.Sin(modTimeWrapped) * (double)limit);

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