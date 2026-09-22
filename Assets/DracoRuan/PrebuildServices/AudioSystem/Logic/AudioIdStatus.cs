namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>How an audio id fared on its way to becoming a C# member name.</summary>
    public enum AudioIdStatus
    {
        /// <summary>The member name is the id, character for character.</summary>
        Ok = 0,

        /// <summary>Usable, but the member name had to differ from the id for C# to accept it.</summary>
        Adjusted = 1,

        /// <summary>Not usable. Nothing is generated and the author has to change the id.</summary>
        Rejected = 2,
    }
}
