namespace App
{
    public class Circle
    {
        public double Area() => 3.14;
    }

    public static class Test
    {
        public static string Run()
        {
            var square = Shared.IShape.Cast(new Shared.Square());
            if (square.Area() != 4) return "adapter from the declaring assembly";
            try
            {
                Shared.IShape.Cast(new Circle());
                return "foreign type did not throw";
            }
            catch (System.InvalidCastException)
            {
            }

            return "ok";
        }
    }
}
