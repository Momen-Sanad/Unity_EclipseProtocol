using EclipseProtocol.Core;
using EclipseProtocol.Player;
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

            playerController.RestoreEnergy(energyRestoreAmount);
            Debug.Log($"[EnergyCellPickup] Restored {energyRestoreAmount} energy to {other.name}.", this);
            gameObject.SetActive(false);
        }
    }
}
