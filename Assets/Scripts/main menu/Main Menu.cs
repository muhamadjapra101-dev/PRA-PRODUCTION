using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void goToScene()
    {
        SceneManager.LoadScene("MainLevel");
    }
}
