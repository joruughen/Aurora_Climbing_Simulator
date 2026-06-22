using UnityEngine;
using UnityEngine.SceneManagement;
using Aurora.RouteProgress;

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

    public void OnPrincipiantePressed(bool isOn)
    {
        if (!isOn) return;
        DifficultySettings.Selected = Difficulty.Principiante;
        SceneManager.LoadScene("MainScene");
    }

    public void OnAvanzadoPressed(bool isOn)
    {
        if (!isOn) return;
        DifficultySettings.Selected = Difficulty.Avanzado;
        SceneManager.LoadScene("MainScene");
    }

    public void OnTutorialPressed(bool isOn)
    {
        if (!isOn) return;
        SceneManager.LoadScene("Climbing_Test");
    }

    public void OnVolverPressed(bool isOn)
    {
        if (!isOn) return;
        SceneManager.LoadScene("PanelMenuPrincipal");
    }
}