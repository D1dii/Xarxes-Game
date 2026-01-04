using System;
using UnityEngine;

public class MovingWallNetwork : NetObj
{
    MovingWall movingWall;
    private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private void Awake()
    {
        movingWall = GetComponent<MovingWall>();
    }

    public override void SyncWithServer(float startTime, float deltaTime)
    {
        movingWall.SetOffset(deltaTime);
    }
}
