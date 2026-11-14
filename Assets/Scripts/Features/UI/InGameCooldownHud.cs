using System;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class InGameCooldownHud : MonoBehaviour
    {
        private NetworkManager network;
        private LobbyNetworkState lobby;
        private GameObject canvas;
        private SkillCaster caster;
        private NetworkObject boundPlayer;

        public CooldownSlot SkillSlot { get; private set; }
        public CooldownSlot ItemSlot { get; private set; }

        public void Configure(NetworkManager manager, LobbyNetworkState state)
        {
            network = manager;
            lobby = state;
            canvas = CooldownHudLayout.Create(transform, () => network.ServerTime.Time, out var skill, out var item);
            SkillSlot = skill;
            ItemSlot = item;
            canvas.SetActive(false);
        }

        private void Update()
        {
            var player = network.LocalClient?.PlayerObject;
            if (lobby.Status != SessionStatus.Playing || player == null || !player.IsSpawned)
            {
                if (canvas.activeSelf)
                {
                    SkillSlot.Unequip();
                    ItemSlot.Unequip();
                    caster = null;
                    boundPlayer = null;
                    canvas.SetActive(false);
                }
                return;
            }

            if (boundPlayer != player)
            {
                boundPlayer = player;
                caster = player.GetComponent<SkillCaster>();
                SkillSlot.Init(caster?.Skill?.SkillIcon);
                canvas.SetActive(true);
            }

            double now = network.ServerTime.Time;
            if (caster != null && caster.Skill != null && SkillSlot.IsEquipped)
            {
                double next = double.PositiveInfinity;
                for (int charge = 0; charge < caster.Skill.Charges; charge++)
                {
                    double end = caster.GetRechargeEnd(charge);
                    if (end > now)
                        next = Math.Min(next, end);
                }
                SkillSlot.Timer.SetCooldown(double.IsPositiveInfinity(next) ? 0d : next, caster.Skill.CooldownSeconds);
            }
        }
    }
}
