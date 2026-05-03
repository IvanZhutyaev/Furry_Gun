using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Auto-spawns enemies at game start around the player.
/// Spawns only once per play session.
/// </summary>
public sealed class EnemyAutoSpawner : MonoBehaviour
{
    [SerializeField] private int spawnCount = 1;
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
    [Tooltip("Множитель высоты/радиуса капсулы проверки свободного места при спавне босса относительно обычного врага.")]
    [SerializeField] private float bossSpawnCapsuleScale = 4f;

    private const string EditorBossPrefabPath =
        "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Code/Enemies/BOSS.prefab";

    private readonly Collider[] overlapHits = new Collider[24];

    private static bool spawnedOnce;
    private bool bossSpawned;
    private GameObject playerObject;

    #region agent log
    private static void AgentLog(string hypothesisId, string location, string message, string dataJsonObject)
    {
        try
        {
            long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sb = new StringBuilder(256);
            sb.Append("{\"sessionId\":\"218006\",\"hypothesisId\":\"").Append(hypothesisId)
                .Append("\",\"location\":\"").Append(location.Replace("\\", "/"))
                .Append("\",\"message\":\"").Append(message.Replace("\\", "\\\\").Replace("\"", "\\\""))
                .Append("\",\"timestamp\":").Append(ts).Append(",\"data\":").Append(string.IsNullOrEmpty(dataJsonObject) ? "{}" : dataJsonObject).Append("}\n");
            string line = sb.ToString();
            string projectLog = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-218006.log"));
            File.AppendAllText(projectLog, line, Encoding.UTF8);
            File.AppendAllText(Path.Combine(Application.persistentDataPath, "debug-218006.log"), line, Encoding.UTF8);
        }
        catch
        {
            // ignored — debug ingest must never break play mode
        }
    }

    private static string BuildEnemyBlockingSnapshotJson()
    {
        enemyAI[] all = FindObjectsOfType<enemyAI>();
        int blockingNonBoss = 0;
        int bossLike = 0;
        int disabledNonBoss = 0;
        for (int i = 0; i < all.Length; i++)
        {
            enemyAI ai = all[i];
            if (ai == null)
                continue;
            bool boss = IsBoss(ai.gameObject);
            if (boss)
            {
                bossLike++;
                continue;
            }

            if (!ai.enabled || !ai.gameObject.activeInHierarchy)
                disabledNonBoss++;
            else
                blockingNonBoss++;
        }

        return "{\"enemyAiTotal\":" + all.Length + ",\"blockingNonBoss\":" + blockingNonBoss +
               ",\"bossLike\":" + bossLike + ",\"disabledOrInactiveNonBoss\":" + disabledNonBoss + "}";
    }
    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (spawnedOnce)
            return;

        GameObject go = new GameObject("Enemy Auto Spawner");
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyAutoSpawner>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (spawnedOnce)
            return;
        StopAllCoroutines();
        StartCoroutine(RunSpawnAttempt());
    }

    private IEnumerator Start()
    {
        yield return RunSpawnAttempt();
    }

    /// <summary>Повтор при смене сцены (например главное меню без игрока → уровень с игроком).</summary>
    private IEnumerator RunSpawnAttempt()
    {
        // Wait one frame so Player/NavMesh in scene are initialized.
        yield return null;

        if (spawnedOnce)
        {
            AgentLog("H2", "EnemyAutoSpawner:Start", "early_exit_spawned_once", "{}");
            yield break;
        }

        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            AgentLog("H2", "EnemyAutoSpawner:Start", "early_exit_no_player", "{}");
            yield break;
        }

        GameObject enemyPrefab = ResolveEnemyPrefab();
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemyAutoSpawner] Enemy prefab not found. Put prefab into Resources/Enemies/enemy.prefab or keep one enemy in scene.");
            AgentLog("H2", "EnemyAutoSpawner:Start", "early_exit_no_enemy_prefab", "{}");
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
        AgentLog("H2", "EnemyAutoSpawner:Start", "wave_complete_start_boss_coroutine",
            "{\"spawned\":" + spawned + ",\"attempts\":" + attempts + "}");
        StartCoroutine(SpawnBossWhenAllEnemiesDead());
    }

    private IEnumerator SpawnBossWhenAllEnemiesDead()
    {
        bool loggedSnapshot;
        loggedSnapshot = false;
        while (!bossSpawned)
        {
            yield return new WaitForSeconds(bossCheckInterval);

            if (!loggedSnapshot)
            {
                loggedSnapshot = true;
                AgentLog("H3", "EnemyAutoSpawner:SpawnBossWhenAllEnemiesDead", "first_tick_snapshot",
                    BuildEnemyBlockingSnapshotJson());
            }

            bool anyBlocking = HasAliveNonBossEnemies();
            if (!anyBlocking)
            {
                AgentLog("H3", "EnemyAutoSpawner:SpawnBossWhenAllEnemiesDead", "no_blocking_non_boss_try_spawn",
                    BuildEnemyBlockingSnapshotJson());

                GameObject bossPrefabBefore = ResolveBossPrefab();
                AgentLog("H1", "EnemyAutoSpawner:SpawnBossWhenAllEnemiesDead", "boss_prefab_resolve",
                    "{\"prefabNull\":" + (bossPrefabBefore == null ? "true" : "false") +
                    ",\"prefabName\":\"" + (bossPrefabBefore != null ? bossPrefabBefore.name.Replace("\"", "'") : "") + "\"}");

                if (bossPrefabBefore == null)
                {
                    Debug.LogError(
                        "[EnemyAutoSpawner] Префаб босса не найден: в билде положите копию в Assets/Resources/Enemies/BOSS.prefab " +
                        "(Resources.Load(\"Enemies/BOSS\")). В редакторе Play Mode используется путь к ассету из скрипта.");
                    AgentLog("H1", "EnemyAutoSpawner:SpawnBossWhenAllEnemiesDead", "abort_no_prefab_stop_coroutine",
                        "{}");
                    bossSpawned = true;
                    Destroy(gameObject);
                    yield break;
                }

                bool spawnOk = SpawnBossAtMapCenterWithResult();

                AgentLog("H4", "EnemyAutoSpawner:SpawnBossWhenAllEnemiesDead", "spawn_result",
                    "{\"spawnOk\":" + (spawnOk ? "true" : "false") + "}");

                if (spawnOk)
                {
                    bossSpawned = true;
                    Destroy(gameObject);
                    yield break;
                }

                // Prefab есть, но точка спавна не прошла — повторим на следующем тике (например другой random offset).
                continue;
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

    /// <returns>True if boss instance was created.</returns>
    private bool SpawnBossAtMapCenterWithResult()
    {
        GameObject bossPrefab = ResolveBossPrefab();
        if (bossPrefab == null)
        {
            Debug.LogWarning("[EnemyAutoSpawner] Boss prefab not found. Put it into Resources/Enemies/BOSS.prefab or keep one boss in scene.");
            AgentLog("H1", "EnemyAutoSpawner:SpawnBossAtMapCenter", "abort_prefab_null", "{}");
            return false;
        }

        Vector3 mapCenter = ResolveMapCenter();
        AgentLog("H4", "EnemyAutoSpawner:SpawnBossAtMapCenter", "map_center",
            "{\"x\":" + mapCenter.x.ToString("R") + ",\"y\":" + mapCenter.y.ToString("R") + ",\"z\":" + mapCenter.z.ToString("R") + "}");

        if (!TryResolveBossSpawnPoint(mapCenter, out Vector3 spawnPos))
        {
            Debug.LogWarning("[EnemyAutoSpawner] Could not resolve valid boss spawn point near map center.");
            AgentLog("H4", "EnemyAutoSpawner:SpawnBossAtMapCenter", "abort_spawn_point_unresolved", "{}");
            return false;
        }

        Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        BossSpawnBanner.Show("Босс здесь!");
        AgentLog("H5", "EnemyAutoSpawner:SpawnBossAtMapCenter", "instantiate_called",
            "{\"spawnPosY\":" + spawnPos.y.ToString("R") + "}");
        return true;
    }

    private bool TryResolveBossSpawnPoint(Vector3 center, out Vector3 spawnPos)
    {
        float bh = enemyCapsuleHeight * bossSpawnCapsuleScale;
        float br = enemyCapsuleRadius * bossSpawnCapsuleScale;

        if (TryResolveSpawnPoint(center, out spawnPos, bh, br))
            return true;

        for (int i = 0; i < bossCenterSearchAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * bossCenterSearchRadius;
            if (TryResolveSpawnPoint(center + new Vector3(offset.x, 0f, offset.y), out spawnPos, bh, br))
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
        return TryResolveSpawnPoint(guess, out spawnPos, enemyCapsuleHeight, enemyCapsuleRadius);
    }

    private bool TryResolveSpawnPoint(Vector3 guess, out Vector3 spawnPos, float capsuleHeight, float capsuleRadius)
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
        Vector3 bottom = p + Vector3.up * (capsuleRadius + footClearance);
        Vector3 top = p + Vector3.up * Mathf.Max(capsuleRadius + footClearance, capsuleHeight - capsuleRadius);

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            capsuleRadius,
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

    private GameObject ResolveBossPrefab()
    {
        GameObject pref = Resources.Load<GameObject>("Enemies/BOSS");
        if (pref != null) return pref;
        pref = Resources.Load<GameObject>("BOSS");
        if (pref != null) return pref;
        pref = Resources.Load<GameObject>("boss");
        if (pref != null) return pref;

#if UNITY_EDITOR
        // Префаб лежит вне Resources — в Play Mode в редакторе грузим напрямую по пути ассета.
        pref = AssetDatabase.LoadAssetAtPath<GameObject>(EditorBossPrefabPath);
        if (pref != null)
            return pref;
#endif

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
