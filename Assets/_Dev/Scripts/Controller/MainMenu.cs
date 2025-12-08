using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public Button OfflinePlayButton, OnlinePlayButton;

    private void Awake()
    {
        OfflinePlayButton.onClick.AddListener(PlayOffline);
        OnlinePlayButton.onClick.AddListener(PlayOnline);
    }

    public void PlayOffline()
    {
        SceneManager.LoadScene("GameSceneOffline");
    }

    public void PlayOnline()
    {
        SceneManager.LoadScene("GameSceneOnline");
    }

    public void Quit()
    {
        Application.Quit();
    }
}
