using System;
using EclipseProtocol.Audio;
using EclipseProtocol.Player;
using UnityEngine;

namespace EclipseProtocol.World
{
    public class EnergyCellSystem : MonoBehaviour
    {
        private static EnergyCellSystem _instance;

        public static EnergyCellSystem Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindAnyObjectByType<EnergyCellSystem>();
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject systemObject = new GameObject(nameof(EnergyCellSystem));
                _instance = systemObject.AddComponent<EnergyCellSystem>();
                return _instance;
            }
        }

        public event Action<EnergyCellPickup, PlayerController, float> EnergyRestored;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public bool TryCollect(EnergyCellPickup pickup, PlayerController player)
        {
            if (pickup == null || player == null || !pickup.gameObject.activeInHierarchy)
            {
                return false;
            }

            float restoredEnergy = player.RestoreEnergy(pickup.EnergyRestoreAmount);
            AudioManager.Instance?.PlayPickup(pickup.transform.position);
            EnergyRestored?.Invoke(pickup, player, restoredEnergy);
            pickup.Consume();
            return true;
        }
    }
}
