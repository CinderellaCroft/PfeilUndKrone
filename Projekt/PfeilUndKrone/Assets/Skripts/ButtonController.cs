using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonController : MonoBehaviour
{
    public void start()
    {
        //NetworkManager.Instance.Start(); 
        SceneManager.LoadScene(1);
    }

    public void exit()
    {
        Application.Quit();
    }

}
