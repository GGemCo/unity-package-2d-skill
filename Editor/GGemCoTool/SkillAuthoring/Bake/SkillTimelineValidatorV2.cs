using System.Collections.Generic;
using Config;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    public static class SkillTimelineValidatorV2
    {
        public static List<string> Validate(TimelineAsset timeline)
        {
            var errors = new List<string>();
            if (timeline == null)
            {
                errors.Add("TimelineAsset is null.");
                return errors;
            }

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is not SkillEventTrack) continue;

                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is not SkillEventClipBase ev)
                    {
                        errors.Add($"Invalid clip asset. clip={clip.displayName}");
                        continue;
                    }

                    if (clip.start < 0) errors.Add($"Clip start < 0. clip={clip.displayName}");
                    if (clip.end < clip.start) errors.Add($"Clip end < start. clip={clip.displayName}");

                    switch (ev.EventType)
                    {
                        case ConfigCommonSkill.SkillEventType.Damage:
                        {
                            var c = (SkillDamageClip)ev;
                            if (c.AreaUid == 0) errors.Add($"DamageClip: AreaUid is 0. clip={clip.displayName}");
                            break;
                        }
                        case ConfigCommonSkill.SkillEventType.SpawnEffect:
                        {
                            var c = (SkillSpawnEffectClip)ev;
                            if (c.Prefab == null) errors.Add($"EffectClip: Prefab is null. clip={clip.displayName}");
                            break;
                        }
                        case ConfigCommonSkill.SkillEventType.PlayAudio:
                        {
                            var c = (SkillPlayAudioClip)ev;
                            if (c.Clip == null) errors.Add($"AudioClip: AudioClip is null. clip={clip.displayName}");
                            break;
                        }
                        case ConfigCommonSkill.SkillEventType.ApplyAffect:
                        {
                            var c = (SkillApplyAffectClip)ev;
                            if (c.AffectUid == 0) errors.Add($"AffectClip: AffectUid is 0. clip={clip.displayName}");
                            break;
                        }
                    }
                }
            }

            return errors;
        }
    }
}
