using System;
using DuckiDuckiCastingLib.Annotations;

namespace Zoo
{
    public interface INamed
    {
        string Name { get; }
    }

    [SupportsDuckCast]
    public partial interface IDuck : INamed
    {
        int Quacks { get; set; }
        string? Nickname { get; init; }
        void Quack(int times);
        string Describe(string prefix);
        long Weight(int scale);
        void Swim(in int depth);
        bool TryGet(string key, out int value);
        void Bump(ref int counter);
        T Convert<T>(T value) where T : struct;
        string this[int index] { get; }
        event EventHandler? Quacked;
        void Noise();
    }

    public class Robot
    {
        private readonly string[] _items = { "a", "b" };
        public string Name => "Robo";
        public int Quacks { get; set; }
        public string? Nickname { get; set; }
        public void Quack(int times) => Quacks += times;
        public string Describe(object prefix) => prefix + ":" + Name;
        public int Weight(int scale) => 42 * scale;
        public void Swim(int depth) { }
        public bool TryGet(string key, out int value) { value = key.Length; return true; }
        public void Bump(ref int counter) => counter++;
        public T Convert<T>(T value) where T : struct => value;
        public string this[int index] => _items[index];
        public event EventHandler? Quacked;
        public int Noise() => 1;
        public void Fire() => Quacked?.Invoke(this, EventArgs.Empty);
    }

    internal struct Toy
    {
        public string Name => "Toy";
        public int Quacks { get; set; }
        public string? Nickname;
        public void Quack(int times) => Quacks += times;
        public string Describe(string prefix) => prefix + "!";
        public long Weight(int scale) => scale;
        public void Swim(in int depth) { }
        public bool TryGet(string key, out int value) { value = 0; return false; }
        public void Bump(ref int counter) { }
        public T Convert<T>(T value) where T : struct => value;
        public string this[int index] => index.ToString();
        public event EventHandler? Quacked { add { } remove { } }
        public void Noise() { }
    }

    public static class Test
    {
        public static string Run()
        {
            var robot = new Robot();
            IDuck duck = IDuck.Cast(robot);
            IDuck again = IDuck.Cast(robot);
            IDuck toy = IDuck.Cast<Toy>(new Toy());
            IDuck self = IDuck.Cast(duck);

            duck.Quack(3);
            if (robot.Quacks != 3) return "quack not forwarded";
            if (duck.Name != "Robo") return "inherited property";
            if (duck.Describe("x") != "x:Robo") return "parameter conversion";
            if (duck.Weight(2) != 84) return "return conversion";
            int counter = 1;
            duck.Bump(ref counter);
            if (counter != 2) return "ref";
            if (!duck.TryGet("abc", out int length) || length != 3) return "out";
            if (duck.Convert(5) != 5) return "generic method";
            if (duck[1] != "b") return "indexer";
            int fired = 0;
            duck.Quacked += (_, _) => fired++;
            robot.Fire();
            if (fired != 1) return "event";
            duck.Quacks = 10;
            if (robot.Quacks != 10) return "setter";
            duck.Noise();
            if (!ReferenceEquals(self, duck)) return "already implemented";
            if (toy.Name != "Toy" || toy[7] != "7") return "struct source";
            toy.Quack(2);
            if (toy.Quacks != 2) return "struct copy mutation";
            if (again.GetType() != duck.GetType()) return "adapter reuse";

            var cast = typeof(IDuck).GetMethod("Cast")!.MakeGenericMethod(typeof(object));
            try
            {
                cast.Invoke(null, new object[] { new object() });
                return "missing adapter did not throw";
            }
            catch (System.Reflection.TargetInvocationException e) when (e.InnerException is InvalidCastException)
            {
            }

            try
            {
                IDuck.Cast<Robot>(null!);
                return "null did not throw";
            }
            catch (ArgumentNullException)
            {
            }

            return "ok";
        }
    }
}
