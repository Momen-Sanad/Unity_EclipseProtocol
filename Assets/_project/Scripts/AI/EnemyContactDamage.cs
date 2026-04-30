using EclipseProtocol.Player;
using UnityEngine;

namespace EclipseProtocol.AI
{
    [DisallowMultipleComponent]
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damageAmount = 10f;
        [SerializeField, Min(0f)] private float damageCooldown = 1f;

        private float _nextDamageTime;

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
            if (Time.time < _nextDamageTime)
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
