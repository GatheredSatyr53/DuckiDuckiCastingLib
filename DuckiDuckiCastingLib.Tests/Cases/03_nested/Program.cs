using DuckiDuckiCastingLib.Annotations;

public partial class Outer<TOuter>
{
    public partial struct Inner
    {
        [SupportsDuckCast]
        internal partial interface IThing
        {
            string Tag { get; }
            TOuter Value { get; }
        }
    }
}

public class Tagged
{
    public string Tag => "t";
    public int Value => 5;
}

namespace Deep.Nest
{
    public partial record Holder
    {
        [SupportsDuckCast]
        public partial interface IGreeter
        {
            string Greet(string who);
        }
    }

    public class Greeter
    {
        public string Greet(string who) => "hi " + who;
    }
}

public static class Test
{
    public static string Run()
    {
        Outer<int>.Inner.IThing thing = Outer<int>.Inner.IThing.Cast(new Tagged());
        if (thing.Tag != "t" || thing.Value != 5) return "nested in generic outer";
        var greeter = Deep.Nest.Holder.IGreeter.Cast(new Deep.Nest.Greeter());
        if (greeter.Greet("bob") != "hi bob") return "nested in record";
        return "ok";
    }
}
