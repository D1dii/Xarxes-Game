using UnityEngine;

public class HammerNetwork : NetObj
{
    RotatingHammer hammer;

    private void Awake()
    {
        hammer = GetComponent<RotatingHammer>();
    }

    public override void SyncWithServer(float startTime, float deltaTime)
    {
        hammer.SetOffset(deltaTime);
    }
}
