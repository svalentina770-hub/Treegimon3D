using UnityEngine;

public class LocalFollowCamera : MonoBehaviour
{
    private enum CameraMode
    {
        FollowTarget,
        FixedAnchor,
        CombatOrbit
    }

    [Header("Follow")]
    [SerializeField] private float height = 6f;
    [SerializeField] private float distance = 7f;
    [SerializeField] private float lookHeight = 1.5f;

    [Header("Suavizado")]
    [SerializeField] private float positionSmoothTime = 0.22f;
    [SerializeField] private float forwardSmoothSpeed = 4f;
    [SerializeField] private float rotationSmoothSpeed = 5f;
    [SerializeField] private float snapDistance = 25f;

    [Header("Combat Orbit")]
    [SerializeField] private float combatPitch = 20f;
    [SerializeField] private float combatHeight = 2f;
    [SerializeField] private float orbitYawSpeed = 100f;
    [SerializeField] private float orbitYawLimit = 75f;
    [SerializeField] private float zoomSpeed = 4f;
    [SerializeField] private float minZoom = 3.5f;
    [SerializeField] private float maxZoom = 8f;

    private CameraMode currentMode = CameraMode.FollowTarget;

    private Transform followTarget;
    private Transform fixedAnchor;
    private Transform combatTarget;

    private Vector3 positionVelocity;
    private Vector3 smoothedForward = Vector3.forward;

    private VirtualJoystick joystick;

    private float baseCombatYaw;
    private float currentCombatYaw;
    private float currentCombatZoom;

    public void SetTarget(Transform newTarget, bool snapImmediately = true)
    {
        SetFollowTarget(newTarget, snapImmediately);
    }

    public void SetFollowTarget(Transform newTarget, bool snapImmediately = true)
    {
        currentMode = CameraMode.FollowTarget;
        followTarget = newTarget;
        fixedAnchor = null;
        combatTarget = null;

        if (followTarget != null)
        {
            Vector3 flatForward = GetFlatForward(followTarget.forward);
            if (flatForward.sqrMagnitude > 0.001f)
                smoothedForward = flatForward.normalized;
        }

        if (snapImmediately)
            SnapNow();
    }

    public void SetFixedAnchor(Transform anchor, bool snapImmediately = true)
    {
        currentMode = CameraMode.FixedAnchor;
        fixedAnchor = anchor;
        followTarget = null;
        combatTarget = null;

        if (snapImmediately)
            SnapNow();
    }

    public void SetCombatOrbit(Transform target, bool snapImmediately = true)
    {
        currentMode = CameraMode.CombatOrbit;
        combatTarget = target;
        followTarget = null;
        fixedAnchor = null;

        if (joystick == null)
            joystick = FindFirstObjectByType<VirtualJoystick>();

        if (combatTarget != null)
        {
            Vector3 flatDir = transform.position - combatTarget.position;
            flatDir.y = 0f;

            if (flatDir.sqrMagnitude < 0.01f)
                flatDir = -combatTarget.forward;

            baseCombatYaw = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            currentCombatYaw = 0f;
            currentCombatZoom = Mathf.Clamp(flatDir.magnitude, minZoom, maxZoom);
        }

        if (snapImmediately)
            SnapNow();
    }

    private void LateUpdate()
    {
        switch (currentMode)
        {
            case CameraMode.FollowTarget:
                UpdateFollowMode();
                break;

            case CameraMode.FixedAnchor:
                UpdateFixedMode();
                break;

            case CameraMode.CombatOrbit:
                UpdateCombatMode();
                break;
        }
    }

    private void UpdateFollowMode()
    {
        if (followTarget == null) return;

        Vector3 desiredForward = GetFlatForward(followTarget.forward);
        if (desiredForward.sqrMagnitude > 0.001f)
        {
            smoothedForward = Vector3.Slerp(
                smoothedForward,
                desiredForward.normalized,
                forwardSmoothSpeed * Time.deltaTime
            );
        }

        Vector3 desiredPosition = followTarget.position - smoothedForward * distance + Vector3.up * height;
        Vector3 lookPoint = followTarget.position + Vector3.up * lookHeight;

        if (Vector3.Distance(transform.position, desiredPosition) > snapDistance)
        {
            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateFixedMode()
    {
        if (fixedAnchor == null) return;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            fixedAnchor.position,
            ref positionVelocity,
            positionSmoothTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            fixedAnchor.rotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateCombatMode()
    {
        if (combatTarget == null) return;

        if (joystick == null)
            joystick = FindFirstObjectByType<VirtualJoystick>();

        Vector2 input = joystick != null ? joystick.InputVector : Vector2.zero;

        currentCombatYaw += input.x * orbitYawSpeed * Time.deltaTime;
        currentCombatYaw = Mathf.Clamp(currentCombatYaw, -orbitYawLimit, orbitYawLimit);

        currentCombatZoom -= input.y * zoomSpeed * Time.deltaTime;
        currentCombatZoom = Mathf.Clamp(currentCombatZoom, minZoom, maxZoom);

        Vector3 lookPoint = combatTarget.position + Vector3.up * combatHeight;

        Quaternion orbitRotation = Quaternion.Euler(combatPitch, baseCombatYaw + currentCombatYaw, 0f);
        Vector3 offset = orbitRotation * new Vector3(0f, 0f, -currentCombatZoom);

        Vector3 desiredPosition = lookPoint + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    public void SnapNow()
    {
        if (currentMode == CameraMode.FollowTarget)
        {
            if (followTarget == null) return;

            Vector3 flatForward = GetFlatForward(followTarget.forward);
            if (flatForward.sqrMagnitude > 0.001f)
                smoothedForward = flatForward.normalized;

            Vector3 desiredPosition = followTarget.position - smoothedForward * distance + Vector3.up * height;
            Vector3 lookPoint = followTarget.position + Vector3.up * lookHeight;

            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }
        else if (currentMode == CameraMode.FixedAnchor)
        {
            if (fixedAnchor == null) return;

            transform.position = fixedAnchor.position;
            transform.rotation = fixedAnchor.rotation;
        }
        else if (currentMode == CameraMode.CombatOrbit)
        {
            if (combatTarget == null) return;

            Vector3 lookPoint = combatTarget.position + Vector3.up * combatHeight;
            Quaternion orbitRotation = Quaternion.Euler(combatPitch, baseCombatYaw + currentCombatYaw, 0f);
            Vector3 offset = orbitRotation * new Vector3(0f, 0f, -currentCombatZoom);

            transform.position = lookPoint + offset;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }
    }

    private Vector3 GetFlatForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.normalized;
    }
}



