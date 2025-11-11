using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private GameObject optionsMenuCanvas;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void CreateLobby()
    {
        networkManager.JoinAsHost();
    }

    public void JoinLobby()
    {
        networkManager.JoinAsClient();
    }

    public void OpenOptionsMenu()
    {
        optionsMenuCanvas.SetActive(true);
        mainMenuCanvas.SetActive(false);
    }

    // Preferred name and graceful quit handling
    public void QuitGame()
    {
        // Signal network manager to stop background receive/threads (adjust if you add a proper Shutdown method)
        if (networkManager != null)
        {
            networkManager.cancelReceive = true;
            // If you implement a dedicated cleanup method on NetworkManager, call it here:
            // networkManager.Shutdown();
        }

        // In the Editor stop play mode; in builds quit the application
    #if UNITY_EDITOR
        EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }


}
