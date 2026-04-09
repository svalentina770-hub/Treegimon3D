using UnityEngine;

public class CombatArenaInstance : MonoBehaviour
{
    [SerializeField] private Transform anchorA;
    [SerializeField] private Transform anchorB;
    [SerializeField] private Transform cameraAnchor;

    public Transform AnchorA => anchorA;
    public Transform AnchorB => anchorB;
    public Transform CameraAnchor => cameraAnchor;
}
