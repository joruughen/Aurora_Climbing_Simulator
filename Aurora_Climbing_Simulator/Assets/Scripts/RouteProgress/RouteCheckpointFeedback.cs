using System.Collections;
using TMPro;
using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Feedback visual + sonoro reutilizable para los checkpoints (cartel flotante + sonido).
    ///
    /// Muestra un cartel world-space con un mensaje temporal (ej. "¡Ruta iniciada!") y reproduce
    /// un sonido por un AudioSource. Todo es opcional: si falta alguna referencia se omite esa parte.
    ///
    /// El cartel aparece frente al jugador, hace billboard hacia la cámara mientras está visible,
    /// se mantiene <see cref="_messageDuration"/> segundos y se oculta.
    ///
    /// IMPORTANTE: este componente vive en un GameObject SIEMPRE activo y togglea un contenedor
    /// HIJO (<see cref="_messageRoot"/>), nunca su propio GameObject, para no perder corrutinas.
    /// </summary>
    [AddComponentMenu("Aurora/Route Progress/Route Checkpoint Feedback")]
    public class RouteCheckpointFeedback : MonoBehaviour
    {
        [Header("Cartel / mensaje visual (opcional)")]
        [SerializeField]
        [Tooltip("Contenedor (hijo) del cartel que se activa/desactiva. Debe ser un hijo, NO este GameObject.")]
        private GameObject _messageRoot;

        [SerializeField]
        [Tooltip("Texto TMP donde se escribe el mensaje.")]
        private TMP_Text _messageText;

        [SerializeField]
        [Tooltip("Texto a mostrar, ej. '¡Ruta iniciada!'.")]
        private string _message = "¡Ruta iniciada!";

        [SerializeField]
        [Tooltip("Segundos que el cartel permanece visible.")]
        private float _messageDuration = 2.5f;

        [Header("Posicionamiento del cartel")]
        [SerializeField]
        [Tooltip("Si está activo, al mostrarse el cartel se reubica frente a la cámara del jugador.")]
        private bool _placeInFrontOfCamera = true;

        [SerializeField]
        [Tooltip("Distancia (m) frente a la cámara a la que aparece el cartel.")]
        private float _distanceFromCamera = 2.5f;

        [SerializeField]
        [Tooltip("Cámara/transform de referencia. Si se deja vacío se usa Camera.main (CenterEyeAnchor).")]
        private Transform _cameraTransform;

        [Header("Sonido (opcional)")]
        [SerializeField]
        [Tooltip("AudioSource a reproducir. Si se asigna un clip abajo se usa PlayOneShot; " +
                 "si no, se usa el clip propio del AudioSource con Play().")]
        private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Clip opcional para PlayOneShot. Si se deja vacío se usa el clip del AudioSource.")]
        private AudioClip _clip;

        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
            // Cartel oculto al inicio.
            if (_messageRoot != null)
            {
                _messageRoot.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // Billboard: mientras el cartel está visible, mirar a la cámara (vertical).
            if (_messageRoot != null && _messageRoot.activeInHierarchy && _cameraTransform != null)
            {
                Vector3 dir = _messageRoot.transform.position - _cameraTransform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    _messageRoot.transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        /// <summary>Dispara el feedback (cartel + sonido).</summary>
        public void Play()
        {
            PlaySound();
            ShowMessage();
        }

        private void PlaySound()
        {
            if (_audioSource == null) return;

            if (_clip != null)
            {
                _audioSource.PlayOneShot(_clip);
            }
            else if (_audioSource.clip != null)
            {
                _audioSource.Play();
            }
        }

        private void ShowMessage()
        {
            if (_messageRoot == null) return;

            if (_messageText != null)
            {
                _messageText.text = _message;
            }

            // Reubicar frente al jugador antes de mostrar.
            if (_placeInFrontOfCamera && _cameraTransform != null)
            {
                Vector3 forward = _cameraTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();
                _messageRoot.transform.position =
                    _cameraTransform.position + forward * _distanceFromCamera;
            }

            _messageRoot.SetActive(true);

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
            }
            if (isActiveAndEnabled)
            {
                _hideRoutine = StartCoroutine(HideAfterDelay());
            }
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_messageDuration);
            if (_messageRoot != null)
            {
                _messageRoot.SetActive(false);
            }
            _hideRoutine = null;
        }
    }
}
