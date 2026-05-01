using System.Collections.Generic;
using EclipseProtocol.Player;
using UnityEngine;

namespace EclipseProtocol.World
{
    [RequireComponent(typeof(Collider))]
    public class RoomDoorTransition : MonoBehaviour
    {
        private static readonly Dictionary<PlayerController, float> LastTransitionTimes = new Dictionary<PlayerController, float>();

        [SerializeField] private Transform destinationPoint;
        [SerializeField] private bool useDestinationRotation = true;
        [SerializeField, Min(0f)] private float transitionCooldown = 0.35f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            LastTransitionTimes.Clear();
        }

        private void Awake()
        {
            Collider doorCollider = GetComponent<Collider>();
            doorCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled)
            {
                return;
            }

            if (destinationPoint == null)
            {
                Debug.LogWarning($"[RoomDoorTransition] No destination assigned on {name}.", this);
                return;
            }

            if (LastTransitionTimes.TryGetValue(player, out float lastTransitionTime)
                && Time.time - lastTransitionTime < transitionCooldown)
            {
                return;
            }

            LastTransitionTimes[player] = Time.time;

            Quaternion targetRotation = useDestinationRotation ? destinationPoint.rotation : player.transform.rotation;
            player.TeleportTo(destinationPoint.position, targetRotation);
        }
    }
}
