using System;
using System.Collections.Generic;
using System.IO;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// TimelineAsset -> SkillRuntimeSequence Bake 유틸리티.
    /// - 이벤트 클립의 start 시간을 이벤트 시간으로 사용한다.
    /// </summary>
    public static class SkillTimelineBakerV2
    {
        public static SkillRuntimeSequence BakeOrUpdate(int skillUid, TimelineAsset timeline, string assetPath)
        {
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            if (skillUid <= 0) throw new ArgumentOutOfRangeException(nameof(skillUid));
            if (string.IsNullOrEmpty(assetPath)) throw new ArgumentNullException(nameof(assetPath));

            var (events, payloads, duration) = Extract(timeline);

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? "Assets");

            var seq = AssetDatabase.LoadAssetAtPath<SkillRuntimeSequence>(assetPath);
            if (seq == null)
            {
                seq = ScriptableObject.CreateInstance<SkillRuntimeSequence>();
                AssetDatabase.CreateAsset(seq, assetPath);
            }

            seq.EditorSetData(skillUid, duration, events, payloads);
            EditorUtility.SetDirty(seq);
            AssetDatabase.SaveAssets();
            return seq;
        }

        private static (SkillRuntimeEvent[] events, UnityEngine.Object[] payloads, float duration) Extract(TimelineAsset timeline)
        {
            var tmpEvents = new List<SkillRuntimeEvent>(64);
            var tmpPayloads = new List<UnityEngine.Object>(64);

            float duration = (float)timeline.duration;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track == null) continue;
                if (track is not SkillEventTrackV2) continue;

                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is not SkillEventClipV2 evClip) continue;

                    int payloadIndex = -1;
                    if (evClip.Payload != null)
                    {
                        payloadIndex = tmpPayloads.Count;
                        tmpPayloads.Add(evClip.Payload);
                    }

                    tmpEvents.Add(new SkillRuntimeEvent
                    {
                        Type = evClip.EventType,
                        StartTime = (float)clip.start,
                        Order = 0,
                        PayloadIndex = payloadIndex
                    });
                }
            }

            // 정렬 안정성: time -> order
            tmpEvents.Sort((a, b) =>
            {
                int t = a.StartTime.CompareTo(b.StartTime);
                if (t != 0) return t;
                return a.Order.CompareTo(b.Order);
            });

            return (tmpEvents.ToArray(), tmpPayloads.ToArray(), duration);
        }
    }
}
