using System;

namespace DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Badge
{
    /// <summary>
    /// A hierarchical badge path, segment-separated by '/' (e.g. "Shop/Weapons/Sword") - the
    /// "cây key" (key tree) REWRITE_PLAN.md asks for. Two keys with the same segments are equal
    /// regardless of how they were constructed (string vs segment array), so a Dictionary&lt;BadgeKey, _&gt;
    /// works as the natural backing store for an IBadgeService implementation.
    /// </summary>
    public readonly struct BadgeKey : IEquatable<BadgeKey>
    {
        public const char Separator = '/';

        private readonly string _path;

        public BadgeKey(string path) => this._path = path ?? string.Empty;

        public static BadgeKey Combine(BadgeKey parent, string segment) =>
            new BadgeKey(parent.IsRoot ? segment : parent._path + Separator + segment);

        public bool IsRoot => string.IsNullOrEmpty(this._path);

        /// <summary>True if `other` is this key or a descendant of it (e.g. "Shop" is an
        /// ancestor-or-self of "Shop/Weapons") - the relation IBadgeService.GetCount's roll-up
        /// and Changed's subtree-notification both walk.</summary>
        public bool IsAncestorOrSelfOf(BadgeKey other)
        {
            if (this.IsRoot)
                return true;

            if (this._path.Length > other._path.Length)
                return false;

            if (!other._path.StartsWith(this._path, StringComparison.Ordinal))
                return false;

            return other._path.Length == this._path.Length || other._path[this._path.Length] == Separator;
        }

        /// <summary>This key's parent, or the root key if this is already a top-level segment.</summary>
        public BadgeKey Parent()
        {
            if (this.IsRoot)
                return this;

            int lastSeparator = this._path.LastIndexOf(Separator);
            return lastSeparator < 0 ? new BadgeKey(string.Empty) : new BadgeKey(this._path.Substring(0, lastSeparator));
        }

        public bool Equals(BadgeKey other) => string.Equals(this._path, other._path, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is BadgeKey other && this.Equals(other);

        public override int GetHashCode() => this._path?.GetHashCode() ?? 0;

        public override string ToString() => this._path;

        public static implicit operator BadgeKey(string path) => new BadgeKey(path);
    }
}
