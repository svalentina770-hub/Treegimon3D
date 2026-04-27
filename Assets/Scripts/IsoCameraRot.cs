
using UnityEngine;

[DefaultExecutionOrder(10000)]

public class IsoCameraRot : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Object used as rotation source. Assign here the object that REALLY rotates in play mode, for example the visual Capsule, Geometry, Armature, or Player root.")]
    public Transform rotationSource;

    [Tooltip("Object that will receive the rotation. If empty, this object will be rotated. Use this when Cinemachine is overriding the transform where this script is attached.")]
    public Transform objectToRotate;

    [Tooltip("Optional. If empty, the script uses rotationSource. This is useful if you want to read rotation from one object but apply the camera around another object.")]
    public Transform followTarget;

    [Header("Isometric Rotation")]
    public float cameraXAngle = 50f;
    public float additionalYAngle = 45f;
    public float cameraZAngle = 0f;

    [Header("Smoothing")]
    public float rotationSmoothTime = 0.1f;

    [Header("Rotation Mode")]
    [Tooltip("Activate this when this script is on PlayerFollowCamera or any object that is not child of the player.")]
    public bool useWorldRotation = true;

    [Header("Debug")]
    public bool showDebugValues = true;
    [SerializeField] private float sourceY;
    [SerializeField] private float targetCameraY;
    [SerializeField] private float appliedCameraY;

    private float currentYVelocity;

    private void LateUpdate()
    {
        if (rotationSource == null) return;

        Transform targetTransform = objectToRotate != null ? objectToRotate : transform;

        sourceY = rotationSource.eulerAngles.y;
        targetCameraY = sourceY + additionalYAngle;

        float currentY = useWorldRotation
            ? targetTransform.eulerAngles.y
            : targetTransform.localEulerAngles.y;

        appliedCameraY = Mathf.SmoothDampAngle(
            currentY,
            targetCameraY,
            ref currentYVelocity,
            rotationSmoothTime
        );

        Quaternion targetRotation = Quaternion.Euler(cameraXAngle, appliedCameraY, cameraZAngle);

        if (useWorldRotation)
        {
            targetTransform.rotation = targetRotation;
        }
        else
        {
            targetTransform.localRotation = targetRotation;
        }
    }
}