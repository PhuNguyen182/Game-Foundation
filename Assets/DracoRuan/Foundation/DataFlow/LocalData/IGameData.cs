namespace DracoRuan.Foundation.DataFlow.LocalData
{
    /// <summary>
    /// Marks a class as a save or config data model.
    /// </summary>
    /// <remarks>
    /// <para><b>Version deliberately does not live here.</b> This interface used to carry a
    /// <c>DataVersion</c> property, which made the schema version a property of an <i>instance</i>
    /// when it is really a property of the <i>type</i> — and worse, a second source of truth
    /// competing with the controller's own declaration. Saving read one, loading read the other, so
    /// the moment they disagreed a save was written to a file the loader would never look at.
    /// </para>
    ///
    /// <para>The version is now declared once, by the controller, and stored in the save file's
    /// header. Each schema version has its own frozen data class.</para>
    /// </remarks>
    public interface IGameData
    {
    }
}