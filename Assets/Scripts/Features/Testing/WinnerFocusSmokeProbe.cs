#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    /// <summary>
    /// Every peer: after the host ends the match, the camera must follow the announced winner with the winner banner
    /// shown, spectator input locked, and the Result scene must only load after the host-time celebration ends.
    /// Optional --smoke-winner-shot &lt;dir&gt; saves a game-window screenshot (rendering mode only) mid-celebration.
    /// </summary>
    public sealed class WinnerFocusSmokeProbe : MonoBehaviour
    {
        private NetworkManager network;

        public void Initialize(NetworkManager manager)
        {
            network = manager;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var state = FindFirstObjectByType<SessionNetworkState>();
            var focus = FindFirstObjectByType<WinnerFocus>();
            var follow = FindFirstObjectByType<LocalPlayerCameraFollow>();
            var spectator = FindFirstObjectByType<SpectatorMode>();
            if (state == null || focus == null || follow == null || spectator == null)
            {
                Fail("InGame camera is missing WinnerFocus/LocalPlayerCameraFollow/SpectatorMode.");
                yield break;
            }

            float until = Time.realtimeSinceStartup + 15f;
            while (!state.Winner.IsActive)
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Fail("No winner announcement arrived.");
                    yield break;
                }
                yield return null;
            }

            var winner = state.Winner;
            double announcedAt = network.ServerTime.Time;
            yield return new WaitForSeconds(2f);

            if (!focus.IsFocused || focus.FocusedClientId != winner.ClientId || !focus.BannerVisible ||
                !spectator.IsCelebrating)
            {
                Fail($"Peer {network.LocalClientId}: celebration state wrong focused={focus.IsFocused} " +
                     $"id={focus.FocusedClientId} banner={focus.BannerVisible} celebrating={spectator.IsCelebrating}.");
                yield break;
            }
            var target = follow.ActiveTarget;
            var targetObject = target != null ? target.GetComponentInParent<NetworkObject>() : null;
            if (targetObject == null || targetObject.OwnerClientId != winner.ClientId)
            {
                Fail($"Peer {network.LocalClientId}: camera target is not the winner {winner.ClientId}.");
                yield break;
            }
            float depth = Vector3.Dot(target.position - follow.transform.position, follow.transform.forward);
            if (follow.FollowDistance > 12f || depth > 12f)
            {
                Fail($"Peer {network.LocalClientId}: camera did not zoom in (distance={follow.FollowDistance}, depth={depth}).");
                yield break;
            }

            var camera = follow.GetComponent<Camera>();
            if (camera.orthographic && camera.orthographicSize > 6f)
            {
                Fail($"Peer {network.LocalClientId}: orthographic size {camera.orthographicSize:F1} did not shrink.");
                yield break;
            }

            var arguments = Environment.GetCommandLineArgs();
            int shot = Array.IndexOf(arguments, "--smoke-winner-shot");
            if (shot >= 0 && shot + 1 < arguments.Length)
            {
                Directory.CreateDirectory(arguments[shot + 1]);
                string path = Path.Combine(arguments[shot + 1], $"winner_peer{network.LocalClientId}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                yield return null;
                Debug.Log("MUSHROOM_WINNER_SHOT " + path);
            }

            float resultUntil = Time.realtimeSinceStartup + 15f;
            while (!SceneManager.GetSceneByName(ResultSceneLoader.SceneName).isLoaded)
            {
                if (Time.realtimeSinceStartup > resultUntil)
                {
                    Fail("Result scene did not load after the celebration.");
                    yield break;
                }
                yield return null;
            }
            double loadedAt = network.ServerTime.Time;
            if (loadedAt < winner.ResultAt - 0.3d)
            {
                Fail($"Peer {network.LocalClientId}: Result loaded {winner.ResultAt - loadedAt:F2}s before ResultAt.");
                yield break;
            }
            Debug.Log($"MUSHROOM_WINNER_FOCUS_PASSED peer={network.LocalClientId}; winner={winner.ClientId}; " +
                      $"distance={follow.FollowDistance:F1}; ortho={follow.GetComponent<Camera>().orthographicSize:F1}; celebration={loadedAt - announcedAt:F2}s; " +
                      $"resultAt-loaded={winner.ResultAt - loadedAt:F2}s");
        }

        private static void Fail(string message)
        {
            Debug.LogError("MUSHROOM_SMOKE_FAILED " + message);
            Application.Quit(1);
        }
    }
}
#endif
