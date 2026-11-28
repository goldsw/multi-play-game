using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MushroomSurvival
{
    // Plays a single clip on a local model without needing an Animator Controller state.
    // With no clip the model simply stays in whatever idle pose the Animator/model already has.
    public sealed class ResultMotionPlayer : MonoBehaviour
    {
        private PlayableGraph graph;

        public bool Play(AnimationClip clip)
        {
            Stop();
            if (clip == null)
                return false;

            var animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = gameObject.AddComponent<Animator>();
            AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
            return true;
        }

        public void Stop()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        private void OnDestroy() => Stop();
    }
}
