using System;
using DracoRuan.Foundation.DataFlow.Core.Serialization;
#if USE_MESSAGE_PACK
using MessagePack;
#endif

namespace DracoRuan.Foundation.DataFlow.Serialization
{
    /// <summary>
    /// MessagePack implementation of <see cref="IPayloadCodec"/> — the default for save payloads.
    /// </summary>
    /// <remarks>
    /// Always serializes through <see cref="DataFlowSerialization.Options"/> rather than
    /// <c>MessagePackSerializer</c>'s implicit defaults, so the AOT resolver chain and the
    /// untrusted-data limits apply on every call. Under IL2CPP the implicit path needs
    /// <c>Reflection.Emit</c> and throws.
    /// </remarks>
    public sealed class MessagePackPayloadCodec : IPayloadCodec
    {
        public byte[] Serialize<T>(T value)
        {
#if USE_MESSAGE_PACK
            EnsureInitialized();
            return MessagePackSerializer.Serialize(value, DataFlowSerialization.Options);
#else
            throw new NotSupportedException(
                "USE_MESSAGE_PACK is not defined, so save payloads cannot be serialized.");
#endif
        }

        public T Deserialize<T>(ReadOnlySpan<byte> payload)
        {
#if USE_MESSAGE_PACK
            EnsureInitialized();
            return MessagePackSerializer.Deserialize<T>(payload.ToArray(), DataFlowSerialization.Options);
#else
            throw new NotSupportedException(
                "USE_MESSAGE_PACK is not defined, so save payloads cannot be deserialized.");
#endif
        }

        public byte[] Serialize(Type type, object value)
        {
#if USE_MESSAGE_PACK
            EnsureInitialized();
            return MessagePackSerializer.Serialize(type, value, DataFlowSerialization.Options);
#else
            throw new NotSupportedException(
                "USE_MESSAGE_PACK is not defined, so save payloads cannot be serialized.");
#endif
        }

        public object Deserialize(Type type, ReadOnlySpan<byte> payload)
        {
#if USE_MESSAGE_PACK
            EnsureInitialized();
            return MessagePackSerializer.Deserialize(type, payload.ToArray(), DataFlowSerialization.Options);
#else
            throw new NotSupportedException(
                "USE_MESSAGE_PACK is not defined, so save payloads cannot be deserialized.");
#endif
        }

        private static void EnsureInitialized()
        {
            // Cheap guard: in the Editor, code can run before any RuntimeInitializeOnLoadMethod has.
            if (!DataFlowSerialization.IsInitialized)
                DataFlowSerialization.Initialize();
        }
    }
}