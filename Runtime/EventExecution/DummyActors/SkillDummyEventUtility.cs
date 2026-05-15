using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 캐릭터 이벤트에서 공통으로 사용하는 키 정규화, 위치 해석, 소스 타입 변환 로직을 제공합니다.
    /// </summary>
    internal static class SkillDummyEventUtility
    {
        /// <summary>
        /// 스킬 더미 소스 타입을 코어 캐릭터 타입으로 변환합니다.
        /// </summary>
        /// <param name="sourceType">스킬 이벤트에서 지정한 더미 소스 타입입니다.</param>
        /// <returns>CharacterManager에서 사용하는 코어 캐릭터 타입입니다.</returns>
        public static CharacterConstants.Type ResolveSourceCharacterType(DummyCharacterSourceType sourceType)
        {
            return sourceType == DummyCharacterSourceType.Npc
                ? CharacterConstants.Type.Npc
                : CharacterConstants.Type.Monster;
        }

        /// <summary>
        /// 더미 생성 기준점을 해석하여 최종 생성 위치를 계산합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="casterPos">해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        /// <param name="spawnPos">계산된 생성 위치입니다.</param>
        /// <returns>생성 위치 계산에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveSpawnPosition(
            SkillRun run,
            SpawnDummyCharacterEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            out Vector3 spawnPos)
        {
            spawnPos = casterPos;
            if (def == null)
                return false;

            switch (def.spawnAnchor)
            {
                case DummySpawnAnchor.Target:
                    spawnPos = targetPos;
                    return true;
                case DummySpawnAnchor.Ground:
                    spawnPos = groundPoint;
                    return true;
                case DummySpawnAnchor.NamedPositionAnchor:
                    if (TryResolveNamedAnchorPosition(run, def.namedAnchorKey, out spawnPos))
                        return true;

                    Debug.LogWarning($"[SkillExecutor] SpawnDummyCharacter named anchor not found. key={def.namedAnchorKey}");
                    return false;
                case DummySpawnAnchor.Caster:
                default:
                    spawnPos = casterPos;
                    return true;
            }
        }

        /// <summary>
        /// 더미 이동 목표 위치를 해석합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <param name="actorPos">이동 대상 더미의 현재 월드 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        /// <param name="moveTarget">해석된 이동 목표 위치입니다.</param>
        /// <returns>이동 목표 해석에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveMoveTargetPosition(
            SkillRun run,
            MoveDummyCharacterEventDefinition def,
            Vector3 actorPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            out Vector3 moveTarget)
        {
            moveTarget = targetPos;
            if (def == null)
                return false;

            switch (def.moveTargetMode)
            {
                case DummyMoveTargetMode.GroundPoint:
                    moveTarget = groundPoint;
                    return true;
                case DummyMoveTargetMode.LockedTarget:
                    moveTarget = targetPos;
                    return true;
                case DummyMoveTargetMode.LockedTargetFront:
                    moveTarget = targetPos + ResolveSignedTargetFrontOffset(actorPos, targetPos, def.targetFrontDistance);
                    return true;
                case DummyMoveTargetMode.AbsoluteWorld:
                    moveTarget = def.absoluteWorldPosition;
                    return true;
                case DummyMoveTargetMode.NamedPositionAnchor:
                    if (TryResolveNamedAnchorPosition(run, def.namedAnchorKey, out moveTarget))
                        return true;

                    Debug.LogWarning($"[SkillExecutor] MoveDummyCharacter named anchor not found. key={def.namedAnchorKey}");
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 더미 액터 키를 정규화합니다.
        /// </summary>
        /// <param name="actorKey">원본 액터 키입니다.</param>
        /// <returns>앞뒤 공백을 제거한 키이며, 비어 있으면 빈 문자열입니다.</returns>
        public static string NormalizeActorKey(string actorKey)
        {
            return string.IsNullOrWhiteSpace(actorKey) ? string.Empty : actorKey.Trim();
        }

        /// <summary>
        /// 더미 이벤트 로그에 표시할 대상 식별 문자열을 반환합니다.
        /// </summary>
        /// <param name="actorReferenceType">대상 참조 방식입니다.</param>
        /// <param name="actorKey">Actor 참조 시 원본 actorKey입니다.</param>
        /// <returns>로그 출력용 대상 식별 문자열입니다.</returns>
        public static string GetActorDisplayName(DummyActorReferenceType actorReferenceType, string actorKey)
        {
            return actorReferenceType == DummyActorReferenceType.Caster
                ? "Caster"
                : NormalizeActorKey(actorKey);
        }

        /// <summary>
        /// 타겟 중심에서 더미가 있던 좌/우 방향을 기준으로 부호 있는 이동 오프셋을 계산합니다.
        /// </summary>
        /// <param name="actorPos">더미의 현재 월드 위치입니다.</param>
        /// <param name="targetPos">타겟 중심 월드 위치입니다.</param>
        /// <param name="signedDistance">타겟 중심에서 떨어질 거리입니다. 양수는 더미가 있던 방향, 음수는 반대 방향입니다.</param>
        /// <returns>타겟 중심에 더할 월드 좌표 오프셋입니다.</returns>
        private static Vector3 ResolveSignedTargetFrontOffset(Vector3 actorPos, Vector3 targetPos, float signedDistance)
        {
            float sideSign = ResolveTargetFrontSideSign(actorPos, targetPos);
            return new Vector3(sideSign * signedDistance, 0f, 0f);
        }

        /// <summary>
        /// 타겟 중심 대비 더미가 서 있던 좌/우 방향 부호를 계산합니다.
        /// </summary>
        /// <param name="actorPos">더미의 현재 월드 위치입니다.</param>
        /// <param name="targetPos">타겟 중심 월드 위치입니다.</param>
        /// <returns>더미가 타겟의 오른쪽이면 1, 왼쪽이면 -1이며 겹치면 1을 반환합니다.</returns>
        private static float ResolveTargetFrontSideSign(Vector3 actorPos, Vector3 targetPos)
        {
            float deltaX = actorPos.x - targetPos.x;
            if (Mathf.Abs(deltaX) <= 1e-4f)
                return 1f;

            return Mathf.Sign(deltaX);
        }

        /// <summary>
        /// 같은 스킬 런에 저장된 이름 있는 위치 앵커를 조회합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="anchorKey">조회할 앵커 키입니다.</param>
        /// <param name="position">조회된 위치입니다.</param>
        /// <returns>앵커 조회에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveNamedAnchorPosition(SkillRun run, string anchorKey, out Vector3 position)
        {
            position = Vector3.zero;
            if (run == null || string.IsNullOrWhiteSpace(anchorKey))
                return false;

            if (!run.TryGetPositionAnchor(anchorKey, out SkillPositionAnchorSnapshot snapshot))
                return false;

            position = snapshot.Position;
            return true;
        }
    }
}
