using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Interaction.Locomotion
{
    /// <summary>
    /// Adds swing momentum, dramatic fall physics, and landing/release sounds
    /// to the climbing system. Purely additive — does not modify ClimbingLocomotor.
    ///
    /// On release: injects accumulated swing velocity into FirstPersonLocomotor
    /// and temporarily boosts gravity for a dramatic fall feel. Plays a release
    /// sound. On landing: plays a thud sound and restores normal gravity.
    /// </summary>
    [RequireComponent(typeof(ClimbingLocomotor))]
    public class ClimbPendulumController : MonoBehaviour
    {
        [Header("Pendulum Physics")]
        [Tooltip("Gravity multiplier for arc accumulation. 1 = realistic, higher = more dramatic.")]
        [SerializeField] private float swingStrength = 1.5f;

        [Tooltip("Gravity magnitude used internally for swing accumulation.")]
        [SerializeField] private float gravityStrength = 9.81f;

        [Tooltip("Exponential damping per second on accumulated swing. 0 = none, ~0.5 = comfortable.")]
        [SerializeField, Range(0f, 1f)] private float damping = 0.3f;

        [Tooltip("Max speed (m/s) the swing can accumulate while grabbing.")]
        [SerializeField] private float maxSwingSpeed = 6f;

        [Header("Release")]
        [Tooltip("Fraction of swing velocity injected into the locomotor on release.")]
        [SerializeField, Range(0f, 2f)] private float releaseVelocityMultiplier = 1.0f;

        [Tooltip("Maximum launch speed (m/s) on release.")]
        [SerializeField] private float maxReleaseVelocity = 5f;

        [Tooltip("Minimum swing speed (m/s) required to apply launch on release.")]
        [SerializeField] private float minReleaseVelocity = 0.3f;

        [Header("Dramatic Fall")]
        [Tooltip("Gravity multiplier applied while falling after release (higher = faster fall).")]
        [SerializeField] private float fallGravityMultiplier = 3.5f;

        [Tooltip("Normal gravity factor to restore on landing.")]
        [SerializeField] private float normalGravityFactor = 1f;

        [Tooltip("Minimum fall speed (m/s downward) to trigger landing sound.")]
        [SerializeField] private float landingSpeedThreshold = 2f;

        [Header("Constraints")]
        [Tooltip("Minimum anchor-to-player distance before pendulum accumulation starts.")]
        [SerializeField] private float minPendulumDistance = 0.3f;

        [Tooltip("When true, only horizontal swing is accumulated (reduces vertical launch).")]
        [SerializeField] private bool comfortMode = false;

        [Header("Sounds — Release")]
        [Tooltip("Clips played randomly on release. Assign Climbing_Holds_Release_01..05.")]
        [SerializeField] private AudioClip[] releaseSounds;

        [Tooltip("Volume for the release sound.")]
        [SerializeField, Range(0f, 1f)] private float releaseVolume = 0.6f;

        [Header("Sounds — Landing")]
        [Tooltip("Clips played randomly on landing. Assign WalkingStick_Floor_Thud_01..05.")]
        [SerializeField] private AudioClip[] landingSounds;

        [Tooltip("Volume for the landing thud.")]
        [SerializeField, Range(0f, 1f)] private float landingVolume = 0.9f;

        [Header("Feature Toggle")]
        [Tooltip("Uncheck to disable without removing the component.")]
        [SerializeField] private bool pendulumEnabled = true;

        // ── runtime refs ───────────────────────────────────────────────────────
        private ClimbingLocomotor _locomotor;
        private FirstPersonLocomotor _firstPersonLocomotor;
        private AudioSource _audioSource;

        // ── state ──────────────────────────────────────────────────────────────
        private Vector3 _swingVelocity;
        private Vector3 _anchorPosition;
        private bool _hasAnchor;
        private bool _isFalling;          // true while we boosted gravity post-release
        private bool _wasGrounded;

        private List<ClimbingLocomotionBroadcaster> _broadcasters = new List<ClimbingLocomotionBroadcaster>();

        // ── lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _locomotor = GetComponent<ClimbingLocomotor>();

            // AudioSource for non-spatial 2D sounds (wind in ears, thud felt in body)
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;   // 2D — heard in both ears equally
            _audioSource.playOnAwake = false;
        }

        private void Start()
        {
            _broadcasters.Clear();
            foreach (var b in FindObjectsByType<ClimbingLocomotionBroadcaster>(FindObjectsSortMode.None))
                _broadcasters.Add(b);

            _firstPersonLocomotor = FindFirstObjectByType<FirstPersonLocomotor>();
        }

        private void OnEnable()
        {
            _locomotor.WhenClimbingStarted.AddListener(OnClimbingStarted);
            _locomotor.WhenClimbingEnded.AddListener(OnClimbingEnded);
        }

        private void OnDisable()
        {
            _locomotor.WhenClimbingStarted.RemoveListener(OnClimbingStarted);
            _locomotor.WhenClimbingEnded.RemoveListener(OnClimbingEnded);
        }

        // ── climbing events ────────────────────────────────────────────────────

        private void OnClimbingStarted()
        {
            _swingVelocity = Vector3.zero;
            _hasAnchor = TryFindActiveAnchor(out _anchorPosition);

            // If we grab again mid-fall, restore normal gravity immediately.
            if (_isFalling) RestoreGravity();
        }

        private void OnClimbingEnded(Vector3 climbVelocity)
        {
            if (!pendulumEnabled) { ResetState(); return; }

            Vector3 swingRelease = _swingVelocity * releaseVelocityMultiplier;
            Vector3 climbRelease = climbVelocity * releaseVelocityMultiplier;
            Vector3 release = swingRelease.magnitude >= climbRelease.magnitude ? swingRelease : climbRelease;

            bool hasLaunch = release.magnitude >= minReleaseVelocity;

            if (hasLaunch)
            {
                if (release.magnitude > maxReleaseVelocity)
                    release = release.normalized * maxReleaseVelocity;

                PlayRandom(releaseSounds, releaseVolume);

                // Boost gravity for dramatic fall.
                if (_firstPersonLocomotor != null)
                    _firstPersonLocomotor.GravityFactor = fallGravityMultiplier;
                _isFalling = true;

                // Inject velocity one frame after EnableMovement resets it to zero.
                StartCoroutine(InjectVelocityNextFrame(release));
            }

            ResetState();
        }

        // ── per-frame ──────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            // Accumulate swing while climbing.
            if (pendulumEnabled && _locomotor.IsClimbing && _hasAnchor)
            {
                TryFindActiveAnchor(out _anchorPosition);
                AccumulateSwing();
            }

            // Detect landing: grounded this frame but airborne last frame.
            if (_isFalling && _firstPersonLocomotor != null)
            {
                bool grounded = _firstPersonLocomotor.IsGrounded;
                if (grounded && !_wasGrounded)
                    OnLanded();
                _wasGrounded = grounded;
            }
        }

        private void OnLanded()
        {
            RestoreGravity();

            // Only play thud if we were falling fast enough.
            float fallSpeed = _firstPersonLocomotor != null
                ? -_firstPersonLocomotor.Velocity.y
                : landingSpeedThreshold;

            if (fallSpeed >= landingSpeedThreshold)
                PlayRandom(landingSounds, landingVolume);
        }

        // ── pendulum accumulation ──────────────────────────────────────────────

        private void AccumulateSwing()
        {
            Vector3 r = transform.position - _anchorPosition;
            float ropeLength = r.magnitude;
            if (ropeLength < minPendulumDistance) return;

            Vector3 rHat = r / ropeLength;
            Vector3 gravity = Vector3.down * gravityStrength;
            Vector3 gravTangent = gravity - Vector3.Dot(gravity, rHat) * rHat;

            if (comfortMode)
                gravTangent = Vector3.ProjectOnPlane(gravTangent, Vector3.up);

            float dt = Time.deltaTime;
            _swingVelocity += gravTangent * (swingStrength * dt);
            _swingVelocity *= Mathf.Max(0f, 1f - damping * dt);

            if (comfortMode)
                _swingVelocity = Vector3.ProjectOnPlane(_swingVelocity, Vector3.up);

            float speed = _swingVelocity.magnitude;
            if (speed > maxSwingSpeed)
                _swingVelocity = _swingVelocity * (maxSwingSpeed / speed);
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private IEnumerator InjectVelocityNextFrame(Vector3 release)
        {
            yield return null;
            if (_firstPersonLocomotor != null)
                _firstPersonLocomotor.Velocity += release;
        }

        private void RestoreGravity()
        {
            if (_firstPersonLocomotor != null)
                _firstPersonLocomotor.GravityFactor = normalGravityFactor;
            _isFalling = false;
        }

        private void PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0 || _audioSource == null) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) _audioSource.PlayOneShot(clip, volume);
        }

        private bool TryFindActiveAnchor(out Vector3 anchor)
        {
            foreach (var b in _broadcasters)
            {
                if (b != null && b.isActiveAndEnabled)
                {
                    anchor = b.transform.position;
                    return true;
                }
            }
            anchor = _hasAnchor ? _anchorPosition : transform.position + Vector3.up;
            return false;
        }

        private void ResetState()
        {
            _hasAnchor = false;
            _swingVelocity = Vector3.zero;
        }

        // ── debug gizmos ───────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !_hasAnchor) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_anchorPosition, 0.05f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_anchorPosition, transform.position);
            Gizmos.color = Color.green;
            Vector3 velEnd = transform.position + _swingVelocity * 0.3f;
            Gizmos.DrawLine(transform.position, velEnd);
            Gizmos.DrawSphere(velEnd, 0.03f);
        }
#endif
    }
}
