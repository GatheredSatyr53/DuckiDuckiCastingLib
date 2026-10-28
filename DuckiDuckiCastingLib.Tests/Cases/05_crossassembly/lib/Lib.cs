using DuckiDuckiCastingLib.Annotations;

namespace Shared
{
    [SupportsDuckCast]
    public partial interface IShape
    {
        double Area();
    }

    public class Square
    {
        public double Area() => 4;
    }

    public static class LibUse
    {
        public static IShape Make() => IShape.Cast(new Square());
    }
}
