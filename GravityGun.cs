using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class GravityGun : MonoBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;
    public Transform gunMuzzle;          // for beam start
    public LineRenderer beamLine;        // optional
    public Transform playerRoot;         // colliders to ignore while holding

    [Header("Targeting")]
    public LayerMask interactMask = ~0;
    public float pickupRange = 12f;
    public float puntRange = 20f;
    public float maxPickupMass = 120f;
    public float sphereRadius = 0.28f;

    [Header("Hold Distance")]
    public float holdDistance = 4.5f;
    public float minHoldDistance = 2.0f;
    public float maxHoldDistance = 10.0f;
    public float scrollStep = 0.35f;

    [Header("PD Controller (Position)")]
    public float posKp = 700f;           // spring (slightly softer = fewer overshoots)
    public float posKd = 120f;           // damping
    public float maxForce = 20000f;      // clamp

    [Header("PD Controller (Rotation)")]
    public float rotKp = 22f;            // spring
    public float rotKd = 2.2f;           // damping
    public float maxTorque = 7000f;      // clamp
    public bool keepUpright = true;      // lock roll to world-up

    [Header("Throw / Punt")]
    public float throwVelocityChange = 22f;
    public float puntVelocityChange  = 14f;

    [Header("Held Rigidbody Tweaks")]
    public float heldDrag = 4f;
    public float heldAngularDrag = 6f;

    [Header("Input (NIS)")]
    public InputActionReference primaryFire;    // LMB (optional)
    public InputActionReference secondaryFire;  // RMB (optional)

    // runtime
    private Rigidbody _held;
    private Rigidbody _anchor;          // kinematic follower
    private float _currentDist;
    private Vector3 _prevAnchorPos;
    private Quaternion _prevAnchorRot;
    private static readonly RaycastHit[] _hits = new RaycastHit[8];
    private readonly List<(Collider a, Collider b)> _ignoredPairs = new();

    // saved rb state
    private bool _savedGravity;
    private float _savedDrag, _savedAngDrag;
    private RigidbodyInterpolation _savedInterp;
    private CollisionDetectionMode _savedCCD;
    private RigidbodyConstraints _savedConstraints;

    // --- helpers to support both naming schemes (drag vs linearDamping) ---
    private static float GetLinDamping(Rigidbody rb) =>
#if UNITY_6_2_OR_NEWER
        rb.linearDamping;
#else
        rb.linearDamping;
#endif

    private static void SetLinDamping(Rigidbody rb, float v)
    {
#if UNITY_6_2_OR_NEWER
        rb.linearDamping = v;
#else
        rb.linearDamping = v;
#endif
    }

    private static float GetAngDamping(Rigidbody rb) =>
#if UNITY_6_2_OR_NEWER
        rb.angularDamping;
#else
        rb.angularDamping;
#endif

    private static void SetAngDamping(Rigidbody rb, float v)
    {
#if UNITY_6_2_OR_NEWER
        rb.angularDamping = v;
#else
        rb.angularDamping = v;
#endif
    }

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;

        // create kinematic anchor once
        var go = new GameObject("GravityGunAnchor_PD");
        go.hideFlags = HideFlags.HideInHierarchy;
        _anchor = go.AddComponent<Rigidbody>();
        _anchor.isKinematic = true;
        _anchor.useGravity = false;
        _anchor.interpolation = RigidbodyInterpolation.Interpolate;

        _currentDist = holdDistance;
        _prevAnchorPos = transform.position;
        _prevAnchorRot = transform.rotation;

        if (beamLine) { beamLine.enabled = false; beamLine.positionCount = 2; }
    }

    private void OnEnable()
    {
        primaryFire?.action.Enable();
        secondaryFire?.action.Enable();
        if (primaryFire != null)   primaryFire.action.performed   += OnPrimary;
        if (secondaryFire != null) secondaryFire.action.performed += OnSecondary;
    }

    private void OnDisable()
    {
        if (primaryFire != null)   primaryFire.action.performed   -= OnPrimary;
        if (secondaryFire != null) secondaryFire.action.performed -= OnSecondary;
        primaryFire?.action.Disable();
        secondaryFire?.action.Disable();

        if (_held) Release(dropOnly:true);
    }

    private void Update()
    {
        // 🚫 Don’t interact while the spawn menu is open
        if (SpawnMenu.IsMenuOpen)
            return;

        // Fallback input for builds if InputActionReferences aren’t wired
        var mouse = Mouse.current;
        if (primaryFire == null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            OnPrimary(default);
        if (secondaryFire == null && mouse != null && mouse.rightButton.wasPressedThisFrame)
            OnSecondary(default);

        if (_held && mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _currentDist = Mathf.Clamp(
                    _currentDist + Mathf.Sign(scroll) * scrollStep,
                    minHoldDistance,
                    maxHoldDistance
                );
            }
        }
        UpdateBeam();
    }

    private void FixedUpdate()
    {
        // move anchor to target (and track its “virtual” velocity)
        Vector3 targetPos = playerCamera.transform.position + playerCamera.transform.forward * _currentDist;
        Quaternion targetRot = playerCamera.transform.rotation;

        Vector3 anchorVel = (targetPos - _prevAnchorPos) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);

        _anchor.MovePosition(targetPos);
        _anchor.MoveRotation(targetRot);

        if (!_held)
        {
            _prevAnchorPos = targetPos;
            _prevAnchorRot = targetRot;
            return;
        }

        // --- POSITION PD ---
        Vector3 to = targetPos - _held.worldCenterOfMass;
        Vector3 relVel = anchorVel - _held.linearVelocity;               // ✅ new API
        Vector3 force = posKp * to + posKd * relVel;
        if (force.sqrMagnitude > maxForce * maxForce)
            force = force.normalized * maxForce;
        _held.AddForce(force, ForceMode.Force);

        // --- ROTATION PD ---
        Quaternion desired = targetRot;
        if (keepUpright)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
            desired = Quaternion.LookRotation(fwd, Vector3.up);
        }

        Quaternion qErr = desired * Quaternion.Inverse(_held.rotation);
        qErr.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 angErr = axis * Mathf.Deg2Rad * angleDeg;
        Vector3 angVelErr = -_held.angularVelocity;

        Vector3 torque = rotKp * angErr + rotKd * angVelErr;
        if (torque.sqrMagnitude > maxTorque * maxTorque)
            torque = torque.normalized * maxTorque;
        _held.AddTorque(torque, ForceMode.Force);

        _prevAnchorPos = targetPos;
        _prevAnchorRot = targetRot;
    }

    // ---------------- Input ----------------
    private void OnSecondary(InputAction.CallbackContext _)
    {
        if (_held) Release(dropOnly:true); // toggle drop
        else TryPickup();
    }

    private void OnPrimary(InputAction.CallbackContext _)
    {
        if (_held) Release(dropOnly:false); // throw
        else Punt();
    }

    // ---------------- Actions ----------------
    private void TryPickup()
    {
        var camPos = playerCamera.transform.position;
        var camFwd = playerCamera.transform.forward;
        int count = Physics.SphereCastNonAlloc(
            new Ray(camPos, camFwd),
            sphereRadius,
            _hits,
            pickupRange,
            interactMask,
            QueryTriggerInteraction.Ignore
        );
        if (count == 0) return;

        Rigidbody bestRB = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            var h = _hits[i];
            var rb = h.rigidbody;
            if (!rb || rb.isKinematic) continue;
            if (rb.mass > maxPickupMass) continue;

            float distScore = -h.distance;
            float dot = Vector3.Dot(camFwd, (h.point - camPos).normalized);
            float score = distScore + dot * 3f;
            if (score > bestScore)
            {
                bestScore = score;
                bestRB = rb;
            }
        }
        if (!bestRB) return;

        _held = bestRB;

        // save & tweak rb
        _savedGravity = _held.useGravity;
        _savedDrag = GetLinDamping(_held);
        _savedAngDrag = GetAngDamping(_held);
        _savedInterp = _held.interpolation;
        _savedCCD = _held.collisionDetectionMode;
        _savedConstraints = _held.constraints;

        _held.useGravity = false;
        SetLinDamping(_held, heldDrag);
        SetAngDamping(_held, heldAngularDrag);
        _held.interpolation = RigidbodyInterpolation.Interpolate;
        _held.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // start at current distance to avoid big step
        Vector3 camToObj = _held.worldCenterOfMass - camPos;
        _currentDist = Mathf.Clamp(Vector3.Dot(camToObj, camFwd), minHoldDistance, maxHoldDistance);

        _anchor.position = camPos + camFwd * _currentDist;
        _anchor.rotation = playerCamera.transform.rotation;
        _prevAnchorPos = _anchor.position;
        _prevAnchorRot = _anchor.rotation;

        IgnorePlayerCollisions(_held, true);

        if (beamLine) beamLine.enabled = true;
    }

    private void Release(bool dropOnly)
    {
        if (_held == null) return;

        if (!dropOnly)
        {
            _held.linearVelocity = Vector3.zero;
            _held.angularVelocity = Vector3.zero;
            _held.AddForce(playerCamera.transform.forward * throwVelocityChange, ForceMode.VelocityChange);
        }

        // restore
        _held.useGravity = _savedGravity;
        SetLinDamping(_held, _savedDrag);
        SetAngDamping(_held, _savedAngDrag);
        _held.interpolation = _savedInterp;
        _held.collisionDetectionMode = _savedCCD;
        _held.constraints = _savedConstraints;

        IgnorePlayerCollisions(_held, false);

        _held = null;
        if (beamLine) beamLine.enabled = false;
    }

    private void Punt()
    {
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.SphereCast(ray, sphereRadius, out RaycastHit hit, puntRange, interactMask, QueryTriggerInteraction.Ignore))
        {
            var rb = hit.rigidbody;
            if (rb && !rb.isKinematic)
                rb.AddForce(playerCamera.transform.forward * puntVelocityChange, ForceMode.VelocityChange);
        }
    }

    // ---------------- Collisions with player ----------------
    private void IgnorePlayerCollisions(Rigidbody target, bool ignore)
    {
        if (!playerRoot) playerRoot = transform.root;
        var pCols = playerRoot ? playerRoot.GetComponentsInChildren<Collider>(true) : new Collider[0];
        var oCols = target.GetComponentsInChildren<Collider>(true);

        if (ignore)
        {
            _ignoredPairs.Clear();
            foreach (var pc in pCols)
                foreach (var oc in oCols)
                {
                    if (!pc || !oc) continue;
                    if (Physics.GetIgnoreCollision(pc, oc)) continue;
                    Physics.IgnoreCollision(pc, oc, true);
                    _ignoredPairs.Add((pc, oc));
                }
        }
        else
        {
            foreach (var (a, b) in _ignoredPairs)
                if (a && b) Physics.IgnoreCollision(a, b, false);
            _ignoredPairs.Clear();
        }
    }

    // ---------------- Beam ----------------
    private void UpdateBeam()
    {
        if (!beamLine) return;

        if (_held)
        {
            Vector3 start = gunMuzzle ? gunMuzzle.position : playerCamera.transform.position;
            beamLine.SetPosition(0, start);
            beamLine.SetPosition(1, _held.worldCenterOfMass);
            if (!beamLine.enabled) beamLine.enabled = true;
        }
        else if (beamLine.enabled)
        {
            beamLine.enabled = false;
        }
    }
}
