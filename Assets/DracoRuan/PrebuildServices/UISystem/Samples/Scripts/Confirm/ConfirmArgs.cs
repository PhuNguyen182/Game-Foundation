namespace DracoRuan.PrebuildServices.UISystem.Samples.Confirm
{
    /// <summary>What the Confirm popup shows - the caller supplies its own copy per use
    /// (e.g. "Delete this item?" vs "Quit the game?"), so one popup/VM serves every confirm
    /// dialog in the game instead of one per message.</summary>
    public readonly struct ConfirmArgs
    {
        public readonly string Title;
        public readonly string Message;

        public ConfirmArgs(string title, string message)
        {
            this.Title = title;
            this.Message = message;
        }
    }
}
