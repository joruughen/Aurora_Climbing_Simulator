using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public void OnInicioPressed(string name)
    {
        SceneManager.LoadScene(name);
    }
}