using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 액터 생성 요청을 검증하고, 실제 캐릭터 생성과 초기 런타임 핸들 구성을 담당합니다.
    /// </summary>
    internal static class SkillDummyActorSpawnUtility
    {
        /// <summary>
        /// 스킬 이벤트 정의를 기반으로 더미 캐릭터를 생성하고 레지스트리에 등록합니다.
        /// </summary>
        /// <param name="runner">페이드 코루틴과 기존 더미 정리에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="snapshotCasterPos">스킬 시작 시점 캐스터 위치 스냅샷입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        /// <param name="handle">생성된 더미 액터 핸들입니다.</param>
        /// <returns>더미 생성과 레지스트리 등록에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TrySpawn(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            SpawnDummyCharacterEventDefinition def,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            out SkillDummyActorHandle handle)
        {
            handle = null;
            if (runner == null || registry == null || def == null)
                return false;

            var sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.CharacterManager == null)
                return false;

            if (!TryValidateDefinition(def, out string actorKey))
                return false;

            if (!TryReserveActorKey(runner, registry, actorKey, def.replaceIfExists))
                return false;

            ResolveSpawnReferencePositions(
                ctx,
                def.useSnapshotCenter,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                out Vector3 casterPos,
                out Vector3 targetPos,
                out Vector3 groundPoint);

            if (!SkillDummyEventUtility.TryResolveSpawnPosition(run, def, casterPos, targetPos, groundPoint, out Vector3 spawnPos))
                return false;

            spawnPos += def.localOffset;

            if (!TryCreateCharacter(sceneGame, def, actorKey, spawnPos, out CharacterBase character))
                return false;

            handle = CreateHandle(actorKey, character, def, spawnPos);
            ApplyInitialFacingAndAnimation(runner, handle, def, spawnPos, targetPos);
            BindMarker(character, actorKey, ResolveOwnerSkillUid(run, skill));

            SkillDummyActorPresentationUtility.ApplyRuntimeLocks(handle);
            registry[actorKey] = handle;
            SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
            ApplyInitialFade(runner, registry, handle, def);
            return true;
        }

        /// <summary>
        /// 더미 생성 정의에 필수 값이 들어 있는지 확인하고 actorKey를 정규화합니다.
        /// </summary>
        /// <param name="def">검증할 더미 생성 이벤트 정의입니다.</param>
        /// <param name="actorKey">정규화된 더미 액터 키입니다.</param>
        /// <returns>필수 값 검증에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryValidateDefinition(SpawnDummyCharacterEventDefinition def, out string actorKey)
        {
            actorKey = string.Empty;
            if (def.characterUid <= 0)
            {
                Debug.LogWarning("[SkillExecutor] SpawnDummyCharacter characterUid must be greater than 0.");
                return false;
            }

            actorKey = SkillDummyEventUtility.NormalizeActorKey(def.actorKey);
            if (string.IsNullOrEmpty(actorKey))
            {
                Debug.LogWarning("[SkillExecutor] SpawnDummyCharacter actorKey is empty.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 같은 actorKey를 가진 기존 더미가 있을 때 교체 정책에 따라 등록 공간을 확보합니다.
        /// </summary>
        /// <param name="runner">기존 더미 정리에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="actorKey">확보할 더미 액터 키입니다.</param>
        /// <param name="replaceIfExists">기존 더미가 있을 때 교체할지 여부입니다.</param>
        /// <returns>새 더미를 등록해도 되면 <see langword="true"/>입니다.</returns>
        private static bool TryReserveActorKey(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            string actorKey,
            bool replaceIfExists)
        {
            SkillDummyActorLifecycleUtility.Prune(registry);

            if (!registry.TryGetValue(actorKey, out var existing) || existing == null)
                return true;

            if (!replaceIfExists)
                return false;

            SkillDummyActorLifecycleUtility.DestroyActor(
                runner,
                registry,
                existing,
                destroyGameObject: true,
                removeFromRegistry: true);
            return true;
        }

        /// <summary>
        /// 현재 컨텍스트 또는 스냅샷 기준으로 더미 생성 위치 계산에 사용할 참조 좌표를 결정합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="useSnapshotCenter">스냅샷 좌표를 사용할지 여부입니다.</param>
        /// <param name="snapshotCasterPos">스킬 시작 시점 캐스터 위치 스냅샷입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        /// <param name="casterPos">해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        private static void ResolveSpawnReferencePositions(
            SkillTargetContext ctx,
            bool useSnapshotCenter,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            out Vector3 casterPos,
            out Vector3 targetPos,
            out Vector3 groundPoint)
        {
            if (useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
                return;
            }

            casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            groundPoint = ctx.groundPoint;
        }

        /// <summary>
        /// CharacterManager를 통해 실제 더미 캐릭터 GameObject를 생성하고 런타임 제어 상태로 초기화합니다.
        /// </summary>
        /// <param name="sceneGame">현재 SceneGame 인스턴스입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="actorKey">생성할 더미 액터 키입니다.</param>
        /// <param name="spawnPos">최종 생성 위치입니다.</param>
        /// <param name="character">생성된 CharacterBase입니다.</param>
        /// <returns>더미 캐릭터 생성과 CharacterBase 조회에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryCreateCharacter(
            SceneGame sceneGame,
            SpawnDummyCharacterEventDefinition def,
            string actorKey,
            Vector3 spawnPos,
            out CharacterBase character)
        {
            character = null;
            int mapUid = sceneGame.mapManager != null ? sceneGame.mapManager.GetCurrentMapUid() : 0;
            var regenData = new CharacterRegenData(def.characterUid, spawnPos, flip: false, mapUid, defaultVisible: true);

            CharacterConstants.Type sourceType = SkillDummyEventUtility.ResolveSourceCharacterType(def.sourceType);
            GameObject dummyObject = sceneGame.CharacterManager.CreateDummyCharacter(
                sourceType,
                def.characterUid,
                regenData);

            if (dummyObject == null)
            {
                Debug.LogWarning($"[SkillExecutor] Failed to spawn dummy character. source={def.sourceType}, uid={def.characterUid}, key={actorKey}");
                return false;
            }

            dummyObject.transform.position = new Vector3(spawnPos.x, spawnPos.y, dummyObject.transform.position.z);

            character = dummyObject.GetComponent<CharacterBase>() ?? dummyObject.GetComponentInParent<CharacterBase>();
            if (character == null)
            {
                sceneGame.CharacterManager.RemoveCharacter(dummyObject);
                Debug.LogWarning($"[SkillExecutor] Spawned dummy has no CharacterBase. key={actorKey}, uid={def.characterUid}");
                return false;
            }

            SkillDummyActorPresentationUtility.ConfigureRuntime(character);
            return true;
        }

        /// <summary>
        /// 생성된 더미 캐릭터를 추적할 런타임 핸들을 구성합니다.
        /// </summary>
        /// <param name="actorKey">더미 액터 키입니다.</param>
        /// <param name="character">생성된 더미 캐릭터입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="spawnPos">최종 생성 위치입니다.</param>
        /// <returns>초기 상태가 채워진 더미 액터 핸들입니다.</returns>
        private static SkillDummyActorHandle CreateHandle(
            string actorKey,
            CharacterBase character,
            SpawnDummyCharacterEventDefinition def,
            Vector3 spawnPos)
        {
            return new SkillDummyActorHandle
            {
                ActorKey = actorKey,
                Character = character,
                DespawnOnSkillEnd = def.despawnOnSkillEnd,
                DespawnOnCancel = def.despawnOnCancel,
                GroundPosition = new Vector3(spawnPos.x, spawnPos.y, character.transform.position.z),
                AirHeight = 0f,
            };
        }

        /// <summary>
        /// 생성 직후 바라보기 방향과 초기 애니메이션을 적용합니다.
        /// </summary>
        /// <param name="runner">예약된 애니메이션 후속 전환을 취소할 MonoBehaviour입니다.</param>
        /// <param name="handle">초기 연출을 적용할 더미 핸들입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="spawnPos">더미가 생성된 월드 좌표입니다.</param>
        /// <param name="targetPos">생성 시점에 해석된 잠금 타겟 월드 좌표입니다.</param>
        private static void ApplyInitialFacingAndAnimation(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            SpawnDummyCharacterEventDefinition def,
            Vector3 spawnPos,
            Vector3 targetPos)
        {
            if (handle == null || handle.Character == null)
                return;

            ApplyInitialFacing(handle.Character, def, spawnPos, targetPos);

            if (!string.IsNullOrWhiteSpace(def.initialAnimationName))
            {
                SkillDummyActorAnimationEventUtility.PlayAnimation(
                    runner,
                    handle,
                    def.initialAnimationName,
                    def.initialAnimationLoop,
                    def.initialAnimationTimeScale);
            }
        }

        /// <summary>
        /// 생성 직후 바라보기 정책에 따라 더미 캐릭터의 초기 방향을 적용합니다.
        /// </summary>
        /// <param name="character">방향을 적용할 더미 캐릭터입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="spawnPos">더미가 생성된 월드 좌표입니다.</param>
        /// <param name="targetPos">생성 시점에 해석된 잠금 타겟 월드 좌표입니다.</param>
        private static void ApplyInitialFacing(
            CharacterBase character,
            SpawnDummyCharacterEventDefinition def,
            Vector3 spawnPos,
            Vector3 targetPos)
        {
            if (character == null || def == null)
                return;

            switch (def.spawnFacingPolicy)
            {
                case DummySpawnFacingPolicy.LookAtTarget:
                    ApplyFacingTowardTarget(character, def, spawnPos, targetPos);
                    break;

                case DummySpawnFacingPolicy.FixedDirection:
                default:
                    ApplyFixedFacing(character, def.spawnFacing);
                    break;
            }
        }

        /// <summary>
        /// 지정된 고정 방향이 유효할 때 더미 캐릭터에 적용합니다.
        /// </summary>
        /// <param name="character">방향을 적용할 더미 캐릭터입니다.</param>
        /// <param name="facing">적용할 고정 8방향입니다.</param>
        private static void ApplyFixedFacing(CharacterBase character, CharacterConstants.FacingDirection8 facing)
        {
            if (character == null || facing == CharacterConstants.FacingDirection8.None)
                return;

            character.SetFacing(facing);
        }

        /// <summary>
        /// 생성 위치에서 타겟 위치를 바라보는 방향을 계산해 더미 캐릭터에 적용합니다.
        /// </summary>
        /// <param name="character">방향을 적용할 더미 캐릭터입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="spawnPos">더미가 생성된 월드 좌표입니다.</param>
        /// <param name="targetPos">생성 시점에 해석된 잠금 타겟 월드 좌표입니다.</param>
        private static void ApplyFacingTowardTarget(
            CharacterBase character,
            SpawnDummyCharacterEventDefinition def,
            Vector3 spawnPos,
            Vector3 targetPos)
        {
            if (character == null)
                return;

            Vector2 direction = new(targetPos.x - spawnPos.x, targetPos.y - spawnPos.y);
            if (direction.sqrMagnitude <= 1e-6f)
            {
                ApplyFixedFacing(character, def.spawnFacing);
                return;
            }

            character.SetFacing(direction);
        }

        /// <summary>
        /// 더미 캐릭터에 액터 키와 생성한 스킬 UID를 기록하는 마커를 연결합니다.
        /// </summary>
        /// <param name="character">마커를 연결할 더미 캐릭터입니다.</param>
        /// <param name="actorKey">더미 액터 키입니다.</param>
        /// <param name="ownerSkillUid">더미를 생성한 스킬 UID입니다.</param>
        private static void BindMarker(CharacterBase character, string actorKey, int ownerSkillUid)
        {
            if (character == null)
                return;

            var marker = character.GetComponent<SkillDummyCharacterMarker>();
            if (marker == null)
                marker = character.gameObject.AddComponent<SkillDummyCharacterMarker>();

            marker.Bind(actorKey, ownerSkillUid);
        }

        /// <summary>
        /// 런타임 스킬 런 또는 스킬 정의에서 생성 주체 스킬 UID를 결정합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <returns>확인된 스킬 UID이며 없으면 0입니다.</returns>
        private static int ResolveOwnerSkillUid(SkillRun run, RuntimeSkillDefinition skill)
        {
            if (run != null)
                return run.SkillUid;

            return skill != null ? skill.Uid : 0;
        }

        /// <summary>
        /// 생성 직후 페이드 인 사용 여부에 따라 알파값과 페이드 코루틴을 초기화합니다.
        /// </summary>
        /// <param name="runner">페이드 코루틴을 실행할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">페이드 초기화를 적용할 더미 핸들입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        private static void ApplyInitialFade(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            SpawnDummyCharacterEventDefinition def)
        {
            if (handle == null || handle.Character == null)
                return;

            if (def.fadeInEnabled && def.fadeInDurationSeconds > 0f)
            {
                SkillDummyActorPresentationUtility.SetVisualAlpha(handle.Character, 0f);
                handle.ActiveFadeCoroutine = SkillDummyActorLifecycleUtility.StartFade(
                    runner,
                    registry,
                    handle,
                    fadeIn: true,
                    durationSeconds: def.fadeInDurationSeconds,
                    destroyAfterFade: false,
                    removeFromRegistry: false);
                return;
            }

            SkillDummyActorPresentationUtility.SetVisualAlpha(handle.Character, 1f);
        }
    }
}
