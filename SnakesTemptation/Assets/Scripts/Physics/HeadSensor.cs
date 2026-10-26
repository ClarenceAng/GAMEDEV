using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The snake's head in the physics world: a small trigger sphere on a kinematic rigidbody. Unity reports every collider
    /// it starts to overlap through <see cref="OnTriggerEnter"/>, and it keeps what those colliders are until they are read.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class HeadSensor : MonoBehaviour
    {
        /// <summary>What the head touched since this was last cleared.</summary>
        public readonly List<Contact> Touched = new List<Contact>();

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Touchable touchable)) Touched.Add(touchable.kind);
        }
    }
}
