using UnityEngine;

public class DashCooldownUI : MonoBehaviour
{
    // 1. The Singleton Instance
    public static DashCooldownUI Instance { get; private set; }

    [SerializeField] private UnityEngine.UI.Slider dashCooldownSlider;

    private void Awake()
    {
        // 2. Initialize the Singleton
        // If an instance doesn't exist, this becomes the instance.
        // If one already exists (e.g., scene reload), destroy the duplicate.
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        // Ensure slider is full and hidden at start
        dashCooldownSlider.value = 1f;
        dashCooldownSlider.gameObject.SetActive(false);
    }

    public void HandleDash(float cooldownTime)
    {
        dashCooldownSlider.gameObject.SetActive(true);
        StartCoroutine(CooldownRoutine(cooldownTime));
    }

    private System.Collections.IEnumerator CooldownRoutine(float duration)
    {
        dashCooldownSlider.value = 0f;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            // Smoothly move slider from 0 to 1
            dashCooldownSlider.value = Mathf.Lerp(0f, 1f, timer / duration);
            yield return null;
        }

        dashCooldownSlider.value = 1f;
        dashCooldownSlider.gameObject.SetActive(false);
    }
}