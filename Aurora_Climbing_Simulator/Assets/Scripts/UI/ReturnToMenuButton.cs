using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Botón de mundo para volver al menú principal. En lugar de depender de
/// triggers de física (que las manos VR no siempre disparan), detecta por
/// PROXIMIDAD: mide la distancia entre este objeto y las manos del jugador
/// cada frame, y al acercar una mano lo suficiente carga la escena del menú.
///
/// Las manos se localizan automáticamente desde el OVRCameraRig de la escena;
/// como respaldo, también se pueden asignar transforms a mano en el Inspector.
/// </summary>
public class ReturnToMenuButton : MonoBehaviour
{
    [Tooltip("Nombre de la escena del menú principal a cargar.")]
    public string menuSceneName = "PanelMenuPrincipal";

    [Tooltip("Distancia (metros) a la que una mano activa el botón.")]
    public float activationDistance = 0.12f;

    [Tooltip("Tiempo (segundos) que la mano debe permanecer cerca antes de activar. Evita toques accidentales.")]
    public float dwellTime = 0.4f;

    [Tooltip("Transforms de las manos. Si se dejan vacíos, se buscan automáticamente desde el OVRCameraRig.")]
    public Transform[] handTransforms;

    [Tooltip("Opcional: objeto visual que cambia de color/escala al acercarse (feedback). Puede dejarse vacío.")]
    public Transform highlightTarget;

    private float _timer;
    private bool _triggered;
    private Vector3 _baseScale = Vector3.one;

    private void Start()
    {
        if (highlightTarget != null) _baseScale = highlightTarget.localScale;
        if (handTransforms == null || handTransforms.Length == 0)
        {
            TryAutoFindHands();
        }
    }

    private void TryAutoFindHands()
    {
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            handTransforms = new Transform[]
            {
                rig.leftHandAnchor,
                rig.rightHandAnchor
            };
        }
    }

    private void Update()
    {
        if (_triggered) return;
        if (handTransforms == null || handTransforms.Length == 0) return;

        float nearest = float.MaxValue;
        Vector3 pos = transform.position;
        foreach (var hand in handTransforms)
        {
            if (hand == null) continue;
            float d = Vector3.Distance(pos, hand.position);
            if (d < nearest) nearest = d;
        }

        bool handNear = nearest <= activationDistance;

        // Feedback visual: crece un poco cuando una mano se acerca.
        if (highlightTarget != null)
        {
            float t = Mathf.Clamp01(1f - (nearest / (activationDistance * 3f)));
            highlightTarget.localScale = _baseScale * (1f + 0.15f * t);
        }

        if (handNear)
        {
            _timer += Time.deltaTime;
            if (_timer >= dwellTime)
            {
                _triggered = true;
                SceneManager.LoadScene(menuSceneName);
            }
        }
        else
        {
            _timer = 0f;
        }
    }
}
