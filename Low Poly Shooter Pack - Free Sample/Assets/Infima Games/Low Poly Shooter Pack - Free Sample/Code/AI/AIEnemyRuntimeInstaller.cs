using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfimaGames.LowPolyShooterPack.AI
{
    public static class AIEnemyRuntimeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "S_Content_Overview")
                return;

            CharacterBehaviour player = ServiceLocator.Current.Get<IGameModeService>().GetPlayerCharacter();
            if (player == null)
                return;

            if (player.GetComponent<AIPlayerHealth>() == null)
                player.gameObject.AddComponent<AIPlayerHealth>();

            if (Object.FindObjectOfType<AIEnemyController>() != null)
                return;

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "AI Enemy (Runtime)";
            enemy.transform.position = player.transform.position + player.transform.forward * 18.0f + Vector3.right * 6.0f;
            enemy.transform.localScale = new Vector3(1.0f, 1.9f, 1.0f);

            AIEnemyPerception perception = enemy.AddComponent<AIEnemyPerception>();
            AIEnemyCombat combat = enemy.AddComponent<AIEnemyCombat>();
            AIEnemyHealth health = enemy.AddComponent<AIEnemyHealth>();
            AIEnemyController controller = enemy.AddComponent<AIEnemyController>();

            GameObject firePointObject = new GameObject("Fire Point");
            firePointObject.transform.SetParent(enemy.transform);
            firePointObject.transform.localPosition = new Vector3(0.0f, 1.35f, 0.35f);
            combat.SetFirePoint(firePointObject.transform);

            AIEnemyPatrolPoint[] patrolPoints = CreatePatrolPoints(enemy.transform.position);
            controller.SetPatrolPoints(patrolPoints);
        }

        private static AIEnemyPatrolPoint[] CreatePatrolPoints(Vector3 center)
        {
            AIEnemyPatrolPoint[] points = new AIEnemyPatrolPoint[4];
            Vector3[] offsets =
            {
                new Vector3(8.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 8.0f),
                new Vector3(-8.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.0f, -8.0f)
            };

            for (int i = 0; i < points.Length; i++)
            {
                GameObject pointObject = new GameObject($"AI Patrol Point {i + 1}");
                pointObject.transform.position = center + offsets[i];
                points[i] = pointObject.AddComponent<AIEnemyPatrolPoint>();
            }

            return points;
        }
    }
}
