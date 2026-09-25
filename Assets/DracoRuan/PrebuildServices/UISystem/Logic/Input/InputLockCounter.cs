using System;

namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>
    /// Reference-counted input lock. Multiple concurrent transitions/loads can each
    /// Acquire a lock; input stays locked until every Acquire has a matching Release.
    /// </summary>
    public sealed class InputLockCounter
    {
        private int _count;

        public bool IsLocked => this._count > 0;
        public int Count => this._count;

        public void Acquire() => this._count++;

        public void Release()
        {
            if (this._count == 0)
                throw new InvalidOperationException("InputLockCounter.Release called without a matching Acquire.");

            this._count--;
        }
    }
}
