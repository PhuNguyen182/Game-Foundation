namespace DracoRuan.CoreSystems.DesignPatterns.Visitors.Core
{
    public interface IVisitable
    {
        public void Accept(IVisitor visitor);
    }
}
