using EclipseProtocol.Player;
using UnityEngine;

namespace EclipseProtocol.AI
{
    [DisallowMultipleComponent]
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damageAmount = 10f;
        [SerializeField, Min(0f)] private float damageCooldown = 1f;
        [SerializeField] private bool damageEnabled = true;

        private float _nextDamageTime;

        public void Configure(float amount, float cooldown, bool enabled)
        {
            damageAmount = Mathf.Max(0f, amount);
            damageCooldown = Mathf.Max(0f, cooldown);
            damageEnabled = enabled;
        }

        public void SetDamageEnabled(bool enabled)
        {
            damageEnabled = enabled;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDamage(collision.collider);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryDamage(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider other)
        {
            if (!damageEnabled || Time.time < _nextDamageTime)
            {
                return;
            }

            if (!other.TryGetComponent(out PlayerController playerController))
            {
                return;
            }

            playerController.TakeDamage(damageAmount);
            _nextDamageTime = Time.time + damageCooldown;
        }
    }
}
