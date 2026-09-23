namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>One declared vibration id and whatever declared it.</summary>
    /// <remarks>
    /// The owner is carried alongside the id so a conflict can name both sides. A message that says
    /// only "duplicate id 'ButtonTap'" leaves the author hunting through the project for the other
    /// one.
    /// </remarks>
    public readonly struct VibrationIdRecord
    {
        public string Id { get; }

        /// <summary>Where the id comes from, quoted verbatim in conflict messages.</summary>
        public string OwnerPath { get; }

        public VibrationIdRecord(string id, string ownerPath)
        {
            this.Id = id;
            this.OwnerPath = ownerPath;
        }
    }
}
