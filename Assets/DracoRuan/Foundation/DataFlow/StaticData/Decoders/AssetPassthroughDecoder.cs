using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// Takes the ScriptableObject a source already loaded and hands it over as-is.
    /// </summary>
    /// <remarks>
    /// <para>The asset belongs to whichever source produced it — Resources or Addressables — so this
    /// reports <c>ownsValue: false</c> and the controller asks that source to release it rather than
    /// destroying it. Destroying it instead would tear down the asset itself, not a copy of it, and
    /// every other reference in the project would go null.</para>
    ///
    /// <para>A type mismatch names both types. The predecessor logged only "data is mismatched",
    /// which for a wrong Addressables address gave no way to tell whether the asset was missing,
    /// the wrong kind, or a sub-asset of the right one.</para>
    /// </remarks>
    public sealed class AssetPassthroughDecoder<TData> : IStaticDataDecoder<TData>
        where TData : ScriptableObject
    {
        public DecodeResult<TData> Decode(in StaticDataPayload payload)
        {
            if (payload.Kind != StaticDataPayloadKind.Asset)
                return DecodeResult<TData>.Failure(
                    $"Expected an asset payload for {typeof(TData).Name}, got {payload.Kind}.");

            if (payload.Asset is not TData typed)
                return DecodeResult<TData>.Failure(
                    $"Expected {typeof(TData).Name} but the asset is " +
                    $"{payload.Asset.GetType().Name} ('{payload.Asset.name}').");

            return DecodeResult<TData>.Success(typed, ownsValue: false);
        }
    }
}
