using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DetectiveGame.Tutorial
{
    [Serializable]
    public sealed class WakeUpCameraKey
    {
        [Range(0f, 1f)] public float time;
        [Tooltip("World-space offset from the Lie/Sit/Stand anchor selected below.")]
        public Vector3 offset;
        public Vector3 rotation;
        [Range(0, 2)] public int anchor;
    }

    // Timeline owns timing and motion data; the controller owns locking and restoration.
    public sealed class TutorialWakeUpMotionBehaviour : PlayableBehaviour
    {
        public TutorialWakeUpMotionAsset data;
    }

    public sealed class TutorialWakeUpMotionMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var controller = playerData as TutorialWakeUpController;
            if (controller == null) return;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                var input = (ScriptPlayable<TutorialWakeUpMotionBehaviour>)playable.GetInput(i);
                double duration = input.GetDuration();
                float time = duration > 0 ? (float)(input.GetTime() / duration) : 0f;
                controller.SampleMotion(input.GetBehaviour().data, Mathf.Clamp01(time));
                return;
            }
        }
    }

    [TrackColor(0.2f, 0.65f, 0.85f)]
    [TrackClipType(typeof(TutorialWakeUpMotionAsset))]
    [TrackBindingType(typeof(TutorialWakeUpController))]
    public sealed class TutorialWakeUpMotionTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<TutorialWakeUpMotionMixer>.Create(graph);
            mixer.SetInputCount(inputCount);
            return mixer;
        }
    }
}

