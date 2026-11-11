#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MushroomSurvival
{
    public sealed class MovementSmokeProbe : MonoBehaviour
    {
        private const ulong DriverId = 1;
        private NetworkObject driver;
        private NetworkObject stationary;
        private ulong localClientId;
        private Keyboard keyboard;
        private Vector3 initialPosition;
        private Vector3 stationaryPosition;
        private float deadline;
        private float collisionAt;
        private uint sequenceBeforeCollision;
        private bool movementVerified;
        private bool collisionInjected;

        public void Initialize(NetworkManager network)
        {
            localClientId = network.LocalClientId;
            if (!PlayerCollisionSweep.TryHit(new Vector3(-2f, 0f, 0f), new Vector3(4f, 0f, 0f),
                new Vector3(2f, 0f, 0f), new Vector3(-4f, 0f, 0f), Vector3.one,
                out float hitTime, out Vector3 normal) ||
                !Mathf.Approximately(hitTime, 0.375f) || normal != Vector3.left)
            {
                Fail("Opposing paths must stop at their first contact.");
                return;
            }
            foreach (var player in network.SpawnManager.SpawnedObjectsList)
            {
                if (!player.IsPlayerObject)
                    continue;
                if (player.OwnerClientId == DriverId)
                    driver = player;
                if (player.OwnerClientId == NetworkManager.ServerClientId)
                    stationary = player;
            }
            if (driver == null || stationary == null)
            {
                Fail("Expected both the host and first guest to be spawned.");
                return;
            }

            initialPosition = driver.transform.position;
            deadline = Time.realtimeSinceStartup + 7f;
            if (driver.IsOwner)
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            }
        }

        private void Update()
        {
            if (driver == null)
                return;
            if (!movementVerified && driver.transform.position.x - initialPosition.x >= 1f)
            {
                movementVerified = true;
                Debug.Log("MUSHROOM_MOVE_PASSED guest movement observed by " + localClientId);
                if (!driver.IsOwner)
                {
                    enabled = false;
                    return;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                collisionAt = Time.realtimeSinceStartup + 0.8f;
            }

            if (movementVerified && !collisionInjected && Time.realtimeSinceStartup >= collisionAt)
            {
                collisionInjected = true;
                stationaryPosition = stationary.transform.position;
                var body = driver.GetComponent<Rigidbody>();
                sequenceBeforeCollision = driver.GetComponent<PlayerPositionCorrection>().LastAppliedSequence;
                body.linearVelocity = Vector3.zero;
                body.position = stationaryPosition;
                driver.GetComponent<OwnerNetworkTransform>().Teleport(
                    stationaryPosition, body.rotation, driver.transform.localScale);
            }

            if (collisionInjected &&
                driver.GetComponent<PlayerPositionCorrection>().LastAppliedSequence != sequenceBeforeCollision)
            {
                var difference = driver.transform.position - stationary.transform.position;
                if (Mathf.Max(Mathf.Abs(difference.x), Mathf.Abs(difference.z)) >= 0.97f &&
                    Vector2.Distance(new Vector2(stationary.transform.position.x, stationary.transform.position.z),
                        new Vector2(stationaryPosition.x, stationaryPosition.z)) < 0.1f)
                {
                    Debug.Log("MUSHROOM_CORRECTION_PASSED guest separated without pushing the host");
                    enabled = false;
                    return;
                }
            }

            if (Time.realtimeSinceStartup >= deadline)
                Fail(movementVerified ? "Host position correction did not settle." : "Guest movement was not replicated.");
        }

        private void OnDestroy()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
        }

        private void Fail(string message)
        {
            enabled = false;
            Debug.LogError("MUSHROOM_MOVE_FAILED " + message);
            Application.Quit(1);
        }
    }
}
#endif
