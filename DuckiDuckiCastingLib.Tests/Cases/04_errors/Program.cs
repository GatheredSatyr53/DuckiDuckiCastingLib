using System;
using DuckiDuckiCastingLib.Annotations;

namespace Bad
{
    [SupportsDuckCast]
    public interface INotPartial { void M(); }

    public class NotPartialOuter
    {
        [SupportsDuckCast]
        public partial interface INested { void M(); }
    }

    [SupportsDuckCast]
    public partial interface IStaticAbstract { static abstract void S(); }

    [SupportsDuckCast]
    public partial interface IHasCast { void Cast(); }

    [SupportsDuckCast]
    public partial interface IRefReturn { ref int R(); }

    public partial class PrivateHolder
    {
        [SupportsDuckCast]
        private partial interface IPrivate { void M(); }
    }

    [SupportsDuckCast]
    public partial interface IGood
    {
        string Name { get; }
        void Go(int x);
    }

    public class Missing { public string Name => ""; }
    public class WrongSig { public string Name => ""; public void Go(string x) { } }
    public class Fine { public string Name => ""; public void Go(int x) { } }
    public class Hidden
    {
        private class Secret { public string Name => ""; public void Go(int x) { } }
        public static void Use() => IGood.Cast(new Secret());
    }

    public static class Test
    {
        public static void Run<T>(T value)
        {
            IGood a = IGood.Cast(new Missing());
            IGood b = IGood.Cast(new WrongSig());
            IGood c = IGood.Cast(value);
            IGood d = IGood.Cast(new { Name = "x" });
            IGood e = IGood.Cast(null);
            IGood f = IGood.Cast(new Fine());
        }
    }
}
