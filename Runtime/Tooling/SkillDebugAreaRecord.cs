#if UNITY_EDITOR
using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 테스트/디버그용 데미지 영역 표시 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillDebugAreaRecord
    {
        public int CasterInstanceId;
        public float ExpireTime;
        public ConfigCommonSkill.SkillAreaShape Shape;
        public Matrix4x4 Matrix;
        public Vector2 Size;
        public Vector2 Offset;
        public float Radius;
        public CapsuleDirection2D CapsuleDirection;
        public Vector2[] PolygonPoints;

        public static SkillDebugAreaRecord Create(
            Vector3 center,
            Vector3 forward,
            in SkillAreaSpec area,
            float durationSeconds,
            GameObject caster)
        {
            var record = new SkillDebugAreaRecord
            {
                CasterInstanceId = caster != null ? caster.GetInstanceID() : 0,
                ExpireTime = Time.time + Mathf.Max(0.01f, durationSeconds),
                Shape = area.shape,
                PolygonPoints = Array.Empty<Vector2>(),
            };

            var f2 = Flatten2D(forward);
            var fwd3 = new Vector3(f2.x, f2.y, 0f);
            var worldCenter = center + fwd3 * area.localOffset.x + Vector3.up * area.localOffset.y;
            float z = Mathf.Atan2(f2.y, f2.x) * Mathf.Rad2Deg - 90f;
            var rotation = Quaternion.identity;

            switch (area.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Circle:
                    record.Matrix = Matrix4x4.TRS(worldCenter, Quaternion.identity, Vector3.one);
                    record.Radius = Mathf.Max(0f, area.radius);
                    record.Offset = Vector2.zero;
                    break;

                case ConfigCommonSkill.SkillAreaShape.Box:
                    rotation = Quaternion.identity;
                    record.Matrix = Matrix4x4.TRS(worldCenter, rotation, Vector3.one);
                    record.Size = new Vector2(Mathf.Max(0f, area.width), Mathf.Max(0f, area.length));
                    record.Offset = new Vector2(0f, area.length * 0.5f);
                    break;

                case ConfigCommonSkill.SkillAreaShape.Line:
                    rotation = Quaternion.Euler(0f, 0f, z);
                    record.Matrix = Matrix4x4.TRS(worldCenter, rotation, Vector3.one);
                    record.Size = new Vector2(Mathf.Max(0f, area.width), Mathf.Max(0f, area.length));
                    record.Offset = new Vector2(0f, area.length * 0.5f);
                    break;

                case ConfigCommonSkill.SkillAreaShape.Capsule:
                    rotation = Quaternion.Euler(0f, 0f, z);
                    record.Matrix = Matrix4x4.TRS(worldCenter, rotation, Vector3.one);
                    record.CapsuleDirection = area.capsuleDirection;
                    record.Size = new Vector2(Mathf.Max(0f, area.radius), Mathf.Max(0f, area.length));
                    record.Offset = new Vector2(0f, area.length * 0.5f);
                    break;

                case ConfigCommonSkill.SkillAreaShape.Cone:
                    rotation = Quaternion.Euler(0f, 0f, z);
                    record.Matrix = Matrix4x4.TRS(worldCenter, rotation, Vector3.one);
                    float len = Mathf.Max(0.0001f, area.length);
                    float half = Mathf.Max(0f, area.angle) * 0.5f * Mathf.Deg2Rad;
                    record.PolygonPoints = new[]
                    {
                        Vector2.zero,
                        new Vector2(-Mathf.Sin(half) * len, Mathf.Cos(half) * len),
                        new Vector2(Mathf.Sin(half) * len, Mathf.Cos(half) * len),
                    };
                    break;

                default:
                    record.Matrix = Matrix4x4.TRS(worldCenter, Quaternion.identity, Vector3.one);
                    record.Radius = Mathf.Max(0f, area.radius);
                    record.Offset = Vector2.zero;
                    break;
            }

            return record;
        }

        private static Vector2 Flatten2D(Vector3 v)
        {
            var result = new Vector2(v.x, v.y);
            return result.sqrMagnitude < 1e-6f ? Vector2.up : result.normalized;
        }
    }
}
#endif
