using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab; // Necesario para el Hand Grab Interactor

public class ClimbStressRelease : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El interactor de agarre de esta mano.")]
    [SerializeField] private HandGrabInteractor grabInteractor; // <-- Cambiado aquí

    [Tooltip("El transform crudo que sigue al control físico.")]
    [SerializeField] private Transform physicalController;

    [Tooltip("El transform de la mano virtual que se queda pegada en la pared.")]
    [SerializeField] private Transform virtualHand;

    [Tooltip("El control a vibrar (LTouch o RTouch).")]
    [SerializeField] private OVRInput.Controller controllerNode;

    [Header("Configuración de Distancia")]
    [Tooltip("Distancia (en metros) a la que empieza a vibrar el control.")]
    [SerializeField] private float minDistanceForVibration = 0.15f;

    [Tooltip("Distancia (en metros) a la que el control se suelta automáticamente.")]
    [SerializeField] private float maxDistanceForRelease = 0.35f;

    [Header("Haptics")]
    [SerializeField, Range(0f, 1f)] private float maxVibrationAmplitude = 1f;

    private bool isGrabbing = false;

    private void OnEnable()
    {
        if (grabInteractor != null)
        {
            grabInteractor.WhenStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (grabInteractor != null)
        {
            grabInteractor.WhenStateChanged -= HandleStateChanged;
        }
        StopVibration();
    }

    private void HandleStateChanged(InteractorStateChangeArgs args)
    {
        if (args.NewState == InteractorState.Select)
        {
            isGrabbing = true;
        }
        else if (args.PreviousState == InteractorState.Select)
        {
            isGrabbing = false;
            StopVibration();
        }
    }

    private void Update()
    {
        if (!isGrabbing) return;

        float distance = Vector3.Distance(physicalController.position, virtualHand.position);

        if (distance > maxDistanceForRelease)
        {
            ForceRelease();
            return;
        }

        if (distance > minDistanceForVibration)
        {
            float t = (distance - minDistanceForVibration) / (maxDistanceForRelease - minDistanceForVibration);
            float amplitude = Mathf.Lerp(0.1f, maxVibrationAmplitude, t);

            OVRInput.SetControllerVibration(1f, amplitude, controllerNode);
        }
        else
        {
            StopVibration();
        }
    }

    private void ForceRelease()
    {
        isGrabbing = false;
        StopVibration();

        // Suelta la presa
        grabInteractor.Unselect();
    }

    private void StopVibration()
    {
        OVRInput.SetControllerVibration(0, 0, controllerNode);
    }
}