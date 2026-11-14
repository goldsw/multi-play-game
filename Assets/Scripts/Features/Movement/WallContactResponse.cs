using System.Collections.Generic;
using UnityEngine;

namespace MushroomSurvival
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WallContactResponse : MonoBehaviour
    {
        private readonly List<Vector3> wallNormals = new(4);
        private Rigidbody body;

        public Vector3 LastReactionForce { get; private set; }

        private void Awake() => body = GetComponent<Rigidbody>();

        private void OnCollisionEnter(Collision collision) => RecordContacts(collision);
        private void OnCollisionStay(Collision collision) => RecordContacts(collision);

        private void RecordContacts(Collision collision)
        {
            if (body.isKinematic || collision.rigidbody != null)
                return;

            for (int index = 0; index < collision.contactCount; index++)
            {
                var contact = collision.GetContact(index);
                if (Mathf.Abs(contact.normal.y) > 0.1f)
                    continue;

                var normal = new Vector3(contact.normal.x, 0f, contact.normal.z).normalized;
                if (Vector3.Dot(body.worldCenterOfMass - contact.point, normal) < 0f)
                    normal = -normal;

                bool known = false;
                foreach (var previous in wallNormals)
                {
                    if (Vector3.Dot(previous, normal) > 0.999f)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                    wallNormals.Add(normal);
            }
        }

        public void ApplyReaction(Vector3 movementForce)
        {
            var allowedForce = movementForce;
            foreach (var normal in wallNormals)
            {
                float intoWall = Vector3.Dot(allowedForce, normal);
                if (intoWall < 0f)
                    allowedForce -= intoWall * normal;
            }

            wallNormals.Clear();
            LastReactionForce = allowedForce - movementForce;
            body.AddForce(LastReactionForce, ForceMode.Force);
        }

        public void ClearContacts()
        {
            wallNormals.Clear();
            LastReactionForce = Vector3.zero;
        }

        private void OnDisable() => ClearContacts();
    }
}
