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
            enemy.transform.localScale = Vector3.one;
            enemy.transform.position = ResolveSpawnPosition(player.transform, enemy);

            AIEnemyPerception perception = enemy.AddComponent<AIEnemyPerception>();
            AIEnemyCombat combat = enemy.AddComponent<AIEnemyCombat>();
            AIEnemyHealth health = enemy.AddComponent<AIEnemyHealth>();
            AIEnemyController controller = enemy.AddComponent<AIEnemyController>();

            GameObject firePointObject = new GameObject("Fire Point");
            firePointObject.transform.SetParent(enemy.transform);
            firePointObject.transform.localPosition = new Vector3(0.0f, 1.35f, 0.35f);
            combat.SetFirePoint(firePointObject.transform);

            controller.SetTarget(player.transform);
        }

        private static Vector3 ResolveSpawnPosition(Transform player, GameObject enemy)
        {
            Vector3 baseForward = player.forward;
            baseForward.y = 0.0f;
            if (baseForward.sqrMagnitude < 0.01f)
                baseForward = Vector3.forward;
            baseForward.Normalize();

            Vector3 desired = player.position + baseForward * 5.0f;
            Vector3 rayStart = desired + Vector3.up * 20.0f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 60.0f, ~0))
            {
                // Place capsule bottom slightly above ground.
                float halfHeight = 1.0f;
                if (enemy.TryGetComponent(out CapsuleCollider collider))
                    halfHeight = collider.height * 0.5f * enemy.transform.localScale.y;
                return hit.point + Vector3.up * (halfHeight + 0.05f);
            }

            return player.position + baseForward * 5.0f + Vector3.up * 1.05f;
        }

    }
}
