using UnityEngine;

namespace OfficeImposter
{
    // A place the player can sit. The anchor supplies both the seated position and
    // the direction the player faces while seated.
    public class Seat : MonoBehaviour
    {
        [SerializeField] Transform anchor;
        [SerializeField] float eyeHeight = 1.18f;
        [SerializeField] float yawRange = 110f;

        PlayerController _occupant;

        public float EyeHeight => eyeHeight;
        public float YawRange => yawRange;
        public bool IsTaken => _occupant != null;
        public Vector3 Position => anchor != null ? anchor.position : transform.position;
        public Quaternion Rotation => anchor != null ? anchor.rotation : transform.rotation;

        public void Configure(Transform seatAnchor, float height, float yaw)
        {
            anchor = seatAnchor;
            eyeHeight = height;
            yawRange = yaw;
        }

        public bool TryClaim(PlayerController player)
        {
            if (_occupant != null && _occupant != player) return false;
            _occupant = player;
            return true;
        }

        public void Release(PlayerController player)
        {
            if (_occupant == player) _occupant = null;
        }
    }
}
