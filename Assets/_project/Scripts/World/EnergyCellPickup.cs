using EclipseProtocol.Core;
using EclipseProtocol.Audio;
using EclipseProtocol.Player;
using EclipseProtocol.UI;
using UnityEngine;

namespace EclipseProtocol.World
{
    [RequireComponent(typeof(Collider))]
    public class EnergyCellPickup : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField, Min(1f)] private float energyRestoreAmount = 25f;

        private void Awake()
        {
            if (balanceData != null)
            {
                energyRestoreAmount = balanceData.energyCellRestoreAmount;
            }

            Collider pickupCollider = GetComponent<Collider>();
            pickupCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerController playerController))
            {
                return;
            }

            float restoredEnergy = playerController.RestoreEnergy(energyRestoreAmount);
            FindAnyObjectByType<HUDController>()?.ShowEnergyGain(restoredEnergy);
            AudioManager.Instance?.PlayPickup(transform.position);
            Debug.Log($"[EnergyCellPickup] Restored {restoredEnergy} energy to {other.name}.", this);
            gameObject.SetActive(false);
        }
    }
}
