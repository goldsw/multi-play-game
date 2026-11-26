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
        private SkillDefinition boundSkill;
        private CharacterStateMachine character;
        private ItemSlotView itemView;

        public CooldownSlot SkillSlot { get; private set; }
        public CooldownSlot ItemSlot { get; private set; }

        public void Configure(NetworkManager manager, LobbyNetworkState state)
        {
            network = manager;
            lobby = state;
            canvas = CooldownHudLayout.Create(transform, RequestSkill, RequestItem, out var skill, out var item, out itemView);
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
                    itemView.Bind(null);
                    caster = null;
                    boundPlayer = null;
                    boundSkill = null;
                    character = null;
                    canvas.SetActive(false);
                }
                return;
            }

            if (boundPlayer != player)
            {
                boundPlayer = player;
                caster = player.GetComponent<SkillCaster>();
                character = player.GetComponent<CharacterStateMachine>();
                itemView.Bind(player.GetComponent<PlayerItemInventory>());
                canvas.SetActive(true);
            }
            SkillSlot.SetLocked(character?.AllowsPlayerActions == false);
            ItemSlot.SetLocked(character?.AllowsItemUse == false);
            var currentSkill = caster?.Skill;
            if (currentSkill != boundSkill)
            {
                boundSkill = currentSkill;
                SkillSlot.Init(currentSkill?.SkillIcon);
            }

            double now = network.ServerTime.Time;
            if (currentSkill != null)
                RenderSkillGauge(currentSkill, now);

            ItemSlot.Timer.Render(now);
            itemView.Render();
        }

        private void RequestSkill()
        {
            if (boundSkill != null && !SkillSlot.IsLocked)
                caster.RequestCast();
        }

        private void RequestItem()
        {
            if (!ItemSlot.IsLocked)
                itemView.RequestUse();
        }

        private void RenderSkillGauge(SkillDefinition skill, double now)
        {
            bool active = skill.CooldownAfterEffect && caster.ActiveEndsAt > now;
            if (active)
            {
                float remaining = (float)((caster.ActiveEndsAt - now) / skill.Effect.DurationSeconds);
                SkillSlot.Timer.SetSkillGauge(remaining, true);
                return;
            }

            if (skill.Charges > 1)
            {
                SkillSlot.Timer.SetChargeGauge(caster.GetReadyChargeCount(now), skill.Charges);
                return;
            }

            double rechargeRemaining = caster.GetRechargeEnd(0) - now;
            float progress = skill.CooldownSeconds > 0f
                ? 1f - Mathf.Clamp01((float)(rechargeRemaining / skill.CooldownSeconds))
                : 1f;
            SkillSlot.Timer.SetSkillGauge(progress, false);
        }
    }
}
