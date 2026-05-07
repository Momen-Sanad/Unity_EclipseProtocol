using UnityEngine;

namespace EclipseProtocol.World
{
    public class RoomModule : MonoBehaviour
    {
        [SerializeField] private RoomType roomType;
        [SerializeField] private RoomAnchor entryAnchor;
        [SerializeField] private RoomAnchor exitAnchor;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform objectiveSocket;
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] pickupSpawnPoints;
        [SerializeField] private Transform[] patrolWaypointPoints;
        [SerializeField] private Vector3 boundsCenter = Vector3.zero;
        [SerializeField] private Vector3 boundsSize = new Vector3(18f, 6f, 18f);

        public RoomType RoomType => roomType;
        public RoomAnchor EntryAnchor => entryAnchor;
        public RoomAnchor ExitAnchor => exitAnchor;
        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public Transform ObjectiveSocket => objectiveSocket;
        public Transform[] EnemySpawnPoints => enemySpawnPoints;
        public Transform[] PickupSpawnPoints => pickupSpawnPoints;
        public Transform[] PatrolWaypointPoints => patrolWaypointPoints;
        public Vector3 BoundsCenter => boundsCenter;
        public Vector3 BoundsSize => boundsSize;

        public Bounds BuildWorldBounds(Vector3 position, Quaternion rotation, float padding)
        {
            Vector3 center = position + rotation * boundsCenter;
            Vector3 size = new Vector3(
                Mathf.Max(0f, boundsSize.x - padding),
                boundsSize.y,
                Mathf.Max(0f, boundsSize.z - padding));

            return new Bounds(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(boundsCenter, boundsSize);
        }
    }
}
