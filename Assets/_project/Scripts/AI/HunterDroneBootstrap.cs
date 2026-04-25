using EclipseProtocol.Core;
using EclipseProtocol.Player;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseProtocol.AI
{
    public static class HunterDroneBootstrap
    {
        private const string HunterObjectName = "HunterDrone";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnHunterDroneOnPlay()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                return;
            }

            HunterDroneAI existingHunter = Object.FindAnyObjectByType<HunterDroneAI>();
            if (existingHunter != null)
            {
                existingHunter.Initialize(FindBalanceData(), player.transform);
                return;
            }

            Vector3 desiredSpawnPosition = player.transform.position + new Vector3(0f, 0f, 8f);
            Vector3 spawnPosition = ResolveSpawnPositionOnNavMesh(desiredSpawnPosition);

            GameObject hunter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hunter.name = HunterObjectName;
            hunter.transform.position = spawnPosition;
            hunter.transform.localScale = Vector3.one * 1.25f;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                hunter.layer = enemyLayer;
            }

            Renderer renderer = hunter.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.35f, 0.2f, 1f);
            }

            Rigidbody body = hunter.AddComponent<Rigidbody>();
            body.isKinematic = true;

            NavMeshAgent agent = hunter.AddComponent<NavMeshAgent>();
            HunterDroneAI hunterAI = hunter.AddComponent<HunterDroneAI>();
            hunterAI.Initialize(FindBalanceData(), player.transform);

            // Ensure the first destination is set even before first AI update tick.
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(player.transform.position);
            }
        }

        private static GameBalanceData FindBalanceData()
        {
            DronePatrolAI patrol = Object.FindAnyObjectByType<DronePatrolAI>();
            return patrol != null ? patrol.BalanceData : null;
        }

        private static Vector3 ResolveSpawnPositionOnNavMesh(Vector3 desiredPosition)
        {
            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return desiredPosition;
        }
    }
}
