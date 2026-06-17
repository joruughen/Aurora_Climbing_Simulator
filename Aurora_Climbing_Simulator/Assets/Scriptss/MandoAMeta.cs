using UnityEngine;
using System;
using Oculus.Interaction.Locomotion;

// Al usar "ILocomotionEventBroadcaster", le decimos a Meta que este script es un mando oficial
public class MandoAMeta : MonoBehaviour, ILocomotionEventBroadcaster
{
    public event Action<LocomotionEvent> WhenLocomotionPerformed = delegate { };

    public Transform cabeza; // Arrastrarás tu CenterEyeAnchor aquí
    public float velocidad = 3f;

    void Update()
    {
        // Leer joystick izquierdo
        Vector2 joystick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (joystick.sqrMagnitude > 0.01f)
        {
            Vector3 adelante = cabeza.forward;
            Vector3 derecha = cabeza.right;
            adelante.y = 0; derecha.y = 0; // Evitar volar

            Vector3 vectorMov = (adelante.normalized * joystick.y + derecha.normalized * joystick.x) * velocidad;

            // Enviar un evento de VELOCIDAD al FirstPersonLocomotor
            LocomotionEvent eventoMeta = new LocomotionEvent(0, vectorMov, LocomotionEvent.TranslationType.Velocity);
            WhenLocomotionPerformed.Invoke(eventoMeta);
        }
    }
}