using System;
using UnityEngine;

namespace EclipseProtocol.Core
{
    [CreateAssetMenu(
        fileName = "GameBalanceData",
        menuName = "Eclipse Protocol/Game Balance Data",
        order = 0)]
    public class GameBalanceData : ScriptableObject
    {
        [Serializable]
        public class DifficultySettings
        {
            [Min(0.1f)] public float energyRestorationMultiplier = 1f;
            [Min(0f)] public float damageTakenMultiplier = 1f;
            [Min(0f)] public float enemyCountMultiplier = 1f;
            [Min(0f)] public float powerNodeCountMultiplier = 1f;
            public int roomCountOffset;
            [Min(0.1f)] public float completionTimeMultiplier = 1f;
        }

        [Header("Player Stats")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(1f)] public float maxEnergy = 100f;

        [Header("Player Movement")]
        [Min(0.1f)] public float movementSpeed = 6.5f;
        [Min(0.1f)] public float jumpVelocity = 7.5f;
        [Min(0.05f)] public float groundedRayDistance = 1.15f;
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

        [Header("Phase 2 Run")]
        [Min(15f)] public float runTimerSeconds = 180f;
        [Min(0)] public int startingScore = 5000;
        [Min(0f)] public float scoreLossPerSecond = 10f;
        [Min(0f)] public float scoreLossPerHealthPoint = 25f;
        [Min(1)] public int corridorRoomCount = 2;
        [Min(0)] public int maxPlacementAttempts = 8;
        [Min(0f)] public float roomOverlapPadding = 0.25f;

        [Header("Objectives")]
        [Min(0.1f)] public float repairHoldSeconds = 3f;
        [Min(0.1f)] public float lockedExtractionMessageSeconds = 2f;

        [Header("Hunter Lunge")]
        [Min(0.1f)] public float hunterAttackRange = 2.25f;
        [Min(0.05f)] public float hunterWindupSeconds = 0.45f;
        [Min(0.05f)] public float hunterLungeSeconds = 0.45f;
        [Min(0.1f)] public float hunterLungeSpeed = 11f;
        [Min(0.1f)] public float hunterAttackCooldown = 1.75f;
        [Min(1f)] public float hunterDamage = 15f;

        [Header("Content Spawning")]
        [Range(0f, 1f)] public float corridorEnergyCellChance = 0.65f;
        [Range(0f, 1f)] public float corridorPatrolDroneChance = 0.6f;
        [Min(0)] public int maxPatrolDrones = 2;
        [Min(0)] public int maxEnergyCells = 3;

        [Header("Energy Pickup")]
        [Min(1f)] public float energyCellRestoreAmount = 25f;

        [Header("Difficulty")]
        public DifficultySettings easy = new DifficultySettings
        {
            energyRestorationMultiplier = 1.4f,
            damageTakenMultiplier = 0.75f,
            enemyCountMultiplier = 0.7f,
            powerNodeCountMultiplier = 0.75f,
            roomCountOffset = -1,
            completionTimeMultiplier = 1.25f
        };

        public DifficultySettings medium = new DifficultySettings();

        public DifficultySettings hard = new DifficultySettings
        {
            energyRestorationMultiplier = 0.7f,
            damageTakenMultiplier = 1.35f,
            enemyCountMultiplier = 1.35f,
            powerNodeCountMultiplier = 1.35f,
            roomCountOffset = 2,
            completionTimeMultiplier = 0.75f
        };

        public DifficultySettings GetDifficultySettings()
        {
            return GetDifficultySettings(RunDifficultyData.CurrentDifficulty);
        }

        public DifficultySettings GetDifficultySettings(DifficultyMode difficulty)
        {
            switch (difficulty)
            {
                case DifficultyMode.Easy:
                    return easy ?? medium ?? new DifficultySettings();
                case DifficultyMode.Hard:
                    return hard ?? medium ?? new DifficultySettings();
                default:
                    return medium ?? new DifficultySettings();
            }
        }

        public float GetEffectiveEnergyCellRestoreAmount()
        {
            return energyCellRestoreAmount * GetDifficultySettings().energyRestorationMultiplier;
        }

        public float GetEffectiveDamageTaken(float damage)
        {
            return damage * GetDifficultySettings().damageTakenMultiplier;
        }

        public float GetEffectiveRunTimerSeconds()
        {
            return runTimerSeconds * GetDifficultySettings().completionTimeMultiplier;
        }

        public int GetEffectiveRoomCount(int baseRoomCount)
        {
            return Mathf.Max(3, baseRoomCount + GetDifficultySettings().roomCountOffset);
        }

        public int GetEffectiveEnemyCount(int baseEnemyCount)
        {
            return Mathf.Max(0, Mathf.RoundToInt(baseEnemyCount * GetDifficultySettings().enemyCountMultiplier));
        }

        public int GetEffectivePowerNodeCount(int basePowerNodeCount)
        {
            return Mathf.Max(0, Mathf.RoundToInt(basePowerNodeCount * GetDifficultySettings().powerNodeCountMultiplier));
        }
    }
}
