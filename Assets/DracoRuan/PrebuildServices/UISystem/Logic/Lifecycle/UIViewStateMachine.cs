namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>
    /// Guards a view's Hidden -> Showing -> Shown -> Hiding -> Hidden cycle against
    /// re-entrance: e.g. Show can't start again while already Showing/Shown, and Hide
    /// can't start while a Show hasn't completed yet (the bug class that let a stale
    /// Hide continuation run underneath a fresh Show in the old UISystem).
    /// </summary>
    public sealed class UIViewStateMachine
    {
        public UIViewState State { get; private set; } = UIViewState.Hidden;

        public bool TryBeginShow()
        {
            if (this.State != UIViewState.Hidden)
                return false;

            this.State = UIViewState.Showing;
            return true;
        }

        public bool TryCompleteShow()
        {
            if (this.State != UIViewState.Showing)
                return false;

            this.State = UIViewState.Shown;
            return true;
        }

        public bool TryBeginHide()
        {
            if (this.State != UIViewState.Shown)
                return false;

            this.State = UIViewState.Hiding;
            return true;
        }

        public bool TryCompleteHide()
        {
            if (this.State != UIViewState.Hiding)
                return false;

            this.State = UIViewState.Hidden;
            return true;
        }
    }
}
