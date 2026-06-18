using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Base común para los checkpoints de ruta. Resuelve de forma robusta si el collider
    /// que entró al trigger pertenece al jugador VR, sin depender exclusivamente de un tag.
    ///
    /// Estrategia de detección (en orden):
    ///   1. Si se asignó <see cref="_playerTransform"/>, se comprueba si el collider que entró
    ///      es ese transform o un descendiente suyo (cubre el caso de colliders en hijos).
    ///   2. Si no, se compara el tag contra <see cref="_playerTag"/> (por defecto "Player",
    ///      que es el tag que ya tiene el PlayerController del rig).
    ///
    /// Requiere un Collider con isTrigger = true en el mismo GameObject. El detector avisa
    /// por consola si el collider no es trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class RouteCheckpointBase : MonoBehaviour
    {
        [Header("Detección del jugador")]
        [SerializeField]
        [Tooltip("Referencia directa al Transform del jugador (ej. PlayerController o el rig root). " +
                 "Si se asigna, se detecta a ese transform o cualquier hijo suyo. Es la forma más " +
                 "robusta; deja vacío para detectar sólo por tag.")]
        protected Transform _playerTransform;

        [SerializeField]
        [Tooltip("Tag a comparar si no se asigna un Transform de jugador. 'Player' es un tag " +
                 "integrado de Unity y el PlayerController del rig ya lo tiene.")]
        protected string _playerTag = "Player";

        [Header("Manager")]
        [SerializeField]
        [Tooltip("Referencia al RouteProgressManager. Si se deja vacío se busca uno en la escena.")]
        protected RouteProgressManager _manager;

        protected virtual void Awake()
        {
            EnsureManager();
            VerifyTriggerCollider();
        }

        protected void EnsureManager()
        {
            if (_manager == null)
            {
                _manager = FindFirstObjectByType<RouteProgressManager>();
                if (_manager == null)
                {
                    Debug.LogWarning($"[{GetType().Name}] No se asignó RouteProgressManager y no se " +
                                     "encontró ninguno en la escena. El checkpoint no funcionará.", this);
                }
            }
        }

        private void VerifyTriggerCollider()
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[{GetType().Name}] El Collider de '{name}' no es trigger. " +
                                 "Marca isTrigger = true para que detecte al jugador.", this);
            }
        }

        /// <summary>
        /// Determina si el collider que entró corresponde al jugador VR.
        /// </summary>
        protected bool IsPlayer(Collider other)
        {
            if (other == null) return false;

            // 1) Detección por referencia de Transform (collider propio o de un hijo del jugador).
            if (_playerTransform != null)
            {
                Transform t = other.transform;
                while (t != null)
                {
                    if (t == _playerTransform)
                    {
                        return true;
                    }
                    t = t.parent;
                }
                // Si hay referencia pero no coincide, igual probamos el tag como respaldo.
            }

            // 2) Detección por tag.
            if (!string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag))
            {
                return true;
            }

            return false;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                OnPlayerEntered(other);
            }
        }

        /// <summary>Se llama cuando un collider del jugador entra al trigger.</summary>
        protected abstract void OnPlayerEntered(Collider other);

#if UNITY_EDITOR
        // Dibuja el volumen del trigger en la Scene view para facilitar el posicionamiento.
        protected virtual void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = GizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.15f);
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }

        /// <summary>Color del gizmo, sobrescrito por cada checkpoint (verde inicio, dorado fin).</summary>
        protected abstract Color GizmoColor { get; }
#endif
    }
}
