using DracoRuan.Foundation.DataFlow.Core.Serialization;
using UnityEditor;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Installs the MessagePack resolver chain for Editor-only code paths.
    ///
    /// <para>
    /// <see cref="DataFlowSerialization.Initialize"/> normally runs from
    /// <c>RuntimeInitializeOnLoadMethod</c>, which only fires when entering Play mode. The Local Data
    /// Manager window and EditMode tests serialize without ever entering Play mode, so without this
    /// hook they would run against whatever resolver happened to be installed — or none at all.
    /// </para>
    ///
    /// <para>
    /// This also re-runs after every domain reload, which matters because
    /// <see cref="DataFlowSerialization"/> keeps its installed state in a static field.
    /// </para>
    /// </summary>
    public static class DataFlowSerializationEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => DataFlowSerialization.Initialize();
    }
}
