using EclipseProtocol.Player;
using Ilumisoft.HealthSystem;
using Ilumisoft.HealthSystem.UI;
using UnityEngine;

/* This component is responsible for attaching a healthbar UI element to the player character.
   It instantiates a healthbar prefab, binds it to the player's health component, 
   and updates its position and rotation to always face the camera.
   The healthbar prefab should be a world-space UI element that can display the player's health visually. 
   The localOffset variable allows you to adjust the position of the healthbar relative to the player, 
   while billboardRotationOffset can be used to fine-tune the rotation of the healthbar to ensure it faces the camera correctly. */
namespace EclipseProtocol.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealthbarAttachment : MonoBehaviour
    {
        [SerializeField] private Healthbar healthbarPrefab;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.35f, 0f);
        [SerializeField] private Vector3 billboardRotationOffset = Vector3.zero;
        [SerializeField] private Vector3 localScale = Vector3.one;

        private Healthbar _healthbarInstance;
        private Camera _camera;
        private HealthComponent _health;

        private void Start()
        {
            if (healthbarPrefab == null)
            {
                Debug.LogWarning("[PlayerHealthbarAttachment] No healthbar prefab assigned.", this);
                return;
            }

            _camera = Camera.main;

            _health = GetComponent<HealthComponent>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<Health>();
            }

            PlayerController playerController = GetComponent<PlayerController>();
            playerController.BindHealthComponent(_health);

            _healthbarInstance = Instantiate(healthbarPrefab);
            _healthbarInstance.name = healthbarPrefab.name;
            _healthbarInstance.Health = _health;

            Transform healthbarTransform = _healthbarInstance.transform;
            healthbarTransform.localScale = localScale;
        }

        private void LateUpdate()
        {
            if (_healthbarInstance == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Transform healthbarTransform = _healthbarInstance.transform;

            healthbarTransform.position = transform.position + localOffset;

            Vector3 directionToCamera = _camera.transform.position - healthbarTransform.position;
            if (directionToCamera.sqrMagnitude > 0.0001f)
            {
                healthbarTransform.rotation =
                    Quaternion.LookRotation(directionToCamera, Vector3.up) *
                    Quaternion.Euler(billboardRotationOffset);
            }
        }
    }
}