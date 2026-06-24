using System;
using UnityEngine;
using UnityEngine.Events;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Estado de la ruta de escalada.
    /// </summary>
    public enum RouteState
    {
        NotStarted,
        InProgress,
        Completed
    }

    /// <summary>
    /// Resumen inmutable de una ruta completada. Se pasa a la UI al finalizar.
    /// </summary>
    public struct RouteResult
    {
        public float ElapsedSeconds;
        public float StartHeight;
        public float FinishHeight;
        public float MaxHeight;
        /// <summary>Altura escalada aproximada (max alcanzado - altura inicial).</summary>
        public float ClimbedHeight;
        public int HoldsUsed;
    }

    /// <summary>
    /// Sistema central de progreso de ruta.
    ///
    /// Responsabilidades:
    ///  - Llevar el estado de la ruta (NotStarted / InProgress / Completed).
    ///  - Medir el tiempo de compleción.
    ///  - Registrar altura inicial, actual, máxima y final del jugador.
    ///  - Exponer métodos públicos claros: StartRoute / FinishRoute / ResetRoute / GetElapsedTime.
    ///  - Evitar reinicios o finalizaciones múltiples mediante guardas de estado.
    ///
    /// La altura se lee de <see cref="_heightSource"/> (por defecto la cámara/HMD del jugador),
    /// para que las estadísticas reflejen la altura real de la cabeza del escalador.
    ///
    /// Diseñado para ser reutilizable: cualquier número de rutas puede usar el mismo manager;
    /// los checkpoints sólo llaman a StartRoute() / FinishRoute().
    /// </summary>
    [DisallowMultipleComponent]
    public class RouteProgressManager : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField]
        [Tooltip("Transform cuya altura (Y mundial) se usa para las estadísticas. " +
                 "Asignar la cámara / CenterEyeAnchor (HMD). Si se deja vacío se intenta " +
                 "auto-resolver con Camera.main al iniciar.")]
        private Transform _heightSource;

        [Header("Tiempo límite")]
        [SerializeField]
        [Tooltip("Si es > 0, la ruta se pierde al pasar este tiempo. Se lee de DifficultySettings al iniciar.")]
        private float _timeLimit;

        [Header("Eventos (opcionales, para UI / feedback)")]
        [Tooltip("Se invoca cuando la ruta inicia.")]
        public UnityEvent OnRouteStarted = new UnityEvent();

        [Tooltip("Se invoca cuando la ruta se completa, con el resumen de estadísticas.")]
        public RouteResultEvent OnRouteCompleted = new RouteResultEvent();

        [Tooltip("Se invoca cuando se acaba el tiempo, con el resumen de estadísticas.")]
        public RouteResultEvent OnRouteTimedOut = new RouteResultEvent();

        [Tooltip("Se invoca cuando la ruta se reinicia.")]
        public UnityEvent OnRouteReset = new UnityEvent();

        // --- Estado interno ---
        private RouteState _state = RouteState.NotStarted;
        private float _startTime;          // Time.time al iniciar
        private float _endTime;            // Time.time al finalizar
        private float _startHeight;
        private float _maxHeight;
        private float _finishHeight;
        private int _holdsUsed;

        // --- Propiedades públicas de sólo lectura ---
        public RouteState State => _state;
        public float StartHeight => _startHeight;
        public float MaxHeight => _maxHeight;
        public float FinishHeight => _finishHeight;
        public int HoldsUsed => _holdsUsed;

        public float TimeLimit => _timeLimit;
        public float RemainingTime => _timeLimit > 0f ? Mathf.Max(0f, _timeLimit - GetElapsedTime()) : float.MaxValue;
        public bool HasTimeLimit => _timeLimit > 0f;

        /// <summary>Altura actual del jugador (Y mundial del height source).</summary>
        public float CurrentHeight => _heightSource != null ? _heightSource.position.y : 0f;

        /// <summary>Altura escalada aproximada respecto al punto de inicio.</summary>
        public float ClimbedHeight => Mathf.Max(0f, _maxHeight - _startHeight);

        private void Awake()
        {
            if (_heightSource == null)
            {
                // Fallback robusto: usar la cámara principal (CenterEyeAnchor está tagueada MainCamera).
                if (Camera.main != null)
                {
                    _heightSource = Camera.main.transform;
                    Debug.Log("[RouteProgressManager] Height source no asignado; " +
                              "usando Camera.main (" + _heightSource.name + ").", this);
                }
                else
                {
                    Debug.LogWarning("[RouteProgressManager] No hay Height Source asignado y no se " +
                                     "encontró Camera.main. Las estadísticas de altura serán 0. " +
                                     "Asigna el CenterEyeAnchor en el Inspector.", this);
                }
            }
        }

        private void Update()
        {
            if (_state != RouteState.InProgress) return;

            if (_heightSource != null)
            {
                float y = _heightSource.position.y;
                if (y > _maxHeight)
                    _maxHeight = y;
            }

            if (_timeLimit > 0f && GetElapsedTime() >= _timeLimit)
            {
                _endTime = _startTime + _timeLimit;
                _finishHeight = CurrentHeight;
                if (_finishHeight > _maxHeight) _maxHeight = _finishHeight;
                _state = RouteState.Completed;

                RouteResult result = BuildResult();
                Debug.Log($"[RouteProgressManager] TIEMPO AGOTADO en {result.ElapsedSeconds:F2}s.", this);
                OnRouteTimedOut.Invoke(result);
            }
        }

        /// <summary>
        /// Inicia la ruta: arranca el timer, guarda la altura inicial y reinicia estadísticas.
        /// No hace nada si la ruta ya está en progreso (evita reinicios accidentales por
        /// reentradas al checkpoint inicial).
        /// </summary>
        [ContextMenu("TEST ▸ Start Route")]
        public void StartRoute()
        {
            if (_state == RouteState.InProgress)
            {
                // Ya iniciada: ignorar para no reiniciar el cronómetro a mitad de ruta.
                return;
            }

            _state = RouteState.InProgress;
            _startTime = Time.time;
            _endTime = 0f;
            _holdsUsed = 0;
            _timeLimit = DifficultySettings.GetTimeLimit();

            _startHeight = CurrentHeight;
            _maxHeight = _startHeight;
            _finishHeight = _startHeight;

            Debug.Log($"[RouteProgressManager] Ruta INICIADA. Altura inicial = {_startHeight:F2} m.", this);
            OnRouteStarted.Invoke();
        }

        /// <summary>
        /// Finaliza la ruta: detiene el timer, calcula altura final / máxima / diferencia,
        /// y dispara el evento de compleción. Sólo finaliza si la ruta está en progreso
        /// (no se puede completar sin haber iniciado, ni completar dos veces).
        /// </summary>
        [ContextMenu("TEST ▸ Finish Route")]
        public void FinishRoute()
        {
            if (_state != RouteState.InProgress)
            {
                // No iniciada o ya completada: ignorar.
                return;
            }

            _endTime = Time.time;
            _finishHeight = CurrentHeight;
            // Asegurar que la altura máxima contemple la final.
            if (_finishHeight > _maxHeight)
            {
                _maxHeight = _finishHeight;
            }

            _state = RouteState.Completed;

            RouteResult result = BuildResult();
            Debug.Log($"[RouteProgressManager] Ruta COMPLETADA en {result.ElapsedSeconds:F2}s. " +
                      $"Escalado ≈ {result.ClimbedHeight:F2} m (de {result.StartHeight:F2} a " +
                      $"máx {result.MaxHeight:F2}).", this);
            OnRouteCompleted.Invoke(result);
        }

        /// <summary>
        /// Reinicia la ruta a NotStarted y limpia todas las estadísticas, permitiendo
        /// volver a iniciarla desde el checkpoint inicial.
        /// </summary>
        [ContextMenu("TEST ▸ Reset Route")]
        public void ResetRoute()
        {
            _state = RouteState.NotStarted;
            _startTime = 0f;
            _endTime = 0f;
            _startHeight = 0f;
            _maxHeight = 0f;
            _finishHeight = 0f;
            _holdsUsed = 0;

            Debug.Log("[RouteProgressManager] Ruta REINICIADA.", this);
            OnRouteReset.Invoke();
        }

        /// <summary>
        /// Tiempo transcurrido en segundos. Mientras está en progreso devuelve el tiempo vivo;
        /// una vez completada devuelve el tiempo total congelado; si no ha iniciado devuelve 0.
        /// </summary>
        public float GetElapsedTime()
        {
            switch (_state)
            {
                case RouteState.InProgress:
                    return Time.time - _startTime;
                case RouteState.Completed:
                    return _endTime - _startTime;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Registra el uso de un agarre. Opcional: lo llaman los climbables si se integra
        /// el conteo de agarres. Sólo cuenta mientras la ruta está en progreso.
        /// </summary>
        public void RegisterHoldUsed()
        {
            if (_state == RouteState.InProgress)
            {
                _holdsUsed++;
            }
        }

        /// <summary>Construye el resumen de la ruta con los valores actuales.</summary>
        public RouteResult BuildResult()
        {
            return new RouteResult
            {
                ElapsedSeconds = GetElapsedTime(),
                StartHeight = _startHeight,
                FinishHeight = _finishHeight,
                MaxHeight = _maxHeight,
                ClimbedHeight = ClimbedHeight,
                HoldsUsed = _holdsUsed
            };
        }

        /// <summary>
        /// Formatea un tiempo en segundos como mm:ss.cs (minutos:segundos.centésimas).
        /// Helper estático reutilizable por la UI.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = (int)(seconds / 60f);
            int secs = (int)(seconds % 60f);
            int centis = (int)((seconds * 100f) % 100f);
            return $"{minutes:00}:{secs:00}.{centis:00}";
        }
    }

    /// <summary>UnityEvent serializable que transporta un <see cref="RouteResult"/>.</summary>
    [Serializable]
    public class RouteResultEvent : UnityEvent<RouteResult> { }
}
