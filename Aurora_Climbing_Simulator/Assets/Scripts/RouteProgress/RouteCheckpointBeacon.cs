using UnityEngine;

namespace Aurora.RouteProgress
{
    /// <summary>
    /// Marcador visual (haz de luz) de un checkpoint. Hace que el objeto gire lentamente y
    /// "pulse" (sube/baja la opacidad/escala) para llamar la atención en VR. Es puramente
    /// estético y opcional: se puede borrar sin afectar la lógica del checkpoint.
    /// </summary>
    [AddComponentMenu("Aurora/Route Progress/Route Checkpoint Beacon")]
    public class RouteCheckpointBeacon : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Velocidad de giro en grados por segundo (eje Y).")]
        private float _spinSpeed = 30f;

        [SerializeField]
        [Tooltip("Amplitud del pulso de opacidad (0 = sin pulso).")]
        private float _pulseAmount = 0.15f;

        [SerializeField]
        [Tooltip("Velocidad del pulso.")]
        private float _pulseSpeed = 2f;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Color _baseColor;
        private bool _hasColor;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _mpb = new MaterialPropertyBlock();
                // Detectar la propiedad de color disponible en el material.
                var mat = _renderer.sharedMaterial;
                if (mat.HasProperty("_TintColor")) { _baseColor = mat.GetColor("_TintColor"); _hasColor = true; }
                else if (mat.HasProperty("_Color")) { _baseColor = mat.GetColor("_Color"); _hasColor = true; }
                else if (mat.HasProperty("_BaseColor")) { _baseColor = mat.GetColor("_BaseColor"); _hasColor = true; }
            }
        }

        private void Update()
        {
            // Giro.
            if (_spinSpeed != 0f)
            {
                transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.Self);
            }

            // Pulso de opacidad (sin instanciar material, vía MaterialPropertyBlock).
            if (_hasColor && _pulseAmount > 0f && _renderer != null && _mpb != null)
            {
                float a = Mathf.Clamp01(_baseColor.a + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount);
                Color c = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);

                _renderer.GetPropertyBlock(_mpb);
                // Setear la(s) propiedad(es) de color que el material entienda.
                var mat = _renderer.sharedMaterial;
                if (mat.HasProperty("_TintColor")) _mpb.SetColor("_TintColor", c);
                if (mat.HasProperty("_Color")) _mpb.SetColor("_Color", c);
                if (mat.HasProperty("_BaseColor")) _mpb.SetColor("_BaseColor", c);
                _renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
