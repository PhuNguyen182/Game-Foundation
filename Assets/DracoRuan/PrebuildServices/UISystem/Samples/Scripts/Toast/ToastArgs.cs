namespace DracoRuan.PrebuildServices.UISystem.Samples.Toast
{
    /// <summary>REWRITE_PLAN.md muc 5 buoc 9 sample: a self-closing, non-modal notification.
    /// The caller supplies the message per use, same one VM/prefab serves every toast.</summary>
    public readonly struct ToastArgs
    {
        public readonly string Message;

        public ToastArgs(string message)
        {
            this.Message = message;
        }
    }
}
