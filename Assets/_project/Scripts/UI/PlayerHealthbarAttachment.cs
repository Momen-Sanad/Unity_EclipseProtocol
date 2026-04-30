using EclipseProtocol.Player;
using Ilumisoft.HealthSystem;
using Ilumisoft.HealthSystem.UI;
using UnityEngine;

namespace EclipseProtocol.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealthbarAttachment : MonoBehaviour
    {
        [SerializeField] private Healthbar healthbarPrefab;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.35f, 0f);
        [SerializeField] private Vector3 localEulerAngles = new Vector3(60f, 0f, 0f);
        [SerializeField] private Vector3 localScale = Vector3.one;

        private Healthbar _healthbarInstance;

        private void Start()
        {
            if (healthbarPrefab == null)
            {
                Debug.LogWarning("[PlayerHealthbarAttachment] No healthbar prefab assigned.", this);
                return;
            }

            HealthComponent health = GetComponent<HealthComponent>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
            }

            PlayerController playerController = GetComponent<PlayerController>();
            health.MaxHealth = playerController.MaxHealth;
            health.SetHealth(playerController.CurrentHealth);

            _healthbarInstance = Instantiate(healthbarPrefab, transform);
            _healthbarInstance.name = healthbarPrefab.name;
            _healthbarInstance.Health = health;

            Transform healthbarTransform = _healthbarInstance.transform;
            healthbarTransform.localPosition = localOffset;
            healthbarTransform.localRotation = Quaternion.Euler(localEulerAngles);
            healthbarTransform.localScale = localScale;
        }
    }
}
