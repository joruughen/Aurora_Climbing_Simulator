using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public string sceneName;

    public void OnInicioPressed(bool isOn)
    {
        if (isOn)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}