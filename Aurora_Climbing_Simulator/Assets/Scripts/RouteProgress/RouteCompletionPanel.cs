using TMPro;
using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Panel final de compleción para VR. Está oculto al inicio y se muestra cuando el manager
    /// dispara OnRouteCompleted. Rellena las estadísticas con TextMeshPro y opcionalmente se
    /// orienta hacia el jugador (billboard) para que siempre sea legible en VR.
    ///
    /// Botón "Reintentar"/"Reset": al pulsarlo (vía <see cref="OnResetButton"/>) oculta el panel
    /// y reinicia las estadísticas en el manager, permitiendo volver a iniciar la ruta.
    /// </summary>
    [AddComponentMenu("Aurora/Route Progress/Route Completion Panel")]
    public class RouteCompletionPanel : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField]
        [Tooltip("Manager cuyo evento OnRouteCompleted hace aparecer el panel. " +
                 "Si se deja vacío se busca en la escena.")]
        private RouteProgressManager _manager;

        [Header("Raíz del panel")]
        [SerializeField]
        [Tooltip("GameObject CONTENEDOR del panel que se activa/desactiva al mostrar/ocultar. " +
                 "DEBE ser un hijo, NO este mismo GameObject: si se desactivara este GameObject en " +
                 "Awake, dejaría de escuchar el evento de compleción. Si se deja vacío se usa este " +
                 "GameObject como respaldo, pero entonces la suscripción se hace en Awake para no perderla.")]
        private GameObject _panelRoot;

        [Header("Textos de estadísticas (TMP)")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _startHeightText;
        [SerializeField] private TMP_Text _finishHeightText;
        [SerializeField] private TMP_Text _maxHeightText;
        [SerializeField] private TMP_Text _climbedHeightText;

        [SerializeField]
        [Tooltip("Texto de agarres usados. Opcional: déjalo vacío si no se integra el conteo de agarres.")]
        private TMP_Text _holdsText;

        [Header("Billboard (mirar al jugador)")]
        [SerializeField]
        [Tooltip("Si está activo, el panel rota cada frame para mirar a la cámara mientras está visible.")]
        private bool _faceCamera = true;

        [SerializeField]
        [Tooltip("Cámara/transform a la que mirar. Si se deja vacío se usa Camera.main.")]
        private Transform _cameraTransform;

        [Header("Posición al mostrarse")]
        [SerializeField]
        [Tooltip("Si está activo, al completar la ruta el panel se coloca frente al jugador, a la " +
                 "altura de sus ojos. Recomendado para VR (evita que quede muy alto o lejos).")]
        private bool _placeInFrontOfCamera = true;

        [SerializeField]
        [Tooltip("Distancia (m) frente a la cámara a la que aparece el panel.")]
        private float _distanceFromCamera = 2f;

        private void Awake()
        {
            if (_panelRoot == null)
            {
                _panelRoot = gameObject;
            }
            if (_manager == null)
            {
                _manager = FindFirstObjectByType<RouteProgressManager>();
            }
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            // Suscribirse en Awake (NO en OnEnable): así la suscripción persiste aunque el
            // contenedor visual se oculte. La escucha vive en este componente, que permanece activo.
            if (_manager != null)
            {
                _manager.OnRouteCompleted.AddListener(ShowCompleted);
                _manager.OnRouteTimedOut.AddListener(ShowTimedOut);
                _manager.OnRouteReset.AddListener(HideOnReset);
            }
            else
            {
                Debug.LogWarning("[RouteCompletionPanel] Sin RouteProgressManager: el panel no " +
                                 "podrá mostrarse automáticamente al completar la ruta.", this);
            }

            // Oculto al inicio.
            Hide();
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.OnRouteCompleted.RemoveListener(ShowCompleted);
                _manager.OnRouteTimedOut.RemoveListener(ShowTimedOut);
                _manager.OnRouteReset.RemoveListener(HideOnReset);
            }
        }

        private void LateUpdate()
        {
            // Billboard: rotamos ESTE transform (la raíz del canvas), que sigue activo aunque el
            // contenedor esté oculto. Sólo orientamos mientras el panel es visible.
            if (_faceCamera && IsVisible && _cameraTransform != null)
            {
                Vector3 dir = transform.position - _cameraTransform.position;
                dir.y = 0f; // mantener el panel vertical
                if (dir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        /// <summary>True si el contenedor del panel está visible.</summary>
        private bool IsVisible => _panelRoot != null && _panelRoot.activeInHierarchy;

        public void ShowCompleted(RouteResult result)
        {
            ShowPanel(result, "¡Ruta completada!");
        }

        public void ShowTimedOut(RouteResult result)
        {
            ShowPanel(result, "¡Tiempo agotado!");
        }

        private void ShowPanel(RouteResult result, string status)
        {
            if (_placeInFrontOfCamera && _cameraTransform != null)
            {
                Vector3 forward = _cameraTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();
                transform.position =
                    _cameraTransform.position + forward * _distanceFromCamera;
            }

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
            }

            SetText(_statusText, status);
            string timeInfo = "Tiempo: " + RouteProgressManager.FormatTime(result.ElapsedSeconds);
            if (_manager != null && _manager.HasTimeLimit)
                timeInfo += " / " + RouteProgressManager.FormatTime(_manager.TimeLimit);
            SetText(_timeText, timeInfo);
            SetText(_startHeightText, $"Altura inicial: {result.StartHeight:F1} m");
            SetText(_finishHeightText, $"Altura final: {result.FinishHeight:F1} m");
            SetText(_maxHeightText, $"Altura máxima: {result.MaxHeight:F1} m");
            SetText(_climbedHeightText, $"Altura escalada: {result.ClimbedHeight:F1} m");

            if (_holdsText != null)
            {
                _holdsText.text = $"Agarres usados: {result.HoldsUsed}";
            }
        }

        /// <summary>Oculta el panel.</summary>
        public void Hide()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
        }

        private void HideOnReset()
        {
            Hide();
        }

        /// <summary>
        /// Handler del botón "Reintentar"/"Reset". Asignar al onClick del botón del panel.
        /// Oculta el panel y reinicia las estadísticas para volver a empezar la ruta.
        /// </summary>
        public void OnResetButton()
        {
            Hide();
            if (_manager != null)
            {
                _manager.ResetRoute();
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
