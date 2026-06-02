using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public void OnInicioPressed()
    {
        SceneManager.LoadScene("Climbing_Test"); // o el nombre exacto de tu escena destino
    }
}