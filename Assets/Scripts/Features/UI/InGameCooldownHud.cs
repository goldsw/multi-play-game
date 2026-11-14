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
            canvas = CooldownHudLayout.Create(transform, out var skill, out var item, out itemView);
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
            {
                bool active = caster.ActiveEndsAt > now && currentSkill.CooldownAfterEffect;
                float gauge = 0f;
                if (active)
                    gauge = (float)((caster.ActiveEndsAt - now) / currentSkill.Effect.DurationSeconds);
                else
                {
                    for (int charge = 0; charge < currentSkill.Charges; charge++)
                    {
                        double remaining = caster.GetRechargeEnd(charge) - now;
                        gauge += currentSkill.CooldownSeconds > 0f
                            ? 1f - Mathf.Clamp01((float)(remaining / currentSkill.CooldownSeconds)) : 1f;
                    }
                    gauge /= Mathf.Max(1, currentSkill.Charges);
                }
                SkillSlot.Timer.SetSkillGauge(gauge, active);
            }

            ItemSlot.Timer.Render(now);
            itemView.Render();
        }
    }
}
