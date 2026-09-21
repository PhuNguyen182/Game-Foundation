using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// Turns one source's payload into usable data.
    /// </summary>
    /// <remarks>
    /// Decoding is separate from sourcing so the two vary independently: the same
    /// <see cref="CsvRecordDecoder{TRecord,TRecordMap}"/> serves a CSV that shipped in
    /// <c>Resources</c>, one in an Addressables bundle, and one downloaded from a bucket. Adding a
    /// storage backend costs a source; adding a file format costs a decoder; neither costs both.
    /// </remarks>
    public interface IStaticDataDecoder<T>
    {
        DecodeResult<T> Decode(in StaticDataPayload payload);
    }
}
