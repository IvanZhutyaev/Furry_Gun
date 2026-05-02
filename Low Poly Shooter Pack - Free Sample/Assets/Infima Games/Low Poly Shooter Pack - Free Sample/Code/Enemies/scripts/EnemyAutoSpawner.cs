using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Auto-spawns enemies at game start around the player.
/// Spawns only once per play session.
/// </summary>
public sealed class EnemyAutoSpawner : MonoBehaviour
{
    [SerializeField] private int spawnCount = 50;
    [SerializeField] private float maxDistanceFromPlayer = 100f;
    [SerializeField] private float minDistanceFromPlayer = 12f;
    [SerializeField] private float navMeshSampleRadius = 18f;
    [SerializeField] private int maxSpawnAttempts = 600;

    private static bool spawnedOnce;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (spawnedOnce)
            return;

        GameObject go = new GameObject("Enemy Auto Spawner");
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyAutoSpawner>();
    }

    private IEnumerator Start()
    {
        // Wait one frame so Player/NavMesh in scene are initialized.
        yield return null;

        if (spawnedOnce)
            yield break;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            yield break;

        GameObject enemyPrefab = ResolveEnemyPrefab();
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemyAutoSpawner] Enemy prefab not found. Put prefab into Resources/Enemies/enemy.prefab or keep one enemy in scene.");
            yield break;
        }

        int spawned = 0;
        int attempts = 0;
        Vector3 center = player.transform.position;

        while (spawned < spawnCount && attempts < maxSpawnAttempts)
        {
            attempts++;

            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 guess = center + new Vector3(ring.x, 0f, ring.y);

            if (!TryResolveSpawnPoint(guess, out Vector3 spawnPos))
                continue;

            Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            spawned++;
        }

        spawnedOnce = true;
        Destroy(gameObject);
    }

    private bool TryResolveSpawnPoint(Vector3 guess, out Vector3 spawnPos)
    {
        // Priority: NavMesh
        if (NavMesh.SamplePosition(guess, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            spawnPos = navHit.position;
            return true;
        }

        // Fallback: terrain/mesh collider below point
        Vector3 rayStart = guess + Vector3.up * 200f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point;
            return true;
        }

        spawnPos = default;
        return false;
    }

    private static GameObject ResolveEnemyPrefab()
    {
        // Preferred runtime-safe way.
        GameObject pref = Resources.Load<GameObject>("Enemies/enemy");
        if (pref != null)
            return pref;

        pref = Resources.Load<GameObject>("enemy");
        if (pref != null)
            return pref;

        // Fallback if scene already has one enemy instance.
        enemyAI existing = FindObjectOfType<enemyAI>();
        return existing != null ? existing.gameObject : null;
    }
}
