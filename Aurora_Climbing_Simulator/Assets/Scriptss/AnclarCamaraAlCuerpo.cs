using UnityEngine;

public class AnclarCamaraAlCuerpo : MonoBehaviour
{
    [Header("Componentes")]
    public Transform ovrCameraRig; // El padre absoluto (la cámara)

    private CharacterController cc;
    private Vector3 offsetLocalInicial;

    void Start()
    {
        // Conseguimos el Character Controller del PlayerController
        cc = GetComponent<CharacterController>();

        // Guardamos la posición inicial de fábrica
        offsetLocalInicial = transform.localPosition;
    }

    void LateUpdate()
    {
        if (ovrCameraRig == null) return;

        // Calculamos el desfase local entre el cuerpo y la cámara
        Vector3 desfaseLocal = transform.localPosition - offsetLocalInicial;

        // Ignoramos el eje Y para no romper caídas ni pendientes
        desfaseLocal.y = 0;

        // Si el personaje chocó y la cámara intentó seguir de largo
        if (desfaseLocal.magnitude > 0.001f)
        {
            // Convertimos el desfase al espacio del mundo
            Vector3 desfaseMundo = ovrCameraRig.TransformDirection(desfaseLocal);

            // Movemos la cámara hacia atrás para alinearla con el cuerpo bloqueado
            ovrCameraRig.position += desfaseMundo;

            // ¡EL PARCHE CRUCIAL!: Apagamos el componente físico para evitar que salga disparado
            if (cc != null) cc.enabled = false;

            // Reseteamos la posición del cuerpo de forma segura
            transform.localPosition = new Vector3(offsetLocalInicial.x, transform.localPosition.y, offsetLocalInicial.z);

            // Volvemos a encender las físicas instantáneamente
            if (cc != null) cc.enabled = true;
        }
    }
}