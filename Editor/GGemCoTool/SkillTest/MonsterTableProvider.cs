using System;
using System.Collections.Generic;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Core(Editor)의 TableLoaderManager를 사용해 monster.txt(TableMonster)를 로드하여
    /// 스킬 테스트 툴에 필요한 최소 정보 형태로 제공한다.
    /// </summary>
    internal static class MonsterTableProvider
    {
        internal readonly struct MonsterRow
        {
            public readonly int Uid;
            public readonly string Name;
            public readonly int AnimationUid;

            public MonsterRow(int uid, string name, int animationUid)
            {
                Uid = uid;
                Name = name;
                AnimationUid = animationUid;
            }

            public override string ToString() => $"{Uid} - {Name}";
        }

        public static IReadOnlyList<MonsterRow> LoadMonsters(bool forceReload)
        {
            var table = GGemCo2DCoreEditor.TableLoaderManager.LoadMonsterTable(forceReload);
            if (table == null) return Array.Empty<MonsterRow>();

            var list = new List<MonsterRow>(256);
            foreach (var kv in table.GetDatas())
            {
                var row = kv.Value;
                if (row == null) continue;
                list.Add(new MonsterRow(row.Uid, row.Name, row.AnimationUid));
            }

            // UID 정렬(안정적인 UX)
            list.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            return list;
        }
    }
}