using System;

namespace DracoRuan.Foundation.DataFlow.Core.Serialization
{
    /// <summary>
    /// Turns a save payload into bytes and back.
    /// </summary>
    /// <remarks>
    /// Kept as an interface so the pure Core has no compile-time dependency on a particular
    /// serializer, and so tests can drive migrations with a trivial codec instead of standing up
    /// MessagePack's AOT resolver chain.
    /// </remarks>
    public interface IPayloadCodec
    {
        byte[] Serialize<T>(T value);

        T Deserialize<T>(ReadOnlySpan<byte> payload);

        /// <summary>
        /// Serializes when the type is only known at runtime.
        /// </summary>
        /// <remarks>
        /// For Editor tooling, which discovers data types by reflection and so has a
        /// <see cref="Type"/> rather than a type argument. Runtime code should prefer the generic
        /// overload.
        /// </remarks>
        byte[] Serialize(Type type, object value);

        /// <summary>Deserializes when the type is only known at runtime.</summary>
        object Deserialize(Type type, ReadOnlySpan<byte> payload);
    }

    /// <summary>
    /// The codec migrators use when they do not specify one.
    /// </summary>
    /// <remarks>
    /// A settable default rather than constructor injection: migrators are small classes authored
    /// per schema bump, and threading a codec through every one of them is boilerplate that buys
    /// nothing. Set it once during bootstrap, before any migration runs.
    /// </remarks>
    public static class PayloadCodec
    {
        private static IPayloadCodec _default;

        /// <summary>
        /// The active codec. Throws if read before bootstrap has set it, rather than silently
        /// falling back to something that would write payloads the game cannot read back.
        /// </summary>
        public static IPayloadCodec Default
        {
            get => _default ?? throw new InvalidOperationException(
                $"No {nameof(IPayloadCodec)} has been installed. Set {nameof(PayloadCodec)}.{nameof(Default)} " +
                "during bootstrap, before any save is read or migrated.");
            set => _default = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>True once a codec has been installed.</summary>
        public static bool IsConfigured => _default != null;

        /// <summary>Clears the codec. For tests; resets state that survives a domain reload.</summary>
        public static void Reset() => _default = null;
    }
}