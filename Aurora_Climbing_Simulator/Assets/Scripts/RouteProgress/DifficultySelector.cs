using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aurora.RouteProgress
{
    public class DifficultySelector : MonoBehaviour
    {
        [SerializeField] private string _mainSceneName = "MainScene_backup";

        public void OnPrincipiantePressed(bool isOn)
        {
            if (!isOn) return;
            DifficultySettings.Selected = Difficulty.Principiante;
            SceneManager.LoadScene(_mainSceneName);
        }

        public void OnAvanzadoPressed(bool isOn)
        {
            if (!isOn) return;
            DifficultySettings.Selected = Difficulty.Avanzado;
            SceneManager.LoadScene(_mainSceneName);
        }

        public void OnBackPressed(bool isOn)
        {
            if (!isOn) return;
            SceneManager.LoadScene("PanelMenuPrincipal");
        }
    }
}
