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

        public float EnergyRestoreAmount => energyRestoreAmount;

        private void Awake()
        {
            if (balanceData != null)
            {
                energyRestoreAmount = balanceData.GetEffectiveEnergyCellRestoreAmount();
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

            EnergyCellSystem.Instance.TryCollect(this, playerController);
        }

        public void Consume()
        {
            gameObject.SetActive(false);
        }
    }
}
