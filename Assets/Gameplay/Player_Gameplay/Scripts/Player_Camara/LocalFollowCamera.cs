using UnityEngine;

public class LocalFollowCamera : MonoBehaviour
{
    private enum CameraMode
    {
        FollowTarget,
        FixedAnchor
    }

    [SerializeField] private float height = 6f;
    [SerializeField] private float distance = 7f;
    [SerializeField] private float lookHeight = 1.5f;

    [Header("Suavizado")]
    [SerializeField] private float positionSmoothTime = 0.22f;
    [SerializeField] private float forwardSmoothSpeed = 4f;
    [SerializeField] private float rotationSmoothSpeed = 5f;
    [SerializeField] private float snapDistance = 25f;

    private CameraMode currentMode = CameraMode.FollowTarget;

    private Transform followTarget;
    private Transform fixedAnchor;

    private Vector3 positionVelocity;
    private Vector3 smoothedForward = Vector3.forward;

    public void SetTarget(Transform newTarget, bool snapImmediately = true)
    {
        SetFollowTarget(newTarget, snapImmediately);
    }

    public void SetFollowTarget(Transform newTarget, bool snapImmediately = true)
    {
        currentMode = CameraMode.FollowTarget;
        followTarget = newTarget;
        fixedAnchor = null;

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

        if (snapImmediately)
            SnapNow();
    }

    private void LateUpdate()
    {
        if (currentMode == CameraMode.FollowTarget)
            UpdateFollowMode();
        else
            UpdateFixedMode();
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
        else
        {
            if (fixedAnchor == null) return;

            transform.position = fixedAnchor.position;
            transform.rotation = fixedAnchor.rotation;
        }
    }

    private Vector3 GetFlatForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.normalized;
    }
}



























/*using UnityEngine;

public class LocalFollowCamera : MonoBehaviour
{
    [SerializeField] private float height = 6f;
    [SerializeField] private float distance = 7f;
    [SerializeField] private float lookHeight = 1.5f;

    [Header("Suavizado")]
    [SerializeField] private float positionSmoothTime = 0.22f;
    [SerializeField] private float forwardSmoothSpeed = 4f;
    [SerializeField] private float rotationSmoothSpeed = 5f;
    [SerializeField] private float snapDistance = 25f;

    private Transform target;
    private Vector3 positionVelocity;
    private Vector3 smoothedForward = Vector3.forward;

    public void SetTarget(Transform newTarget, bool snapImmediately = true)
    {
        target = newTarget;

        if (target != null)
        {
            Vector3 flatForward = GetFlatForward(target.forward);
            if (flatForward.sqrMagnitude > 0.001f)
                smoothedForward = flatForward.normalized;
        }

        if (snapImmediately)
            SnapNow();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredForward = GetFlatForward(target.forward);
        if (desiredForward.sqrMagnitude > 0.001f)
        {
            smoothedForward = Vector3.Slerp(
                smoothedForward,
                desiredForward.normalized,
                forwardSmoothSpeed * Time.deltaTime
            );
        }

        Vector3 desiredPosition = target.position - smoothedForward * distance + Vector3.up * height;
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;

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

    public void SnapNow()
    {
        if (target == null) return;

        Vector3 flatForward = GetFlatForward(target.forward);
        if (flatForward.sqrMagnitude > 0.001f)
            smoothedForward = flatForward.normalized;

        Vector3 desiredPosition = target.position - smoothedForward * distance + Vector3.up * height;
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;

        transform.position = desiredPosition;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
    }

    private Vector3 GetFlatForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.normalized;
    }
}*/