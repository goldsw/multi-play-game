#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    /// <summary>
    /// 4 peers, 4 placed boxes. Every peer: boxes open on the host timer and show the same state/result (logged for
    /// cross-peer comparison). Host-timed script after Ready: players 0 and 1 touch boxes 1 and 2 and get an item
    /// (box closes, 10 s timer restarts); player 0 (holding) touches box 3 and gets nothing; player 2 touches box 3 and
    /// gets the item; box 4 stays Ready because nobody touches it.
    /// </summary>
    public sealed class PlacedBoxSmokeProbe : MonoBehaviour
    {
        private const double ClosedSeconds = 10d;
        private NetworkManager network;
        private ItemBox[] boxes = Array.Empty<ItemBox>();
        private bool failed;

        public void Initialize(NetworkManager manager)
        {
            network = manager;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float until = Time.realtimeSinceStartup + 10f;
            while (!FindBoxes())
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Fail($"expected 4 spawned ItemBoxes, found {boxes.Length}.");
                    yield break;
                }
                yield return null;
            }
            if (boxes.Any(box => box.State != ItemBoxState.Closed || box.Result != ItemKind.None))
            {
                Fail("boxes must start Closed with no result.");
                yield break;
            }

            until = Time.realtimeSinceStartup + 5f;
            while (boxes.Any(box => box.OpenAt <= 0d))
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Fail("box timers were never armed.");
                    yield break;
                }
                yield return null;
            }
            double armedIn = boxes.Max(box => box.OpenAt) - network.ServerTime.Time;
            if (armedIn is < 8d or > 10.2d)
            {
                Fail($"timer should be ~10 s from arming, got {armedIn:F2}.");
                yield break;
            }

            until = Time.realtimeSinceStartup + 25f;
            while (boxes.Any(box => box.State != ItemBoxState.Ready))
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Fail("boxes did not all reach Ready: " + Describe());
                    yield break;
                }
                yield return null;
            }
            Debug.Log($"MUSHROOM_PLACED_BOX_READY peer={network.LocalClientId} {Describe()}");
            if (boxes.Any(box => box.Result == ItemKind.None))
            {
                Fail("Ready box without result: " + Describe());
                yield break;
            }

            double ready = boxes.Max(box => box.OpenAt) + 4.3d;
            ulong me = network.LocalClientId;
            ulong[] ids = network.ConnectedClientsIds.OrderBy(id => id).ToArray();
            if (ids.Length != 4)
            {
                Fail("expected 4 peers, got " + ids.Length);
                yield break;
            }

            yield return WaitUntil(ready + 0.5d);
            if (me == ids[0]) Teleport(boxes[0], 0f);
            if (me == ids[1]) Teleport(boxes[1], 0f);
            yield return WaitUntil(ready + 2.5d);
            Verify("step A", new[] { ItemBoxState.Closed, ItemBoxState.Closed, ItemBoxState.Ready, ItemBoxState.Ready },
                new[] { true, true, false, false }, ids);
            if (failed) yield break;
            ItemKind heldBefore = network.IsServer ? HeldOf(ids[0]) : ItemKind.None;

            yield return WaitUntil(ready + 3d);
            if (me == ids[0]) Teleport(boxes[2], 0f); // a holder touches a Ready box
            yield return WaitUntil(ready + 5d);
            Verify("step B (holder gets nothing)",
                new[] { ItemBoxState.Closed, ItemBoxState.Closed, ItemBoxState.Ready, ItemBoxState.Ready },
                new[] { true, true, false, false }, ids);
            if (failed) yield break;
            if (network.IsServer && HeldOf(ids[0]) != heldBefore)
            {
                Fail("holder's item changed while touching a Ready box.");
                yield break;
            }

            yield return WaitUntil(ready + 5.5d);
            if (me == ids[2]) Teleport(boxes[2], 0.3f);
            yield return WaitUntil(ready + 7.5d);
            Verify("step C", new[] { ItemBoxState.Closed, ItemBoxState.Closed, ItemBoxState.Closed, ItemBoxState.Ready },
                new[] { true, true, true, false }, ids);
            if (failed) yield break;

            foreach (var box in boxes.Take(3))
            {
                double restartsIn = box.OpenAt - network.ServerTime.Time;
                if (restartsIn is < 1d or > ClosedSeconds + 0.5d)
                {
                    Fail($"{box.name}: timer should have restarted, openAt in {restartsIn:F2}s.");
                    yield break;
                }
            }
            Debug.Log($"MUSHROOM_PLACED_BOX_PASSED peer={me} {Describe()}");
            yield return new WaitForSeconds(2f); // keep the host up until clients verified too
            Application.Quit(0);
        }

        private bool FindBoxes()
        {
            boxes = FindObjectsByType<ItemBox>(FindObjectsSortMode.None)
                .OrderBy(box => box.name, StringComparer.Ordinal).ToArray();
            return boxes.Length >= 4 && boxes.All(box => box.IsSpawned);
        }

        private IEnumerator WaitUntil(double serverTime)
        {
            while (network.ServerTime.Time < serverTime)
                yield return null;
        }

        private ItemKind HeldOf(ulong clientId) =>
            network.ConnectedClients[clientId].PlayerObject.GetComponent<PlayerItemInventory>().Held;

        private void Teleport(ItemBox box, float offset)
        {
            var player = network.LocalClient.PlayerObject;
            var target = box.transform.position + new Vector3(0f, 0.6f, offset);
            player.GetComponent<Rigidbody>().position = target;
            player.GetComponent<OwnerNetworkTransform>()
                .Teleport(target, player.transform.rotation, player.transform.localScale);
        }

        private void Verify(string step, ItemBoxState[] expected, bool[] holders, ulong[] ids)
        {
            for (int index = 0; index < boxes.Length; index++)
            {
                if (boxes[index].State != expected[index])
                {
                    Fail($"{step}: {boxes[index].name} is {boxes[index].State}, expected {expected[index]} ({Describe()}).");
                    return;
                }
                if ((boxes[index].State == ItemBoxState.Closed) != (boxes[index].Result == ItemKind.None))
                {
                    Fail($"{step}: {boxes[index].name} result/state mismatch ({Describe()}).");
                    return;
                }
            }
            for (int index = 0; index < ids.Length; index++)
            {
                if (!network.IsServer && ids[index] != network.LocalClientId)
                    continue; // Held replicates to its owner only
                if ((HeldOf(ids[index]) != ItemKind.None) != holders[index])
                {
                    Fail($"{step}: player {ids[index]} Held={HeldOf(ids[index])}, expected holder={holders[index]}.");
                    return;
                }
            }
            Debug.Log($"MUSHROOM_PLACED_BOX_STEP peer={network.LocalClientId} {step} ok {Describe()}");
        }

        private string Describe() => string.Join(";", boxes.Select(box =>
            $"{box.name}:{box.State}/{box.Result}/{box.OpenAt:F1}"));

        private void Fail(string message)
        {
            failed = true;
            Debug.LogError("MUSHROOM_SMOKE_FAILED placed-box peer=" + network.LocalClientId + " " + message);
            Application.Quit(1);
        }
    }
}
#endif
