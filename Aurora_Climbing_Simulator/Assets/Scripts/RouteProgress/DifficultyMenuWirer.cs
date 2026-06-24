using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Aurora.RouteProgress
{
    public class DifficultyMenuWirer : MonoBehaviour
    {
        private void Start()
        {
            var toggles = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var toggle in toggles)
            {
                var tmp = toggle.GetComponentInChildren<TMP_Text>(true);
                if (tmp == null) continue;

                string lower = tmp.text.ToLower().Trim();

                if (lower.Contains("principiante"))
                {
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (!isOn) return;
                        DifficultySettings.Selected = Difficulty.Principiante;
                        SceneManager.LoadScene("MainScene_backup");
                    });
                }
                else if (lower.Contains("avanzado"))
                {
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (!isOn) return;
                        DifficultySettings.Selected = Difficulty.Avanzado;
                        SceneManager.LoadScene("MainScene_backup");
                    });
                }
                else if (lower.Contains("intermedio"))
                {
                    toggle.gameObject.SetActive(false);
                }
            }
        }
    }
}
