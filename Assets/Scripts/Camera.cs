using UnityEngine;
using System.Collections;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [SerializeField, Range(0.01f, 1f)] private float smoothTime = 0.08f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -8f);
    [SerializeField] private float lookAtHeight = 1.5f;
    [SerializeField] private bool rotateCameraToTarget = true;

    [Header("Búsqueda automática")]
    [SerializeField] private string playerObjectName = "Player(Clone)";

    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 offset;

    private Transform fixedAnchor;
    private bool usingFixedAnchor;

    private void Awake()
    {
        offset = cameraOffset;

        if (target != null)
            CenterCameraOnTarget();
    }

    private void LateUpdate()
    {
        if (usingFixedAnchor && fixedAnchor != null)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                fixedAnchor.position,
                ref currentVelocity,
                smoothTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                fixedAnchor.rotation,
                Time.deltaTime / Mathf.Max(0.01f, smoothTime)
            );

            return;
        }

        if (target == null)
            return;

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );

        RotateTowardsTarget();
    }

    public void FindPlayerTarget()
    {
        if (usingFixedAnchor && fixedAnchor != null)
            return;

        GameObject playerObject = GameObject.Find(playerObjectName);

        if (playerObject == null)
        {
            Debug.LogWarning($"SmoothCameraFollow: No se encontró un objeto llamado '{playerObjectName}' en la escena.");
            return;
        }

        SetFollowTarget(playerObject.transform, true);
    }

    public void FindPlayerTargetDelayed()
    {
        StartCoroutine(FindPlayerTargetDelayedCoroutine());
    }

    private IEnumerator FindPlayerTargetDelayedCoroutine()
    {
        yield return null;
        yield return null;

        FindPlayerTarget();
    }

    public void SetTarget(Transform newTarget)
    {
        SetFollowTarget(newTarget, true);
    }

    public void SetFollowTarget(Transform newTarget, bool snap = false)
    {
        if (usingFixedAnchor && fixedAnchor != null)
            return;

        usingFixedAnchor = false;
        fixedAnchor = null;

        target = newTarget;
        offset = cameraOffset;

        if (snap)
            SnapNow();
    }

    public void SetFixedAnchor(Transform anchor, bool snap = false)
    {
        fixedAnchor = anchor;
        usingFixedAnchor = fixedAnchor != null;

        if (snap)
            SnapNow();
    }

    public void ClearFixedAnchor(bool snap = false)
    {
        usingFixedAnchor = false;
        fixedAnchor = null;

        if (snap)
            SnapNow();
    }

    public void SnapNow()
    {
        if (usingFixedAnchor && fixedAnchor != null)
        {
            transform.SetPositionAndRotation(fixedAnchor.position, fixedAnchor.rotation);
            currentVelocity = Vector3.zero;
            return;
        }

        CenterCameraOnTarget();
        currentVelocity = Vector3.zero;
    }

    public void CenterCameraOnTarget()
    {
        if (target == null)
            return;

        offset = cameraOffset;
        transform.position = target.position + offset;
        RotateTowardsTarget();
    }

    private void RotateTowardsTarget()
    {
        if (!rotateCameraToTarget || target == null)
            return;

        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        transform.LookAt(lookPoint);
    }
}