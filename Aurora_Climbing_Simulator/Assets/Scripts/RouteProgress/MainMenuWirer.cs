using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Aurora.RouteProgress
{
    public class MainMenuWirer : MonoBehaviour
    {
        private void Start()
        {
            var toggles = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var toggle in toggles)
            {
                var tmp = toggle.GetComponentInChildren<TMP_Text>(true);
                if (tmp == null) continue;

                string lower = tmp.text.ToLower().Trim();

                if (lower.Contains("tutorial"))
                {
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (!isOn) return;
                        SceneManager.LoadScene("Climbing_Test");
                    });
                }
            }
        }
    }
}
