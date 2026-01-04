using System;
using UnityEngine;

public class MovingWallNetwork : NetObj
{
    MovingWall movingWall;

    private void Awake()
    {
        movingWall = GetComponent<MovingWall>();
    }

    public override void SyncWithServer(float deltaTime)
    {
        Vector3 start = movingWall.startPos;
        Vector3 end = movingWall.endPos;
        float speed = movingWall.speed;

        float serverSimTime = Time.time + deltaTime;

        float t = Mathf.PingPong(serverSimTime * speed, 1f);
        Vector3 targetPos = Vector3.Lerp(start, end, t);

        movingWall.transform.localPosition = targetPos;
    }
}
