using UnityEngine;

namespace EclipseProtocol.Core
{
    [CreateAssetMenu(
        fileName = "GameBalanceData",
        menuName = "Eclipse Protocol/Game Balance Data",
        order = 0)]
    public class GameBalanceData : ScriptableObject
    {
        [Header("Player Stats")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(1f)] public float maxEnergy = 100f;

        [Header("Player Movement")]
        [Min(0.1f)] public float movementSpeed = 6.5f;
        [Min(0.1f)] public float dashSpeed = 14f;
        [Min(0.1f)] public float dashDuration = 1f;
        [Min(0f)] public float dashCooldown = 8f;
        [Min(0f)] public float dashEnergyCost = 20f;

        [Header("Player Rigidbody")]
        [Min(0.01f)] public float playerMass = 1.5f;
        [Min(0f)] public float playerDrag = 6f;
        [Min(0f)] public float playerAngularDrag = 0.05f;

        [Header("Drone AI")]
        [Min(0.1f)] public float droneMoveSpeed = 3.5f;
        [Min(0.1f)] public float droneAcceleration = 8f;
        [Min(0f)] public float droneStoppingDistance = 0.2f;
        [Min(0.1f)] public float droneDetectionRadius = 8f;

        [Header("Energy Pickup")]
        [Min(1f)] public float energyCellRestoreAmount = 25f;
    }
}
