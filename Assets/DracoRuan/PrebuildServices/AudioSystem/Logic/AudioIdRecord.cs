namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>One declared audio id and whatever declared it.</summary>
    /// <remarks>
    /// The owner is carried alongside the id so a conflict can name both sides. A message that says
    /// only "duplicate id 'Click'" leaves the author hunting through the project for the other one.
    /// </remarks>
    public readonly struct AudioIdRecord
    {
        public string Id { get; }

        /// <summary>Where the id comes from, quoted verbatim in conflict messages.</summary>
        public string OwnerPath { get; }

        public AudioIdRecord(string id, string ownerPath)
        {
            this.Id = id;
            this.OwnerPath = ownerPath;
        }
    }
}
