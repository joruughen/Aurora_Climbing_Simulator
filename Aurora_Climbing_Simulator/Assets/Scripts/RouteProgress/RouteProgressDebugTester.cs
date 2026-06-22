using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Tester opcional para validar el flujo de ruta en Play Mode SIN necesidad de VR ni de
    /// caminar físicamente hasta los checkpoints (útil porque el Meta XR Simulator puede no estar
    /// disponible). Dispara los métodos del manager con teclas:
    ///
    ///   • Tecla 1  →  StartRoute()   (inicia ruta, arranca timer)
    ///   • Tecla 2  →  FinishRoute()  (finaliza ruta, muestra el panel)
    ///   • Tecla 3  →  ResetRoute()   (reinicia, oculta el panel)
    ///
    /// Es 100% opcional y se puede borrar sin afectar al sistema. Sólo actúa en el Editor o en
    /// builds de desarrollo. Usa el Input Manager clásico (el proyecto tiene activeInputHandler=Both).
    ///
    /// NOTA: como en Play Mode sin VR la cámara no sube, la "altura escalada" saldrá ~0; esto sólo
    /// valida la lógica de estados, timer, panel y reset. La altura real se prueba en el headset.
    /// </summary>
    [AddComponentMenu("Aurora/Route Progress/Route Progress Debug Tester")]
    public class RouteProgressDebugTester : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Manager a controlar. Si se deja vacío se busca en la escena / en este GameObject.")]
        private RouteProgressManager _manager;

        [SerializeField]
        [Tooltip("Si está activo, imprime el tiempo transcurrido en consola mientras la ruta corre.")]
        private bool _logElapsedWhileRunning = false;

        private void Awake()
        {
            if (_manager == null)
            {
                _manager = GetComponent<RouteProgressManager>();
            }
            if (_manager == null)
            {
                _manager = FindFirstObjectByType<RouteProgressManager>();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (_manager == null) return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("[RouteProgressDebugTester] Tecla 1 → StartRoute()");
                _manager.StartRoute();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Debug.Log("[RouteProgressDebugTester] Tecla 2 → FinishRoute()");
                _manager.FinishRoute();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Debug.Log("[RouteProgressDebugTester] Tecla 3 → ResetRoute()");
                _manager.ResetRoute();
            }

            if (_logElapsedWhileRunning && _manager.State == RouteState.InProgress)
            {
                Debug.Log($"[RouteProgressDebugTester] Tiempo: {RouteProgressManager.FormatTime(_manager.GetElapsedTime())} " +
                          $"| Altura actual: {_manager.CurrentHeight:F2} m");
            }
        }
#endif
    }
}
