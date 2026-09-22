using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// The single place that decides whether a set of audio ids can coexist.
    /// </summary>
    /// <remarks>
    /// <para>Two ids clash in more ways than string equality catches. They can differ only by case,
    /// which is a typo in every real project and would emit two constants a call site cannot tell
    /// apart; or they can be genuinely different and still generate the same C# member, which is
    /// what happens to <c>a-b</c> and <c>a_b</c>. Every kind blocks generation, because the
    /// alternative is a file that either does not compile or compiles into something unreadable.
    /// </para>
    ///
    /// <para>Each record is reported at most once, under the most specific kind that applies, and
    /// the scan is ordered by id rather than by the order assets happened to be discovered, so the
    /// same project always produces the same report.</para>
    /// </remarks>
    public static class AudioIdCollisionDetector
    {
        private static readonly string[] NoOwners = Array.Empty<string>();

        /// <summary>Every conflict in <paramref name="records"/>, in a stable order.</summary>
        public static IReadOnlyList<AudioIdConflict> Detect(IReadOnlyList<AudioIdRecord> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            List<AudioIdConflict> conflicts = new List<AudioIdConflict>();
            bool[] reported = new bool[records.Count];
            string[] members = new string[records.Count];

            CollectUnusable(records, reported, members, conflicts);

            CollectGroups(records, reported, members, byMember: false, ignoreCase: false,
                AudioIdConflictKind.DuplicateExact, conflicts);
            CollectGroups(records, reported, members, byMember: false, ignoreCase: true,
                AudioIdConflictKind.DuplicateIgnoringCase, conflicts);
            CollectGroups(records, reported, members, byMember: true, ignoreCase: false,
                AudioIdConflictKind.MemberCollision, conflicts);
            CollectGroups(records, reported, members, byMember: true, ignoreCase: true,
                AudioIdConflictKind.MemberCollisionIgnoringCase, conflicts);

            return conflicts;
        }

        /// <summary>
        /// Whether <paramref name="candidateId"/> could be used right now, given
        /// <paramref name="existing"/>. This is what the identifier text field calls.
        /// </summary>
        /// <param name="ignoreOwnerPath">
        /// The asset doing the asking, so an entry editing its own id does not report itself.
        /// </param>
        public static AudioIdConflict? CheckAgainst(
            string candidateId, IReadOnlyList<AudioIdRecord> existing, string ignoreOwnerPath)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));

            AudioIdSanitizeResult candidate = AudioIdSanitizer.ToMemberName(candidateId);
            if (candidate.Status == AudioIdStatus.Rejected)
                return new AudioIdConflict(
                    AudioIdConflictKind.Unusable,
                    $"Audio id '{candidateId}' cannot be generated: {candidate.Message}",
                    NoOwners);

            AudioIdConflict? found = null;

            for (int i = 0; i < existing.Count; i++)
            {
                AudioIdRecord record = existing[i];

                if (ignoreOwnerPath != null
                    && string.Equals(record.OwnerPath, ignoreOwnerPath, StringComparison.Ordinal))
                    continue;

                AudioIdSanitizeResult other = AudioIdSanitizer.ToMemberName(record.Id);
                if (other.Status == AudioIdStatus.Rejected)
                    continue;

                AudioIdConflictKind? kind = Compare(candidateId, candidate.MemberName, record.Id, other.MemberName);
                if (kind == null)
                    continue;

                // Keep the most specific match rather than the first one found, so the message does
                // not depend on the order assets were discovered in.
                if (found == null || kind.Value < found.Value.Kind)
                    found = new AudioIdConflict(kind.Value, DescribeAgainst(kind.Value, candidateId, record),
                        new[] { record.OwnerPath });

                if (kind.Value == AudioIdConflictKind.DuplicateExact)
                    break;
            }

            return found;
        }

        private static AudioIdConflictKind? Compare(
            string candidateId, string candidateMember, string otherId, string otherMember)
        {
            if (string.Equals(candidateId, otherId, StringComparison.Ordinal))
                return AudioIdConflictKind.DuplicateExact;

            if (string.Equals(candidateId, otherId, StringComparison.OrdinalIgnoreCase))
                return AudioIdConflictKind.DuplicateIgnoringCase;

            if (string.Equals(candidateMember, otherMember, StringComparison.Ordinal))
                return AudioIdConflictKind.MemberCollision;

            if (string.Equals(candidateMember, otherMember, StringComparison.OrdinalIgnoreCase))
                return AudioIdConflictKind.MemberCollisionIgnoringCase;

            return null;
        }

        private static string DescribeAgainst(AudioIdConflictKind kind, string candidateId, AudioIdRecord record)
        {
            switch (kind)
            {
                case AudioIdConflictKind.DuplicateExact:
                    return $"Audio id '{candidateId}' is already used by {record.OwnerPath}.";

                case AudioIdConflictKind.DuplicateIgnoringCase:
                    return $"Audio id '{candidateId}' differs only by case from '{record.Id}' " +
                           $"({record.OwnerPath}).";

                case AudioIdConflictKind.MemberCollision:
                    return $"Audio id '{candidateId}' is different from '{record.Id}' " +
                           $"({record.OwnerPath}) but both generate the same C# member.";

                default:
                    return $"Audio id '{candidateId}' generates a C# member differing only by case " +
                           $"from the one '{record.Id}' ({record.OwnerPath}) generates.";
            }
        }

        private static void CollectUnusable(
            IReadOnlyList<AudioIdRecord> records, bool[] reported, string[] members,
            List<AudioIdConflict> conflicts)
        {
            SortedDictionary<string, List<int>> byId =
                new SortedDictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < records.Count; i++)
            {
                AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName(records[i].Id);

                if (result.Status != AudioIdStatus.Rejected)
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
                    AudioIdSanitizeResult result = AudioIdSanitizer.ToMemberName(records[i].Id);
                    conflicts.Add(new AudioIdConflict(
                        AudioIdConflictKind.Unusable,
                        $"Audio id '{records[i].Id}' ({records[i].OwnerPath}) cannot be generated: {result.Message}",
                        new[] { records[i].OwnerPath }));
                }
            }
        }

        private static void CollectGroups(
            IReadOnlyList<AudioIdRecord> records, bool[] reported, string[] members,
            bool byMember, bool ignoreCase, AudioIdConflictKind kind, List<AudioIdConflict> conflicts)
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

                conflicts.Add(new AudioIdConflict(kind, Describe(kind, ids, owners, members[group.Value[0]]), owners));
            }
        }

        private static string Describe(
            AudioIdConflictKind kind, List<string> ids, List<string> owners, string member)
        {
            string quotedIds = "'" + string.Join("', '", ids) + "'";
            string ownerList = string.Join(", ", owners);

            switch (kind)
            {
                case AudioIdConflictKind.DuplicateExact:
                    return $"Audio id '{ids[0]}' is declared by {owners.Count} assets: {ownerList}. " +
                           "Rename all but one of them.";

                case AudioIdConflictKind.DuplicateIgnoringCase:
                    return $"Audio ids {quotedIds} differ only by case ({ownerList}). Rename one of them.";

                case AudioIdConflictKind.MemberCollision:
                    return $"Audio ids {quotedIds} are different ids but all generate the C# member " +
                           $"'{member}' ({ownerList}). Rename one of them.";

                default:
                    return $"Audio ids {quotedIds} generate C# member names that differ only by case " +
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