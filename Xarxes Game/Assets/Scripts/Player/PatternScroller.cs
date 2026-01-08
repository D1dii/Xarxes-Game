using UnityEngine;
using UnityEngine.UI;

public class PatternScroller : MonoBehaviour
{
    [SerializeField] private RawImage _rawImage;
    [SerializeField] private float xSpeed = 0.3f; // Adjust speed here
    [SerializeField] private float ySpeed = 0.3f; // Adjust speed here

    void Update()
    {
        // 1. Get the current rectangle
        Rect uvRect = _rawImage.uvRect;

        // 2. Move the rectangle based on time
        uvRect.x -= xSpeed * Time.deltaTime; // -= moves left, += moves right
        uvRect.y += ySpeed * Time.deltaTime;

        // 3. Apply changes back to the image
        _rawImage.uvRect = uvRect;
    }

    void OnValidate()
    {
        if (_rawImage == null) _rawImage = GetComponent<RawImage>();
    }
}