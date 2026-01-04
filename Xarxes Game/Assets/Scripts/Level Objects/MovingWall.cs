using System;
using UnityEngine;

public class MovingWall : MonoBehaviour
{
    public Vector3 startPos;
    public Vector3 endPos;
    public float speed = 1f;

    public double timeOffset = 0.0;
    public double movementStartTime = double.NaN;

    private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void SetOffset(double deltaTime)
    {
        timeOffset = deltaTime;
    }

    void Update()
    {
        float clampedSpeed = Mathf.Max(0.0001f, speed);

        double clientNow = (DateTime.UtcNow - unixEpoch).TotalSeconds;
        double correctedTime = clientNow + timeOffset;

        double origin = 0.0;
        double elapsed = correctedTime - origin;
        if (elapsed < 0.0) elapsed = 0.0;

        double modTime = elapsed * (double)clampedSpeed;
        // reducimos con módulo 2.0 para reproducir PingPong
        double mod2 = modTime % 2.0;
        double tDouble = (mod2 <= 1.0) ? mod2 : 2.0 - mod2;
        float t = (float)tDouble;

        transform.localPosition = Vector3.Lerp(startPos, endPos, t);
    }
}
