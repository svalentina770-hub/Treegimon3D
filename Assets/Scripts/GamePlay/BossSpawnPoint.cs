using UnityEngine;

public class BossSpawnPoint : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private string bossId = "boss_hidro";
    [SerializeField] private GameObject bossPrefab;

    [Header("Zona")]
    [SerializeField] private BiomeZone assignedZone;

    public string BossId => bossId;
    public GameObject BossPrefab => bossPrefab;
    public BiomeZone AssignedZone => assignedZone;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.55f);
        Gizmos.DrawSphere(transform.position, 0.75f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
    }
}
