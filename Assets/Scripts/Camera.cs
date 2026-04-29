using UnityEngine;
using System.Collections;

public class SmoothCameraFollow : MonoBehaviour
{
    #region Variables

    private Vector3 _offset;
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [SerializeField][Range(0.01f, 1f)] private float smoothTime = 0.08f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -8f);
    [SerializeField] private float lookAtHeight = 1.5f;
    [SerializeField] private bool rotateCameraToTarget = true;

    private Vector3 _currentVelocity = Vector3.zero;

    #endregion

    #region Unity callbacks

    private void Awake()
    {
        if (target != null)
        {
            CenterCameraOnTarget();
        }
    }

    public void FindPlayerTarget()
    {
        GameObject playerObject = GameObject.Find("Player(Clone)");

        if (playerObject == null)
        {
            Debug.LogWarning("SmoothCameraFollow: No se encontró un objeto llamado 'Player' en la escena.");
            return;
        }

        target = playerObject.transform;
        CenterCameraOnTarget();
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

    private void CenterCameraOnTarget()
    {
        _offset = cameraOffset;
        transform.position = target.position + _offset;
        RotateTowardsTarget();
    }

    private void RotateTowardsTarget()
    {
        if (!rotateCameraToTarget || target == null)
        {
            return;
        }

        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        transform.LookAt(lookPoint);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }
        Vector3 targetPosition = target.position + _offset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _currentVelocity,
            smoothTime
        );

        RotateTowardsTarget();
    }

    #endregion
}