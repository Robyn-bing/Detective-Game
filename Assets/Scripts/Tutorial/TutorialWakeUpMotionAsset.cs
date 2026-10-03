using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DetectiveGame.Tutorial
{
    [Serializable]
    public sealed class TutorialWakeUpMotionAsset : PlayableAsset, ITimelineClipAsset
    {
        public AnimationCurve lieToSit = AnimationCurve.EaseInOut(1.8f / 5.6f, 0f, 0.5f, 1f);
        public AnimationCurve sitToStand = AnimationCurve.EaseInOut(0.625f, 0f, 0.938f, 1f);
        public WakeUpCameraKey[] cameraKeys;
        public ClipCaps clipCaps => ClipCaps.SpeedMultiplier;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<TutorialWakeUpMotionBehaviour>.Create(graph);
            p.GetBehaviour().data = this;
            return p;
        }
    }

}

