using System;
using System.Collections.Generic;
using UnityEngine;
#if USE_MESSAGE_PACK
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Unity;
#endif

namespace DracoRuan.Foundation.DataFlow.Core.Serialization
{
    /// <summary>
    /// Installs the MessagePack resolver chain that every DataFlow save/load operation relies on.
    ///
    /// <para>
    /// Why this type exists: under IL2CPP the dynamic MessagePack resolvers need <c>Reflection.Emit</c>,
    /// which is unavailable, so serialization throws at runtime. The project ships
    /// <c>MessagePack.SourceGenerator.dll</c> as a Roslyn analyzer, so formatters for
    /// <c>[MessagePackObject]</c> types ARE generated at compile time — but the generated resolver is
    /// not wired into <see cref="MessagePackSerializer.DefaultOptions"/> automatically. This class does
    /// that wiring, and does it before anything else can serialize.
    /// </para>
    ///
    /// <para>
    /// Ordering matters: initialization runs at <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>,
    /// which is strictly earlier than <c>BeforeSceneLoad</c>. The root VContainer
    /// <c>LifetimeScope</c> also spawns at <c>BeforeSceneLoad</c>, and the relative order of two
    /// <c>BeforeSceneLoad</c> callbacks is unspecified — so this must not use that timing.
    /// </para>
    ///
    /// <para>
    /// Security: options use <see cref="MessagePackSecurity.UntrustedData"/>. A corrupted or tampered
    /// payload can declare an enormous length prefix and cause an out-of-memory crash otherwise.
    /// This is a second line of defence only — the envelope checksum MUST be verified before any
    /// deserialize call. See <c>SaveEnvelopeCodec</c>.
    /// </para>
    /// </summary>
    public static class DataFlowSerialization
    {
        private const string LogTag = "DataFlowSerialization";

        /// <summary>
        /// Type names the MessagePack code generators are known to emit, most recent convention first.
        /// Probed by reflection because Unity compiles source-generator output in memory, so the
        /// generated resolver cannot be referenced directly from this assembly.
        /// </summary>
        private static readonly string[] GeneratedResolverTypeNames =
        {
            "MessagePack.GeneratedMessagePackResolver", // v3 source generator default
            "MessagePack.Resolvers.GeneratedResolver", // v2 `mpc` default
        };

        private static bool _isInitialized;

#if USE_MESSAGE_PACK
        private static readonly List<IFormatterResolver> CustomResolvers = new();

        /// <summary>
        /// Options every DataFlow serializer must use. Also installed as
        /// <see cref="MessagePackSerializer.DefaultOptions"/> so code outside DataFlow benefits too.
        /// </summary>
        public static MessagePackSerializerOptions Options { get; private set; } =
            MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);
#endif

        /// <summary>
        /// True once the resolver chain has been installed. Serializing before this point is a bug
        /// on IL2CPP even though it happens to work under Mono.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Registers an extra resolver ahead of the built-in chain. Call this before the first
        /// serialize — from a <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> hook in the
        /// game assembly — when a game ships formatters this foundation cannot see.
        /// </summary>
        public static void RegisterResolver(object formatterResolver)
        {
#if USE_MESSAGE_PACK
            if (formatterResolver is not IFormatterResolver resolver)
            {
                Debug.LogError(
                    $"[{LogTag}] {formatterResolver?.GetType().FullName ?? "null"} is not an IFormatterResolver.");
                return;
            }

            if (CustomResolvers.Contains(resolver))
                return;

            CustomResolvers.Add(resolver);

            // Re-run so a late registration still takes effect.
            _isInitialized = false;
            Initialize();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Initialize()
        {
            if (_isInitialized)
                return;

#if USE_MESSAGE_PACK
            List<IFormatterResolver> resolvers = new(CustomResolvers);

            IFormatterResolver generatedResolver = TryResolveGeneratedResolver();
            if (generatedResolver != null)
                resolvers.Add(generatedResolver);
            else
                Debug.LogWarning(
                    $"[{LogTag}] No generated MessagePack resolver found. Serialization will fall back to " +
                    "the dynamic resolvers, which require Reflection.Emit and WILL throw under IL2CPP. " +
                    "Verify MessagePack.SourceGenerator.dll is labelled as a RoslynAnalyzer, or call " +
                    $"{nameof(DataFlowSerialization)}.{nameof(RegisterResolver)} with your generated resolver.");

            // Unity built-in types (Vector3, Color, ...) then the standard chain.
            resolvers.Add(UnityResolver.Instance);
            resolvers.Add(StandardResolver.Instance);

            StaticCompositeResolver.Instance.Register(resolvers.ToArray());

            Options = MessagePackSerializerOptions.Standard
                .WithResolver(StaticCompositeResolver.Instance)
                .WithSecurity(MessagePackSecurity.UntrustedData);

            MessagePackSerializer.DefaultOptions = Options;
#endif

            _isInitialized = true;
        }

#if USE_MESSAGE_PACK
        private static IFormatterResolver TryResolveGeneratedResolver()
        {
            foreach (string typeName in GeneratedResolverTypeNames)
            {
                IFormatterResolver resolver = TryCreateResolver(typeName);
                if (resolver != null)
                    return resolver;
            }

            return null;
        }

        private static IFormatterResolver TryCreateResolver(string typeName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type resolverType;
                try
                {
                    resolverType = assembly.GetType(typeName, throwOnError: false);
                }
                catch (Exception)
                {
                    continue;
                }

                if (resolverType == null)
                    continue;

                System.Reflection.FieldInfo instanceField = resolverType.GetField("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (instanceField?.GetValue(null) is IFormatterResolver fromField)
                    return fromField;

                System.Reflection.PropertyInfo instanceProperty = resolverType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (instanceProperty?.GetValue(null) is IFormatterResolver fromProperty)
                    return fromProperty;
            }

            return null;
        }
#endif
    }
}