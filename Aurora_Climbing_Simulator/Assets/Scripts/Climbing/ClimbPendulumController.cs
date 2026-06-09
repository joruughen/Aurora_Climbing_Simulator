// ClimbPendulumController.cs
// Adds a pendulum/swinging effect while the player grabs a Climbable.
//
// How it integrates with Meta XR climbing:
//   ClimbingLocomotor exposes WhenClimbingStarted/WhenClimbingEnded events.
//   We subscribe to those to know when climbing begins and ends.
//   The active grab point (the hand's world-space position) is polled via the
//   ClimbingLocomotor's internal state — since those fields are private, we read
//   the hand anchor transforms exposed by OVRCameraRig instead and pick the one
//   that is currently grabbing.
//
// Movement injection strategy:
//   We do NOT call CharacterController.Move or set transform.position directly.
//   Instead we add a small Relative LocomotionEvent each frame through the same
//   LocomotionEventsConnection that the ClimbingLocomotor uses.
//   This keeps all collision-response, wall-penetration, and sync logic intact.
//
// Pendulum model (per frame while grabbing):
//   1. rope    = playerFeet.position - anchorPoint
//   2. radial  = normalize(rope)          // points away from anchor
//   3. tangent = Vector3.Cross(Vector3.Cross(radial, _swingVelocity), radial).normalized
//                  -- tangent in the plane defined by radial and current swing direction
//   4. gravity contribution on tangent: g_t = dot(gravity, tangent) * dt
//   5. swing velocity update:            _swingVel += (g_t + swingStrength*lateralInput) * dt
//   6. damping:                          _swingVel *= (1 - damping*dt)
//   7. clamp:                            _swingVel  = clamp(_swingVel, -maxSwingSpeed, +maxSwingSpeed)
//   8. displacement:                     delta = tangent * _swingVel * dt
//   The ClimbingLocomotor will apply its own hand-delta on top of this.
//
// On release:
//   Applies _swingVel * releaseMultiplier (clamped) to FirstPersonLocomotor.Velocity.

using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction.Locomotion;

namespace AuroraClimbing
{
    public class ClimbPendulumController : MonoBehaviour
    {
        // ── References ──────────────────────────────────────────────────────────
        [Header("Required References")]
        [Tooltip("The ClimbingLocomotor in the scene. " +
                 "Path: OVRCameraRig/OVRInteractionComprehensive/Locomotor/ClimbingLocomotor")]
        [SerializeField] private ClimbingLocomotor _climbingLocomotor;

        [Tooltip("The FirstPersonLocomotor. Used to inject release velocity. " +
                 "Path: OVRCameraRig/OVRInteractionComprehensive/Locomotor/PlayerController")]
        [SerializeField] private FirstPersonLocomotor _firstPersonLocomotor;

        [Tooltip("The LocomotionEventsConnection that feeds the FirstPersonLocomotor. " +
                 "Path: OVRCameraRig/OVRInteractionComprehensive/Locomotor")]
        [SerializeField] private LocomotionEventsConnection _locomotionConnection;

        [Tooltip("Transform whose world position is used as the player body root for pendulum math. " +
                 "Path: OVRCameraRig/OVRInteractionComprehensive/Locomotor/PlayerController")]
        [SerializeField] private Transform _playerRoot;

        // The hand anchors — we pick the active one as the pendulum anchor when climbing starts.
        [Header("Hand Anchors")]
        [Tooltip("Left hand anchor transform (OVRCameraRig/TrackingSpace/LeftHandAnchor)")]
        [SerializeField] private Transform _leftHandAnchor;
        [Tooltip("Right hand anchor transform (OVRCameraRig/TrackingSpace/RightHandAnchor)")]
        [SerializeField] private Transform _rightHandAnchor;

        // ── Pendulum Tuning ──────────────────────────────────────────────────────
        [Header("Pendulum Tuning")]
        [Tooltip("How strongly lateral hand movement converts into swing. " +
                 "0 = pure gravity pendulum. 1+ = more responsive. Start around 1.")]
        [SerializeField, Range(0f, 5f)] private float _swingStrength = 1.0f;

        [Tooltip("Gravity scale for the pendulum. Real gravity = 9.81. " +
                 "Reduce for a floaty feel, increase for realistic weight.")]
        [SerializeField, Range(0f, 20f)] private float _gravityStrength = 6f;

        [Tooltip("Velocity damping per second (exponential). " +
                 "0 = no damping, 5 = stops quickly. Tune between 1-3 for comfort.")]
        [SerializeField, Range(0f, 10f)] private float _damping = 2f;

        [Tooltip("Maximum swing tangential speed (m/s) while holding. " +
                 "Keeps the pendulum from going wild.")]
        [SerializeField, Range(0f, 10f)] private float _maxSwingSpeed = 3f;

        [Tooltip("Fraction of swing velocity transferred to the character on release. " +
                 "0 = no launch, 1 = full launch. Tune for comfort around 0.4-0.7.")]
        [SerializeField, Range(0f, 1f)] private float _releaseVelocityMultiplier = 0.5f;

        [Tooltip("Maximum release speed (m/s) applied to the character on letting go.")]
        [SerializeField, Range(0f, 10f)] private float _maxReleaseVelocity = 4f;

        [Tooltip("Minimum rope length to activate pendulum physics. " +
                 "Prevents numerical issues when hand is right at the body root.")]
        [SerializeField, Range(0f, 1f)] private float _minPendulumDistance = 0.2f;

        [Header("Comfort")]
        [Tooltip("When enabled, reduces vertical swing component for less nausea.")]
        [SerializeField] private bool _comfortMode = true;

        [Tooltip("Fraction of the vertical swing velocity that is applied (0=flat, 1=full). " +
                 "Only used when comfortMode is enabled.")]
        [SerializeField, Range(0f, 1f)] private float _verticalSwingFraction = 0.4f;

        [Tooltip("Enable/disable the entire pendulum feature from the Inspector.")]
        [SerializeField] private bool _pendulumEnabled = true;

        // ── State ────────────────────────────────────────────────────────────────
        private bool _isClimbing;
        private Vector3 _anchorPoint;
        private Vector3 _swingVelocity; // world-space tangential velocity of the pendulum body

        // Rope length captured at grab time. The pendulum constrains the player to
        // this radius so the rope stays rigid (it must NOT grow as the hand sweeps).
        private float _ropeLength;

        // We store the last hand mid-point to estimate lateral input each frame
        private Vector3 _prevHandMidpoint;

        // ── Unity ────────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_climbingLocomotor != null)
            {
                _climbingLocomotor.WhenClimbingStarted.AddListener(OnClimbingStarted);
                _climbingLocomotor.WhenClimbingEnded.AddListener(OnClimbingEnded);
            }
        }

        private void OnDisable()
        {
            if (_climbingLocomotor != null)
            {
                _climbingLocomotor.WhenClimbingStarted.RemoveListener(OnClimbingStarted);
                _climbingLocomotor.WhenClimbingEnded.RemoveListener(OnClimbingEnded);
            }
        }

        private void LateUpdate()
        {
            if (!_pendulumEnabled || !_isClimbing) return;

            TickPendulum(Time.deltaTime);
        }

        private void OnDrawGizmos()
        {
            if (!_isClimbing || _playerRoot == null) return;

            // Draw anchor
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_anchorPoint, 0.05f);

            // Draw rope
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_anchorPoint, _playerRoot.position);

            // Draw swing velocity arrow
            Gizmos.color = Color.green;
            Gizmos.DrawRay(_playerRoot.position, _swingVelocity * 0.2f);
        }

        // ── Event Handlers ───────────────────────────────────────────────────────
        private void OnClimbingStarted()
        {
            if (!_pendulumEnabled) return;

            _isClimbing = true;
            _swingVelocity = Vector3.zero;

            // Snapshot the anchor ONCE at grab time. Using whichever hand is highest
            // as a proxy for the active grabbing hand.
            _anchorPoint = PickAnchorPoint();
            _prevHandMidpoint = GetHandMidpoint();

            // Capture the rigid rope length now. This is the radius the pendulum body
            // is constrained to for the whole grab. Without a fixed length the rope
            // would stretch as the hand sweeps sideways (the anchor would chase the
            // hand), which inflates the arc and feels wrong.
            _ropeLength = Mathf.Max(_minPendulumDistance, (_playerRoot.position - _anchorPoint).magnitude);
        }

        // The WhenClimbingEnded event passes the climbing release velocity.
        private void OnClimbingEnded(Vector3 climbVelocity)
        {
            if (!_isClimbing) return;
            _isClimbing = false;

            if (!_pendulumEnabled || _firstPersonLocomotor == null) return;

            // Transfer swing momentum to character.
            // We blend with the climb velocity to avoid sudden direction changes.
            Vector3 releaseVel = _swingVelocity * _releaseVelocityMultiplier;

            if (_comfortMode)
            {
                // Damp vertical component for comfort.
                releaseVel.y *= _verticalSwingFraction;
            }

            // Clamp magnitude.
            if (releaseVel.magnitude > _maxReleaseVelocity)
                releaseVel = releaseVel.normalized * _maxReleaseVelocity;

            // Add to the locomotor's existing velocity (which already includes climb exit velocity).
            _firstPersonLocomotor.Velocity += releaseVel;

            _swingVelocity = Vector3.zero;
        }

        // ── Pendulum Tick ────────────────────────────────────────────────────────
        private void TickPendulum(float dt)
        {
            if (_playerRoot == null || _locomotionConnection == null) return;

            // Carry the anchor with the climbable if it moves, but DON'T let the
            // hand's lateral sweep move it — that would stretch the rope. We keep the
            // anchor at its grab-time offset from the active hand.
            UpdateAnchor();

            Vector3 playerPos = _playerRoot.position;
            Vector3 rope = playerPos - _anchorPoint;
            float ropeLen = rope.magnitude;

            // Skip if too close to anchor to compute stable tangent.
            if (ropeLen < _minPendulumDistance) return;

            Vector3 radial = rope / ropeLen; // unit vector: anchor → player

            // ── Gravity contribution on the tangent ──────────────────────────────
            // Project gravity onto the tangent plane perpendicular to the rope.
            // tangent_gravity = gravity - dot(gravity, radial) * radial
            Vector3 gravity = Physics.gravity * _gravityStrength / 9.81f; // scaled gravity
            Vector3 tangentGravity = gravity - Vector3.Dot(gravity, radial) * radial;

            // ── Lateral hand input ────────────────────────────────────────────────
            // Estimate how fast the player's hands are moving laterally.
            // We project this into the tangent space to get a swing push.
            Vector3 currentMidpoint = GetHandMidpoint();
            Vector3 handDelta = (currentMidpoint - _prevHandMidpoint) / Mathf.Max(dt, 0.001f);
            _prevHandMidpoint = currentMidpoint;

            // Remove radial component — only lateral movement swings the pendulum.
            Vector3 lateralHandVel = handDelta - Vector3.Dot(handDelta, radial) * radial;

            // ── Swing velocity integration ─────────────────────────────────────────
            // The swingVelocity lives in world space (tangent to the arc).
            // We accumulate tangent acceleration from gravity and hand input.
            _swingVelocity += tangentGravity * dt;
            _swingVelocity += lateralHandVel * (_swingStrength * dt);

            // ── Damping ────────────────────────────────────────────────────────────
            // Exponential damping: v *= exp(-damping * dt) ≈ v * (1 - damping*dt) for small dt.
            _swingVelocity *= Mathf.Clamp01(1f - _damping * dt);

            // ── Clamp speed ────────────────────────────────────────────────────────
            float speed = _swingVelocity.magnitude;
            if (speed > _maxSwingSpeed)
                _swingVelocity = _swingVelocity * (_maxSwingSpeed / speed);

            // ── Pendulum constraint: keep player on the arc ────────────────────────
            // After applying tangential velocity, project the new position back onto
            // a sphere of radius ropeLen centered at the anchor.
            // We express this as a Relative LocomotionEvent so FirstPersonLocomotor
            // handles collisions normally.
            Vector3 tangentDisplacement = _swingVelocity * dt;

            if (_comfortMode)
            {
                // Reduce vertical motion to lower nausea.
                tangentDisplacement.y *= _verticalSwingFraction;
            }

            // Constrain the player to the RIGID rope length captured at grab time
            // (_ropeLength), not the current distance. This is what keeps the rope
            // from stretching as the player swings — the body rides a fixed-radius arc.
            // newPos = playerPos + tangentDisplacement
            // project back: newPos = anchor + normalize(newPos - anchor) * _ropeLength
            Vector3 newPos = playerPos + tangentDisplacement;
            Vector3 newRope = newPos - _anchorPoint;
            if (newRope.magnitude > 0.001f)
            {
                newPos = _anchorPoint + newRope.normalized * _ropeLength;
                tangentDisplacement = newPos - playerPos;
            }

            // Send as Relative LocomotionEvent — FirstPersonLocomotor applies it with
            // collision resolution at end-of-frame, same as normal climbing movement.
            // Uses the (int, Vector3, TranslationType) overload; RotationType defaults to None.
            LocomotionEvent evt = new LocomotionEvent(
                0,
                tangentDisplacement,
                LocomotionEvent.TranslationType.Relative);

            _locomotionConnection.HandleLocomotionEvent(evt);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        // The anchor is the grab point. In the real rig the grabbing hand is locked
        // to a static hold, so the anchor is a FIXED point in world space — it must
        // not chase the hand's lateral motion (that motion is swing INPUT, not anchor
        // movement; conflating them stretches the rope).
        //
        // We keep the anchor fixed at its grab-time world position. The only time it
        // should move is if the climbable itself translates; that is rare for static
        // holds and is intentionally left out to guarantee a rigid rope. If you later
        // support moving climbables, carry the anchor by the climbable's own delta
        // here (NOT by the hand position).
        private void UpdateAnchor()
        {
            // Intentionally no-op: anchor stays where it was captured in
            // OnClimbingStarted. Kept as a method so the tick reads clearly and a
            // moving-climbable implementation has an obvious hook.
        }

        // Returns the world position of the active (grabbing) hand — the highest hand,
        // matching the heuristic used by PickAnchorPoint at grab time.
        private Vector3 PickActiveHand()
        {
            if (_leftHandAnchor == null && _rightHandAnchor == null)
                return _playerRoot != null ? _playerRoot.position + Vector3.up * 0.5f : Vector3.zero;
            if (_leftHandAnchor == null) return _rightHandAnchor.position;
            if (_rightHandAnchor == null) return _leftHandAnchor.position;
            return _leftHandAnchor.position.y >= _rightHandAnchor.position.y
                ? _leftHandAnchor.position
                : _rightHandAnchor.position;
        }

        // Returns the midpoint between the two hands (or the available hand if only one is set).
        private Vector3 GetHandMidpoint()
        {
            if (_leftHandAnchor == null && _rightHandAnchor == null) return _playerRoot.position;
            if (_leftHandAnchor == null) return _rightHandAnchor.position;
            if (_rightHandAnchor == null) return _leftHandAnchor.position;
            return (_leftHandAnchor.position + _rightHandAnchor.position) * 0.5f;
        }

        // Picks the anchor point: the hand closest to the player body root
        // is the most likely active grab hand.
        private Vector3 PickAnchorPoint()
        {
            if (_leftHandAnchor == null && _rightHandAnchor == null)
                return _playerRoot != null ? _playerRoot.position + Vector3.up * 0.5f : Vector3.zero;

            if (_leftHandAnchor == null) return _rightHandAnchor.position;
            if (_rightHandAnchor == null) return _leftHandAnchor.position;

            if (_playerRoot == null) return GetHandMidpoint();

            // Pick the hand that is highest (most likely the grabbing hand on a wall).
            // Using the highest hand gives a more stable anchor above the body.
            return _leftHandAnchor.position.y >= _rightHandAnchor.position.y
                ? _leftHandAnchor.position
                : _rightHandAnchor.position;
        }
    }
}
