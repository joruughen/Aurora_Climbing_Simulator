using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Checkpoint de INICIO de ruta. Colocar al pie de la ruta en la montaña.
    /// Al entrar el jugador (por primera vez o tras un reset) inicia la ruta en el manager:
    /// arranca el timer, guarda la altura inicial y reinicia estadísticas previas.
    ///
    /// Se apoya en <see cref="RouteProgressManager.StartRoute"/>, que internamente ignora
    /// reentradas mientras la ruta ya está en progreso, así que no hay riesgo de reiniciar
    /// el cronómetro si el jugador vuelve a tocar el checkpoint a mitad de ruta.
    /// </summary>
    [AddComponentMenu("Aurora/Route Progress/Route Start Checkpoint")]
    public class RouteStartCheckpoint : RouteCheckpointBase
    {
        [Header("Feedback al iniciar (opcional)")]
        [SerializeField]
        [Tooltip("Feedback visual/sonoro a disparar cuando se inicia la ruta (mensaje 'Ruta iniciada' + sonido).")]
        private RouteCheckpointFeedback _feedback;

        protected override void OnPlayerEntered(Collider other)
        {
            if (_manager == null) return;

            // Sólo reaccionamos si la ruta aún no estaba en progreso, para evitar feedback repetido.
            bool wasNotInProgress = _manager.State != RouteState.InProgress;

            _manager.StartRoute();

            if (wasNotInProgress && _manager.State == RouteState.InProgress && _feedback != null)
            {
                _feedback.Play();
            }
        }

#if UNITY_EDITOR
        protected override Color GizmoColor => new Color(0.1f, 0.9f, 0.2f); // verde
#endif
    }
}
