namespace DracoRuan.PrebuildServices.DesignPatterns.Visitors.Core
{
    public interface IVisitor
    {
        public void Visit(IVisitable visitable);
    }
}
