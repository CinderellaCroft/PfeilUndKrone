using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu: MonoBehaviour
{
/*
    public GameObject creditsPanel;
    public GameObject settingsPanel;
    public GameObject startGamePanel;
    public GameObject JoinGamePanel;
    public GameObject TutorialPanel;


    public void ShowCredits(){
        creditsPanel.SetActive(true);
    }

    public void HideCredits(){
        creditsPanel.SetActive(false);
    }

    public void ShowSettings(){
        settingsPanel.SetActive(true);
    }

    public void HideSettings(){
        settingsPanel.SetActive(false);
    }

    public void ShowJoinGame(){
        JoinGamePanel.SetActive(true);
    }

    public void HideJoinGame(){
        JoinGamePanel.SetActive(false);
    }

    public void ShowStartGamePanel(){
        startGamePanel.SetActive(true);
    }

    public void HideStartGamePanel(){
        startGamePanel.SetActive(false);
    }

    public void ShowTutorialPanel(){
        TutorialPanel.SetActive(true);
    }

    public void HideTutorialPanel(){
        TutorialPanel.SetActive(false);
    }
    */

    public void TogglePanel(GameObject panel)
    {
        panel.SetActive(!panel.activeSelf);
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

}
