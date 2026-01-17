// Assets/GGemCo/Skills/Editor/Authoring/SkillTimelineBaker.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    public static class SkillTimelineBaker
    {
        public static void Bake(SkillAuthoringAsset authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (authoring.targetSkill == null)
                throw new InvalidOperationException("Target SkillDefinition is null.");

            if (authoring.timelineAsset == null)
                throw new InvalidOperationException("Timeline Asset is null.");

            if (authoring.timelineAsset is not TimelineAsset timeline)
                throw new InvalidOperationException("PlayableAsset is not a TimelineAsset.");

            var seq = new SkillEventSequence
            {
                totalDuration = (float)timeline.duration,
                keyframes = new List<SkillEventKeyframe>(64)
            };

            // 모든 Track을 돌면서 Marker(SignalEmitter)를 수집
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track == null) continue;

                // MarkerTrack(시그널 포함)에서 Markers 수집
                foreach (var marker in track.GetMarkers())
                {
                    if (marker is not SignalEmitter emitter) continue;
                    if (emitter.asset == null) continue;

                    if (emitter.asset is SkillSignalAsset skillSignal)
                    {
                        seq.keyframes.Add(new SkillEventKeyframe
                        {
                            time = (float)emitter.time,
                            type = skillSignal.eventType,
                            payload = skillSignal.payload,
                            order = skillSignal.order
                        });
                    }
                    else
                    {
                        // 다른 SignalAsset이 들어왔을 때는 무시하거나 경고
                        Debug.LogWarning($"[SkillBaker] Unsupported SignalAsset: {emitter.asset.GetType().Name}");
                    }
                }
            }

            // 정렬(시간 → order)
            seq.keyframes.Sort(static (a, b) =>
            {
                int t = a.time.CompareTo(b.time);
                if (t != 0) return t;
                return a.order.CompareTo(b.order);
            });

            // 결과 저장
            authoring.targetSkill.eventSequence = seq;

            EditorUtility.SetDirty(authoring.targetSkill);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SkillBaker] Baked: skill={authoring.targetSkill.skillId}, keyframes={seq.keyframes.Count}, duration={seq.totalDuration:0.###}s");
        }
    }
}
#endif
