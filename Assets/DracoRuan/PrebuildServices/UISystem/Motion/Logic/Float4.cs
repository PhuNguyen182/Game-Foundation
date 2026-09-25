using System;

namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    /// <summary>
    /// Engine-agnostic stand-in for UnityEngine.Vector4, wide enough to carry a track's
    /// value regardless of kind (float/Vector2/Vector3/Color all fit). Keeps value-mode
    /// resolution testable outside Unity; the engine layer converts at the boundary.
    /// </summary>
    public readonly struct Float4 : IEquatable<Float4>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public Float4(float x, float y, float z, float w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }

        public static Float4 operator +(Float4 a, Float4 b) =>
            new Float4(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

        public static Float4 operator -(Float4 a, Float4 b) =>
            new Float4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

        public static Float4 Scale(Float4 a, Float4 b) =>
            new Float4(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);

        public static Float4 LerpUnclamped(Float4 a, Float4 b, float t) =>
            new Float4(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t,
                a.W + (b.W - a.W) * t);

        public bool Equals(Float4 other) =>
            this.X.Equals(other.X) && this.Y.Equals(other.Y) && this.Z.Equals(other.Z) && this.W.Equals(other.W);

        public override bool Equals(object obj) => obj is Float4 other && this.Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = this.X.GetHashCode();
                hash = (hash * 397) ^ this.Y.GetHashCode();
                hash = (hash * 397) ^ this.Z.GetHashCode();
                hash = (hash * 397) ^ this.W.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({this.X}, {this.Y}, {this.Z}, {this.W})";
    }
}
