using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStateMachine))]
    public sealed class CharacterVisibility : MonoBehaviour
    {
        [SerializeField] private VisibilityEffectDefinition definition;
        private readonly List<(Renderer Renderer, Material[] Originals, Material[] Transparent)> materials = new();
        private CharacterStateMachine state;
        private float opacity = 1f;
        private float fadeFrom = 1f;
        private float fadeTo = 1f;
        private double fadeStartedAt;

        public float Opacity => opacity;

        public void Configure(VisibilityEffectDefinition effect) => definition = effect;

        private void Awake()
        {
            state = GetComponent<CharacterStateMachine>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer or SkinnedMeshRenderer)
                    materials.Add((renderer, renderer.sharedMaterials, null));
            }
        }

        private void Update()
        {
            if (definition == null)
                return;

            bool invisible = state.IsSpawned && (state.Effects & CharacterEffects.Invisible) != 0;
            float target = invisible ? (state.IsOwner ? definition.OwnerOpacity : 0f) : 1f;
            double now = Time.unscaledTimeAsDouble;
            if (target != fadeTo)
            {
                fadeFrom = opacity;
                fadeTo = target;
                fadeStartedAt = now;
            }
            float progress = Mathf.Clamp01((float)((now - fadeStartedAt) / Mathf.Max(0.01f, definition.FadeSeconds)));
            float next = Mathf.Lerp(fadeFrom, fadeTo, progress);
            if (next == opacity)
                return;

            opacity = next;
            ApplyOpacity();
        }

        private void ApplyOpacity()
        {
            for (int index = 0; index < materials.Count; index++)
            {
                var (renderer, originals, transparent) = materials[index];
                renderer.forceRenderingOff = opacity <= 0f;
                if (opacity >= 1f)
                {
                    renderer.sharedMaterials = originals;
                    continue;
                }

                if (transparent == null)
                {
                    transparent = new Material[originals.Length];
                    for (int slot = 0; slot < originals.Length; slot++)
                        transparent[slot] = CreateTransparent(originals[slot]);
                    materials[index] = (renderer, originals, transparent);
                }

                for (int slot = 0; slot < transparent.Length; slot++)
                {
                    if (transparent[slot] == null || !transparent[slot].HasProperty("_BaseColor"))
                        continue;

                    var color = originals[slot].GetColor("_BaseColor");
                    color.a *= opacity;
                    transparent[slot].SetColor("_BaseColor", color);
                }
                renderer.sharedMaterials = transparent;
            }
        }

        private static Material CreateTransparent(Material original)
        {
            if (original == null || !original.HasProperty("_BaseColor"))
                return original;

            var material = new Material(original);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            return material;
        }

        private void OnDisable()
        {
            opacity = fadeFrom = fadeTo = 1f;
            ApplyOpacity();
        }

        private void OnDestroy()
        {
            foreach (var entry in materials)
            {
                if (entry.Transparent == null)
                    continue;
                for (int index = 0; index < entry.Transparent.Length; index++)
                {
                    if (entry.Transparent[index] != entry.Originals[index])
                        Destroy(entry.Transparent[index]);
                }
            }
        }
    }
}
