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
    [SerializeField] private float maxGroundSlope = 35f;

    [Header("Spawn Capsule Check")]
    [SerializeField] private float enemyCapsuleHeight = 1.85f;
    [SerializeField] private float enemyCapsuleRadius = 0.36f;
    [SerializeField] private float footClearance = 0.04f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private LayerMask blockingMask = ~0;
    [Header("Boss")]
    [SerializeField] private float bossCheckInterval = 0.5f;
    [SerializeField] private float bossCenterSearchRadius = 20f;
    [SerializeField] private int bossCenterSearchAttempts = 80;

    private readonly Collider[] overlapHits = new Collider[24];

    private static bool spawnedOnce;
    private bool bossSpawned;
    private GameObject playerObject;

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

        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            yield break;

        GameObject enemyPrefab = ResolveEnemyPrefab();
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemyAutoSpawner] Enemy prefab not found. Put prefab into Resources/Enemies/enemy.prefab or keep one enemy in scene.");
            yield break;
        }

        int spawned = 0;
        int attempts = 0;
        Vector3 center = playerObject.transform.position;

        while (spawned < spawnCount && attempts < maxSpawnAttempts)
        {
            attempts++;

            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;
            dir.Normalize();
            Vector2 ring = dir * Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 guess = center + new Vector3(ring.x, 0f, ring.y);

            if (!TryResolveSpawnPoint(guess, out Vector3 spawnPos))
                continue;

            Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            spawned++;
        }

        spawnedOnce = true;
        StartCoroutine(SpawnBossWhenAllEnemiesDead());
    }

    private IEnumerator SpawnBossWhenAllEnemiesDead()
    {
        while (!bossSpawned)
        {
            yield return new WaitForSeconds(bossCheckInterval);

            if (!HasAliveNonBossEnemies())
            {
                SpawnBossAtMapCenter();
                bossSpawned = true;
                Destroy(gameObject);
                yield break;
            }
        }
    }

    private bool HasAliveNonBossEnemies()
    {
        enemyAI[] all = FindObjectsOfType<enemyAI>();
        for (int i = 0; i < all.Length; i++)
        {
            enemyAI ai = all[i];
            if (ai == null || !ai.enabled || !ai.gameObject.activeInHierarchy)
                continue;
            if (IsBoss(ai.gameObject))
                continue;
            return true;
        }
        return false;
    }

    private void SpawnBossAtMapCenter()
    {
        GameObject bossPrefab = ResolveBossPrefab();
        if (bossPrefab == null)
        {
            Debug.LogWarning("[EnemyAutoSpawner] Boss prefab not found. Put it into Resources/Enemies/BOSS.prefab or keep one boss in scene.");
            return;
        }

        Vector3 mapCenter = ResolveMapCenter();
        if (!TryResolveBossSpawnPoint(mapCenter, out Vector3 spawnPos))
        {
            Debug.LogWarning("[EnemyAutoSpawner] Could not resolve valid boss spawn point near map center.");
            return;
        }

        Instantiate(bossPrefab, spawnPos, Quaternion.identity);
    }

    private bool TryResolveBossSpawnPoint(Vector3 center, out Vector3 spawnPos)
    {
        if (TryResolveSpawnPoint(center, out spawnPos))
            return true;

        for (int i = 0; i < bossCenterSearchAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * bossCenterSearchRadius;
            if (TryResolveSpawnPoint(center + new Vector3(offset.x, 0f, offset.y), out spawnPos))
                return true;
        }

        spawnPos = default;
        return false;
    }

    private Vector3 ResolveMapCenter()
    {
        // Most scenes with outdoor map use Terrain: its center is the map center.
        if (Terrain.activeTerrain != null && Terrain.activeTerrain.terrainData != null)
        {
            Terrain t = Terrain.activeTerrain;
            Vector3 size = t.terrainData.size;
            return t.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        }

        // Fallback: center around current player position.
        return playerObject != null ? playerObject.transform.position : Vector3.zero;
    }

    private bool TryResolveSpawnPoint(Vector3 guess, out Vector3 spawnPos)
    {
        Vector3 candidate = guess;

        // Priority: NavMesh point near the guess.
        if (NavMesh.SamplePosition(guess, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
            candidate = navHit.position;

        // Exact ground position under candidate (prevents floating/embedding).
        if (!TryFindGroundPoint(candidate, out RaycastHit groundHit))
        {
            spawnPos = default;
            return false;
        }

        // Keep enemies off steep/invalid slopes.
        float slope = Vector3.Angle(groundHit.normal, Vector3.up);
        if (slope > maxGroundSlope)
        {
            spawnPos = default;
            return false;
        }

        // Volume clearance check (avoids trees/rocks/walls and any overlaps).
        Vector3 p = groundHit.point;
        Vector3 bottom = p + Vector3.up * (enemyCapsuleRadius + footClearance);
        Vector3 top = p + Vector3.up * Mathf.Max(enemyCapsuleRadius + footClearance, enemyCapsuleHeight - enemyCapsuleRadius);

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            enemyCapsuleRadius,
            overlapHits,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = overlapHits[i];
            if (c == null)
                continue;
            // Ignore the surface we're standing on.
            if (c == groundHit.collider)
                continue;
            // Anything else means blocked (trees included).
            spawnPos = default;
            return false;
        }

        spawnPos = p;
        return true;
    }

    private bool TryFindGroundPoint(Vector3 around, out RaycastHit groundHit)
    {
        Vector3 rayStart = around + Vector3.up * 200f;
        return Physics.Raycast(rayStart, Vector3.down, out groundHit, 500f, groundMask, QueryTriggerInteraction.Ignore);
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

        // Fallback if scene already has enemy instances:
        // prefer non-boss object names, because boss may have very different stats.
        enemyAI[] all = FindObjectsOfType<enemyAI>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            string n = all[i].gameObject.name.ToLowerInvariant();
            if (!n.Contains("boss"))
                return all[i].gameObject;
        }

        // Last resort.
        return all.Length > 0 && all[0] != null ? all[0].gameObject : null;
    }

    private static GameObject ResolveBossPrefab()
    {
        GameObject pref = Resources.Load<GameObject>("Enemies/BOSS");
        if (pref != null) return pref;
        pref = Resources.Load<GameObject>("BOSS");
        if (pref != null) return pref;
        pref = Resources.Load<GameObject>("boss");
        if (pref != null) return pref;

        enemyAI[] all = FindObjectsOfType<enemyAI>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (IsBoss(all[i].gameObject))
                return all[i].gameObject;
        }

        return null;
    }

    private static bool IsBoss(GameObject obj)
    {
        if (obj == null) return false;
        string n = obj.name.ToLowerInvariant();
        return n.Contains("boss");
    }
}
