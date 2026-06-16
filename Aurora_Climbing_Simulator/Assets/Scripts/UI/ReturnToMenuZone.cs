using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga la escena del menú principal cuando algo (la mano o la cámara del
/// jugador) entra en el trigger de esta zona. Pensado para una "zona de regreso"
/// física en el mundo VR: el jugador acerca la mano o el cuerpo y vuelve al menú.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReturnToMenuZone : MonoBehaviour
{
    [Tooltip("Nombre de la escena del menú principal a cargar.")]
    public string menuSceneName = "PanelMenuPrincipal";

    [Tooltip("Solo activa la zona si el objeto que entra tiene alguno de estos tags. Vacío = cualquier objeto (recomendado, ya que las manos VR no suelen tener tag).")]
    public string[] triggerTags = new string[0];

    [Tooltip("Tiempo en segundos que se debe permanecer dentro antes de cargar (evita activaciones accidentales).")]
    public float dwellTime = 0.5f;

    private float _timer;
    private bool _inside;
    private bool _triggered;

    private void Reset()
    {
        // Asegura que el collider sea trigger al añadir el componente en el editor.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValid(other)) return;
        _inside = true;
        _timer = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValid(other)) return;
        _inside = false;
        _timer = 0f;
    }

    private void Update()
    {
        if (_triggered || !_inside) return;

        _timer += Time.deltaTime;
        if (_timer >= dwellTime)
        {
            _triggered = true;
            SceneManager.LoadScene(menuSceneName);
        }
    }

    private bool IsValid(Collider other)
    {
        if (triggerTags == null || triggerTags.Length == 0) return true;
        foreach (var tag in triggerTags)
        {
            if (!string.IsNullOrEmpty(tag) && other.CompareTag(tag)) return true;
        }
        return false;
    }
}
