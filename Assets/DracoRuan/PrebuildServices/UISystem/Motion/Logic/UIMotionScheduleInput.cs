namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic.DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    public readonly struct UIMotionScheduleInput
    {
        public readonly UIMotionStartMode StartMode;
        public readonly float Offset;
        public readonly float Duration;

        public UIMotionScheduleInput(UIMotionStartMode startMode, float offset, float duration)
        {
            this.StartMode = startMode;
            this.Offset = offset;
            this.Duration = duration;
        }
    }
}
