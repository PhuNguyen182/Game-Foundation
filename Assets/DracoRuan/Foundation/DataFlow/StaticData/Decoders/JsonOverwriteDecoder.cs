using System;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using Newtonsoft.Json;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// Builds a config ScriptableObject from a JSON payload.
    /// </summary>
    /// <remarks>
    /// <para><b>CreateInstance then populate, never <c>DeserializeObject</c>.</b> Newtonsoft builds
    /// objects with <c>Activator.CreateInstance</c>, which for a ScriptableObject is <c>new T()</c> —
    /// the constructor Unity tells you not to call, and which does not give the object a valid native
    /// counterpart. Unity takes this seriously enough that <c>JsonUtility.FromJson&lt;T&gt;</c> throws
    /// outright for <c>UnityEngine.Object</c> types and offers only an overwrite overload.</para>
    ///
    /// <para><b>Populate is also the right semantics for config.</b> A remote payload should be able
    /// to carry only the handful of fields an experiment changes and leave the rest at the values the
    /// designer authored. <c>DeserializeObject</c> resets every absent field to its default, so a
    /// partial payload would silently zero out everything it did not mention.</para>
    ///
    /// <para><b>Unknown members are an error.</b> A remote payload whose shape has drifted from this
    /// build's data class is the one case where falling back to the shipped asset is clearly right.
    /// Ignoring the unknown field instead would load a half-applied config and call it a success.</para>
    /// </remarks>
    public sealed class JsonOverwriteDecoder<TData> : IStaticDataDecoder<TData>
        where TData : ScriptableObject
    {
        private readonly JsonSerializerSettings _settings;

        /// <param name="settings">
        /// Override only when a model needs something <see cref="StaticDataJson.Settings"/> does not
        /// provide — a custom converter, say. Whatever is passed must keep
        /// <see cref="UnitySerializationContractResolver"/> or private <c>[SerializeField]</c>
        /// members stop being populated, which fails silently rather than loudly.
        /// </param>
        public JsonOverwriteDecoder(JsonSerializerSettings settings = null)
        {
            this._settings = settings ?? StaticDataJson.Settings;
        }

        public DecodeResult<TData> Decode(in StaticDataPayload payload)
        {
            if (payload.Kind != StaticDataPayloadKind.Text)
                return DecodeResult<TData>.Failure(
                    $"Expected a text payload for {typeof(TData).Name}, got {payload.Kind}.");

            TData instance = ScriptableObject.CreateInstance<TData>();

            try
            {
                JsonConvert.PopulateObject(payload.Text, instance, this._settings);
            }
            catch (Exception exception)
            {
                // The half-populated instance is unusable and would leak; the chain moves on.
                StaticDataObjectLifetime.Destroy(instance);
                return DecodeResult<TData>.Failure(
                    $"JSON did not match {typeof(TData).Name}: {exception.Message}");
            }

            instance.name = typeof(TData).Name;

            // Created here, so this decoder's caller is the one that destroys it.
            return DecodeResult<TData>.Success(instance, ownsValue: true);
        }
    }
}