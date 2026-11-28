#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MushroomSurvival
{
    /// <summary>
    /// Host eliminates guest 1; guest 1 then right/left-clicks through the survivors (0, 2, 3) with simulated mouse input.
    /// </summary>
    public sealed class SpectatorCycleSmokeProbe : MonoBehaviour
    {
        private const ulong VictimId = 1;
        private NetworkManager network;

        public void Initialize(NetworkManager manager)
        {
            network = manager;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (network.IsServer)
            {
                yield return new WaitForSeconds(1.5f);
                foreach (var player in network.SpawnManager.SpawnedObjectsList)
                {
                    if (player.IsPlayerObject && player.OwnerClientId == VictimId)
                    {
                        if (!FindFirstObjectByType<MatchPlayerStateManager>().Eliminate(player))
                            Fail("Eliminate was rejected.");
                        break;
                    }
                }
            }
            if (network.LocalClientId != VictimId)
                yield break;

            var mode = FindFirstObjectByType<SpectatorMode>();
            var follow = FindFirstObjectByType<LocalPlayerCameraFollow>();
            if (mode == null || follow == null)
            {
                Fail("Camera is missing SpectatorMode/LocalPlayerCameraFollow.");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 15f;
            while (!(mode.IsSpectating && mode.WatchedClientId == 0))
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Fail("Spectating did not start on the lowest survivor; watched=" + mode.WatchedClientId);
                    yield break;
                }
                yield return null;
            }

            var mouse = InputSystem.AddDevice<Mouse>();
            var steps = new (MouseButton button, ulong expected)[]
            {
                (MouseButton.Right, 2), (MouseButton.Right, 3), (MouseButton.Right, 0), (MouseButton.Left, 3),
            };
            foreach (var (button, expected) in steps)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(button));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                yield return null;
                if (mode.WatchedClientId != expected)
                {
                    Fail($"{button} click expected target {expected} but watching {mode.WatchedClientId}.");
                    yield break;
                }
            }

            yield return new WaitForSeconds(2f);
            var camera = follow.GetComponent<Camera>().transform;
            foreach (var player in network.SpawnManager.SpawnedObjectsList)
            {
                if (!player.IsPlayerObject || player.OwnerClientId != mode.WatchedClientId)
                    continue;
                var focus = camera.position + camera.forward * follow.FollowDistance;
                float error = (focus - player.transform.position).magnitude;
                if (error > 1.5f)
                {
                    Fail($"Camera focus is {error:F2} away from the watched player.");
                    yield break;
                }
                Debug.Log($"MUSHROOM_SPECTATOR_CYCLE_PASSED order=1->0,2,3,0,3 focusError={error:F2}");
                Application.Quit(0);
                yield break;
            }
            Fail("Watched player object not found.");
        }

        private static void Fail(string message)
        {
            Debug.LogError("MUSHROOM_SMOKE_FAILED " + message);
            Application.Quit(1);
        }
    }
}
#endif
