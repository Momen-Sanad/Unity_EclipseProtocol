using EclipseProtocol.Core;
using EclipseProtocol.Audio;
using Ilumisoft.HealthSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EclipseProtocol.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private CapsuleCollider playerCollider;

        [Header("Collision Layers")]
        [SerializeField] private string playerLayerName = "Player";
        [SerializeField] private string enemyLayerName = "Enemy";

        private Vector2 _moveInput;
        private Vector3 _lastNonZeroMoveDirection = Vector3.forward;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private bool _isDashing;
        private int _playerLayer = -1;
        private int _enemyLayer = -1;
        private HealthComponent _healthComponent;

        public float CurrentHealth { get; private set; }
        public float CurrentEnergy { get; private set; }
        public float MaxHealth => balanceData != null ? balanceData.maxHealth : 100f;
        public float MaxEnergy => balanceData != null ? balanceData.maxEnergy : 100f;
        public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);
        public float DashCooldownDuration => balanceData != null ? balanceData.dashCooldown : 8f;
        public bool IsDashing => _isDashing;
        public bool IsInvulnerable { get; private set; }

        private void Reset()
        {
            playerRigidbody = GetComponent<Rigidbody>();
            playerCollider = GetComponent<CapsuleCollider>();
        }

        private void Awake()
        {
            if (playerRigidbody == null)
            {
                playerRigidbody = GetComponent<Rigidbody>();
            }

            if (playerCollider == null)
            {
                playerCollider = GetComponent<CapsuleCollider>();
            }

            if (balanceData != null)
            {
                CurrentHealth = balanceData.maxHealth;
                CurrentEnergy = balanceData.maxEnergy;
                ApplyRigidbodyTuning();
            }
            else
            {
                CurrentHealth = 100f;
                CurrentEnergy = 100f;
            }

            _playerLayer = LayerMask.NameToLayer(playerLayerName);
            _enemyLayer = LayerMask.NameToLayer(enemyLayerName);

            BindHealthComponent(GetComponent<HealthComponent>());
        }

        private void Update()
        {
            ReadKeyboardInput();

            if (!_isDashing)
            {
                _dashCooldownTimer -= Time.deltaTime;
            }
            else
            {
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0f)
                {
                    EndDash();
                }
            }
        }

        private void FixedUpdate()
        {
            Vector3 desiredMove = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (desiredMove.sqrMagnitude > 1f)
            {
                desiredMove.Normalize();
            }

            if (desiredMove.sqrMagnitude > 0f)
            {
                _lastNonZeroMoveDirection = desiredMove.normalized;
            }

            float speed = balanceData != null ? balanceData.movementSpeed : 6.5f;
            float dashSpeed = balanceData != null ? balanceData.dashSpeed : 14f;
            Vector3 planarVelocity = (_isDashing ? _lastNonZeroMoveDirection * dashSpeed : desiredMove * speed);

#if UNITY_6000_0_OR_NEWER
            Vector3 currentVelocity = playerRigidbody.linearVelocity;
            playerRigidbody.linearVelocity = new Vector3(planarVelocity.x, currentVelocity.y, planarVelocity.z);
#else
            Vector3 currentVelocity = playerRigidbody.velocity;
            playerRigidbody.velocity = new Vector3(planarVelocity.x, currentVelocity.y, planarVelocity.z);
#endif
        }

        public bool TryDash()
        {
            float dashCooldown = balanceData != null ? balanceData.dashCooldown : 8f;
            float dashEnergyCost = balanceData != null ? balanceData.dashEnergyCost : 20f;
            float dashDuration = balanceData != null ? balanceData.dashDuration : 1f;

            if (_isDashing || _dashCooldownTimer > 0f || CurrentEnergy < dashEnergyCost)
            {
                return false;
            }

            if (_lastNonZeroMoveDirection.sqrMagnitude <= 0.001f)
            {
                _lastNonZeroMoveDirection = transform.forward;
            }

            CurrentEnergy = Mathf.Max(0f, CurrentEnergy - dashEnergyCost);
            _dashCooldownTimer = dashCooldown;
            _dashTimer = dashDuration;
            _isDashing = true;
            IsInvulnerable = true;
            SetEnemyCollisionIgnored(true);
            AudioManager.Instance?.PlayDash(transform.position);
            return true;
        }

        public void RestoreEnergy(float amount)
        {
            CurrentEnergy = Mathf.Clamp(CurrentEnergy + amount, 0f, MaxEnergy);
        }

        public void TakeDamage(float amount)
        {
            if (IsInvulnerable || amount <= 0f)
            {
                return;
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth < previousHealth)
            {
                AudioManager.Instance?.PlayDamage(transform.position);
            }

            SyncHealthComponent();
        }

        public void BindHealthComponent(HealthComponent healthComponent)
        {
            _healthComponent = healthComponent;
            SyncHealthComponent();
        }

        private void EndDash()
        {
            _isDashing = false;
            IsInvulnerable = false;
            SetEnemyCollisionIgnored(false);
        }

        private void SetEnemyCollisionIgnored(bool shouldIgnore)
        {
            if (_playerLayer < 0 || _enemyLayer < 0)
            {
                return;
            }

            Physics.IgnoreLayerCollision(_playerLayer, _enemyLayer, shouldIgnore);
        }

        private void ApplyRigidbodyTuning()
        {
            playerRigidbody.mass = balanceData.playerMass;
            playerRigidbody.linearDamping = balanceData.playerDrag;
            playerRigidbody.angularDamping = balanceData.playerAngularDrag;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void SyncHealthComponent()
        {
            if (_healthComponent == null)
            {
                return;
            }

            _healthComponent.MaxHealth = MaxHealth;
            _healthComponent.SetHealth(CurrentHealth);
        }

        private void ReadKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            float x = 0f;
            float y = 0f;

            if (keyboard.aKey.isPressed)
            {
                x -= 1f;
            }
            if (keyboard.dKey.isPressed)
            {
                x += 1f;
            }
            if (keyboard.sKey.isPressed)
            {
                y -= 1f;
            }
            if (keyboard.wKey.isPressed)
            {
                y += 1f;
            }

            _moveInput = new Vector2(x, y);

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                TryDash();
            }
        }
    }
}
