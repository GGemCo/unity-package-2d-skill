// using System;
// using System.Collections.Generic;
// using Config;
// using GGemCo2DSkill;
// using UnityEditor;
// using UnityEngine;
// using UnityEngine.Timeline;
//
// namespace GGemCo2DSkillEditor
// {
//     public static class SkillTimelineBakerV2
//     {
//         public static SkillRuntimeSequence Bake(
//             int skillUid,
//             TimelineAsset timeline,
//             string outputAssetPath,
//             out string report)
//         {
//             if (timeline == null) throw new ArgumentNullException(nameof(timeline));
//             if (string.IsNullOrWhiteSpace(outputAssetPath)) throw new ArgumentException("Invalid output path.");
//
//             var errors = new List<string>();
//             var events = new List<SkillRuntimeEvent>();
//
//             var payloadDamage = new List<DamagePayload>();
//             var payloadVfx = new List<SpawnVfxPayload>();
//             var payloadSfx = new List<PlaySfxPayload>();
//             var payloadAffect = new List<ApplyAffectPayload>();
//
//             foreach (var track in timeline.GetOutputTracks())
//             {
//                 if (track is not SkillEventTrack) continue;
//
//                 foreach (var clip in track.GetClips())
//                 {
//                     if (clip.asset is not SkillEventClipBase evClip)
//                     {
//                         errors.Add($"Invalid clip asset type. clip={clip.displayName}");
//                         continue;
//                     }
//
//                     var start = (float)clip.start;
//                     var end = (float)clip.end;
//                     int payloadIndex = -1;
//
//                     switch (evClip.EventType)
//                     {
//                         case ConfigCommonSkill.SkillEventType.Damage:
//                         {
//                             var c = (SkillDamageClip)evClip;
//                             payloadIndex = payloadDamage.Count;
//                             payloadDamage.Add(new DamagePayload
//                             {
//                                 Coef = c.Coef,
//                                 DamageTypeUid = c.DamageTypeUid,
//                                 AreaUid = c.AreaUid
//                             });
//                             break;
//                         }
//                         case ConfigCommonSkill.SkillEventType.SpawnEffect:
//                         {
//                             var c = (SkillSpawnEffectClip)evClip;
//                             payloadIndex = payloadVfx.Count;
//                             payloadVfx.Add(new SpawnVfxPayload
//                             {
//                                 Prefab = c.Prefab,
//                                 Anchor = c.Anchor,
//                                 Offset = c.Offset
//                             });
//                             break;
//                         }
//                         case ConfigCommonSkill.SkillEventType.PlayAudio:
//                         {
//                             var c = (SkillPlayAudioClip)evClip;
//                             payloadIndex = payloadSfx.Count;
//                             payloadSfx.Add(new PlaySfxPayload
//                             {
//                                 Clip = c.Clip,
//                                 Volume = c.Volume
//                             });
//                             break;
//                         }
//                         case ConfigCommonSkill.SkillEventType.ApplyAffect:
//                         {
//                             var c = (SkillApplyAffectClip)evClip;
//                             payloadIndex = payloadAffect.Count;
//                             payloadAffect.Add(new ApplyAffectPayload
//                             {
//                                 AffectUid = c.AffectUid,
//                                 Duration = c.Duration
//                             });
//                             break;
//                         }
//                         default:
//                             errors.Add($"Unsupported event type: {evClip.EventType} (clip={clip.displayName})");
//                             break;
//                     }
//
//                     if (payloadIndex < 0) continue;
//
//                     events.Add(new SkillRuntimeEvent
//                     {
//                         Type = evClip.EventType,
//                         StartTime = start,
//                         EndTime = end,
//                         Order = evClip.Order,
//                         PayloadIndex = payloadIndex
//                     });
//                 }
//             }
//
//             // 런타임 Tick 최적화를 위한 정렬
//             events.Sort((a, b) =>
//             {
//                 int t = a.StartTime.CompareTo(b.StartTime);
//                 if (t != 0) return t;
//                 return a.Order.CompareTo(b.Order);
//             });
//
//             var seq = AssetDatabase.LoadAssetAtPath<SkillRuntimeSequence>(outputAssetPath);
//             if (seq == null)
//             {
//                 seq = ScriptableObject.CreateInstance<SkillRuntimeSequence>();
//                 AssetDatabase.CreateAsset(seq, outputAssetPath);
//             }
//             seq.EditorSetData(skillUid, (float)timeline.duration, events.ToArray(), new SkillBakedPayloads
//             {
//                 Damage = payloadDamage.ToArray(),
//                 SpawnVfx = payloadVfx.ToArray(),
//                 PlaySfx = payloadSfx.ToArray(),
//                 ApplyAffect = payloadAffect.ToArray()
//             } );
//
//             EditorUtility.SetDirty(seq);
//             AssetDatabase.SaveAssets();
//
//             report = errors.Count == 0 ? "Bake OK" : string.Join("\n", errors);
//             return seq;
//         }
//     }
// }
