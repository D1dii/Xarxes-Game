using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private GameObject optionsMenuCanvas;
    [SerializeField] private GameObject mainMenuCanvas;
    void Start()
    {
        optionsMenuCanvas.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }




    public void CloseOptionsMenu()
    {
        
        optionsMenuCanvas.SetActive(false);
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(true);
        }
    }
}
