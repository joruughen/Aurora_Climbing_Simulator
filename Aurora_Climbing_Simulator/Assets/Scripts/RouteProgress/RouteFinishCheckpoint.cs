using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Checkpoint FINAL de ruta. Colocar en la cima de la ruta en la montaña.
    /// Sólo finaliza la ruta si ya fue iniciada (el manager ignora FinishRoute si el estado
    /// no es InProgress, evitando finalizaciones múltiples o sin haber empezado).
    ///
    /// Al finalizar, el manager dispara OnRouteCompleted, que el panel de compleción escucha
    /// para mostrarse. Opcionalmente este checkpoint también puede reproducir feedback.
    /// </summary>
    [AddComponentMenu("Aurora/Route Progress/Route Finish Checkpoint")]
    public class RouteFinishCheckpoint : RouteCheckpointBase
    {
        [Header("Feedback al finalizar (opcional)")]
        [SerializeField]
        [Tooltip("Feedback visual/sonoro a disparar cuando se completa la ruta.")]
        private RouteCheckpointFeedback _feedback;

        protected override void OnPlayerEntered(Collider other)
        {
            if (_manager == null) return;

            // Sólo completamos (y damos feedback) si la ruta estaba realmente en progreso.
            bool wasInProgress = _manager.State == RouteState.InProgress;

            _manager.FinishRoute();

            if (wasInProgress && _manager.State == RouteState.Completed && _feedback != null)
            {
                _feedback.Play();
            }
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(1f, 0.82f, 0.1f); // dorado
#endif
    }
}
