using UnityEngine;
using UnityEngine.Rendering;

namespace MushroomSurvival
{
    internal sealed class SkillRangeView
    {
        private const int CircleSegments = 64;
        private const float LineWidth = 0.1f;

        private readonly Transform parent;
        private readonly Material material;
        private readonly Vector3[] circlePositions = new Vector3[CircleSegments];
        private LineRenderer line;

        public SkillRangeView(Transform parent, Material material)
        {
            this.parent = parent;
            this.material = material;
        }

        public void ShowCircle(Vector3 center, float radius)
        {
            if (radius <= 0f || material == null)
            {
                Hide();
                return;
            }

            var renderer = GetRenderer();
            renderer.loop = true;
            renderer.positionCount = CircleSegments;
            for (int index = 0; index < CircleSegments; index++)
            {
                float angle = index * (2f * Mathf.PI / CircleSegments);
                circlePositions[index] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }

            renderer.SetPositions(circlePositions);
            renderer.enabled = true;
        }

        public void ShowLine(Vector3 start, Vector3 end)
        {
            if ((start - end).sqrMagnitude < 0.000001f || material == null)
            {
                Hide();
                return;
            }

            var renderer = GetRenderer();
            renderer.loop = false;
            renderer.positionCount = 2;
            renderer.SetPosition(0, start);
            renderer.SetPosition(1, end);
            renderer.enabled = true;
        }

        public void Hide()
        {
            if (line != null)
                line.enabled = false;
        }

        private LineRenderer GetRenderer()
        {
            if (line != null)
                return line;

            var indicator = new GameObject("Skill Range Indicator");
            indicator.transform.SetParent(parent, false);
            line = indicator.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
