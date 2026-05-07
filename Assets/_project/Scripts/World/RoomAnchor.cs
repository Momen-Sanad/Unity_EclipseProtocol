using UnityEngine;

namespace EclipseProtocol.World
{
    public class RoomAnchor : MonoBehaviour
    {
        [SerializeField] private bool isEntry;

        public bool IsEntry => isEntry;
    }
}
