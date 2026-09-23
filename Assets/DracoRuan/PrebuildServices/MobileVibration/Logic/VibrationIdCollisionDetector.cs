using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>
    /// The single place that decides whether a set of vibration ids can coexist.
    /// </summary>
    /// <remarks>
    /// <para>Mirrors <c>AudioIdCollisionDetector</c> exactly. Two ids clash in more ways than string
    /// equality catches: they can differ only by case, which is a typo in every real project and
    /// would emit two constants a call site cannot tell apart, or they can be genuinely different and
    /// still generate the same C# member, which is what happens to <c>a-b</c> and <c>a_b</c>. Every
    /// kind blocks generation, because the alternative is a file that either does not compile or
    /// compiles into something unreadable.</para>
    ///
    /// <para>Each record is reported at most once, under the most specific kind that applies, and the
    /// scan is ordered by id rather than by the order assets happened to be discovered, so the same
    /// project always produces the same report.</para>
    /// </remarks>
    public static class VibrationIdCollisionDetector
    {
        private static readonly string[] NoOwners = Array.Empty<string>();

        /// <summary>Every conflict in <paramref name="records"/>, in a stable order.</summary>
        public static IReadOnlyList<VibrationIdConflict> Detect(IReadOnlyList<VibrationIdRecord> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            List<VibrationIdConflict> conflicts = new List<VibrationIdConflict>();
            bool[] reported = new bool[records.Count];
            string[] members = new string[records.Count];

            CollectUnusable(records, reported, members, conflicts);

            CollectGroups(records, reported, members, byMember: false, ignoreCase: false,
                VibrationIdConflictKind.DuplicateExact, conflicts);
            CollectGroups(records, reported, members, byMember: false, ignoreCase: true,
                VibrationIdConflictKind.DuplicateIgnoringCase, conflicts);
            CollectGroups(records, reported, members, byMember: true, ignoreCase: false,
                VibrationIdConflictKind.MemberCollision, conflicts);
            CollectGroups(records, reported, members, byMember: true, ignoreCase: true,
                VibrationIdConflictKind.MemberCollisionIgnoringCase, conflicts);

            return conflicts;
        }

        /// <summary>
        /// Whether <paramref name="candidateId"/> could be used right now, given
        /// <paramref name="existing"/>. This is what the identifier text field calls.
        /// </summary>
        /// <param name="ignoreOwnerPath">
        /// The asset doing the asking, so an entry editing its own id does not report itself.
        /// </param>
        public static VibrationIdConflict? CheckAgainst(
            string candidateId, IReadOnlyList<VibrationIdRecord> existing, string ignoreOwnerPath)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));

            VibrationIdSanitizeResult candidate = VibrationIdSanitizer.ToMemberName(candidateId);
            if (candidate.Status == VibrationIdStatus.Rejected)
                return new VibrationIdConflict(
                    VibrationIdConflictKind.Unusable,
                    $"Vibration id '{candidateId}' cannot be generated: {candidate.Message}",
                    NoOwners);

            VibrationIdConflict? found = null;

            for (int i = 0; i < existing.Count; i++)
            {
                VibrationIdRecord record = existing[i];

                if (ignoreOwnerPath != null
                    && string.Equals(record.OwnerPath, ignoreOwnerPath, StringComparison.Ordinal))
                    continue;

                VibrationIdSanitizeResult other = VibrationIdSanitizer.ToMemberName(record.Id);
                if (other.Status == VibrationIdStatus.Rejected)
                    continue;

                VibrationIdConflictKind? kind = Compare(candidateId, candidate.MemberName, record.Id, other.MemberName);
                if (kind == null)
                    continue;

                // Keep the most specific match rather than the first one found, so the message does
                // not depend on the order assets were discovered in.
                if (found == null || kind.Value < found.Value.Kind)
                    found = new VibrationIdConflict(kind.Value, DescribeAgainst(kind.Value, candidateId, record),
                        new[] { record.OwnerPath });

                if (kind.Value == VibrationIdConflictKind.DuplicateExact)
                    break;
            }

            return found;
        }

        private static VibrationIdConflictKind? Compare(
            string candidateId, string candidateMember, string otherId, string otherMember)
        {
            if (string.Equals(candidateId, otherId, StringComparison.Ordinal))
                return VibrationIdConflictKind.DuplicateExact;

            if (string.Equals(candidateId, otherId, StringComparison.OrdinalIgnoreCase))
                return VibrationIdConflictKind.DuplicateIgnoringCase;

            if (string.Equals(candidateMember, otherMember, StringComparison.Ordinal))
                return VibrationIdConflictKind.MemberCollision;

            if (string.Equals(candidateMember, otherMember, StringComparison.OrdinalIgnoreCase))
                return VibrationIdConflictKind.MemberCollisionIgnoringCase;

            return null;
        }

        private static string DescribeAgainst(VibrationIdConflictKind kind, string candidateId, VibrationIdRecord record)
        {
            switch (kind)
            {
                case VibrationIdConflictKind.DuplicateExact:
                    return $"Vibration id '{candidateId}' is already used by {record.OwnerPath}.";

                case VibrationIdConflictKind.DuplicateIgnoringCase:
                    return $"Vibration id '{candidateId}' differs only by case from '{record.Id}' " +
                           $"({record.OwnerPath}).";

                case VibrationIdConflictKind.MemberCollision:
                    return $"Vibration id '{candidateId}' is different from '{record.Id}' " +
                           $"({record.OwnerPath}) but both generate the same C# member.";

                default:
                    return $"Vibration id '{candidateId}' generates a C# member differing only by case " +
                           $"from the one '{record.Id}' ({record.OwnerPath}) generates.";
            }
        }

        private static void CollectUnusable(
            IReadOnlyList<VibrationIdRecord> records, bool[] reported, string[] members,
            List<VibrationIdConflict> conflicts)
        {
            SortedDictionary<string, List<int>> byId =
                new SortedDictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < records.Count; i++)
            {
                VibrationIdSanitizeResult result = VibrationIdSanitizer.ToMemberName(records[i].Id);

                if (result.Status != VibrationIdStatus.Rejected)
                {
                    members[i] = result.MemberName;
                    continue;
                }

                reported[i] = true;
                AddToGroup(byId, records[i].Id ?? string.Empty, i);
            }

            foreach (KeyValuePair<string, List<int>> group in byId)
            {
                foreach (int i in group.Value)
                {
                    VibrationIdSanitizeResult result = VibrationIdSanitizer.ToMemberName(records[i].Id);
                    conflicts.Add(new VibrationIdConflict(
                        VibrationIdConflictKind.Unusable,
                        $"Vibration id '{records[i].Id}' ({records[i].OwnerPath}) cannot be generated: {result.Message}",
                        new[] { records[i].OwnerPath }));
                }
            }
        }

        private static void CollectGroups(
            IReadOnlyList<VibrationIdRecord> records, bool[] reported, string[] members,
            bool byMember, bool ignoreCase, VibrationIdConflictKind kind, List<VibrationIdConflict> conflicts)
        {
            SortedDictionary<string, List<int>> groups =
                new SortedDictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < records.Count; i++)
            {
                if (reported[i])
                    continue;

                string key = byMember ? members[i] : records[i].Id;
                if (key == null)
                    continue;

                AddToGroup(groups, ignoreCase ? key.ToLowerInvariant() : key, i);
            }

            foreach (KeyValuePair<string, List<int>> group in groups)
            {
                if (group.Value.Count < 2)
                    continue;

                List<string> ids = new List<string>(group.Value.Count);
                List<string> owners = new List<string>(group.Value.Count);

                foreach (int i in group.Value)
                {
                    reported[i] = true;
                    ids.Add(records[i].Id);
                    owners.Add(records[i].OwnerPath);
                }

                ids.Sort(StringComparer.Ordinal);
                owners.Sort(StringComparer.Ordinal);

                conflicts.Add(new VibrationIdConflict(kind, Describe(kind, ids, owners, members[group.Value[0]]), owners));
            }
        }

        private static string Describe(
            VibrationIdConflictKind kind, List<string> ids, List<string> owners, string member)
        {
            string quotedIds = "'" + string.Join("', '", ids) + "'";
            string ownerList = string.Join(", ", owners);

            switch (kind)
            {
                case VibrationIdConflictKind.DuplicateExact:
                    return $"Vibration id '{ids[0]}' is declared by {owners.Count} assets: {ownerList}. " +
                           "Rename all but one of them.";

                case VibrationIdConflictKind.DuplicateIgnoringCase:
                    return $"Vibration ids {quotedIds} differ only by case ({ownerList}). Rename one of them.";

                case VibrationIdConflictKind.MemberCollision:
                    return $"Vibration ids {quotedIds} are different ids but all generate the C# member " +
                           $"'{member}' ({ownerList}). Rename one of them.";

                default:
                    return $"Vibration ids {quotedIds} generate C# member names that differ only by case " +
                           $"({ownerList}). Rename one of them.";
            }
        }

        private static void AddToGroup(SortedDictionary<string, List<int>> groups, string key, int index)
        {
            if (!groups.TryGetValue(key, out List<int> group))
            {
                group = new List<int>();
                groups.Add(key, group);
            }

            group.Add(index);
        }
    }
}
