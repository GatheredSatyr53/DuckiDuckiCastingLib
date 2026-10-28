using System.Collections.Generic;
using DuckiDuckiCastingLib.Annotations;

namespace Store
{
    [SupportsDuckCast]
    public partial interface IRepository<T> where T : class
    {
        T? Find(int id);
        void Add(T item);
        int Count { get; }
    }

    [SupportsDuckCast]
    public partial interface IReader<out T>
    {
        T Read();
    }

    [SupportsDuckCast]
    public partial interface IWriter<in T>
    {
        void Write(T value);
    }

    public class User
    {
        public string Name = "";
    }

    public class UserRepo
    {
        private readonly List<User> _users = new();
        public User? Find(int id) => id < _users.Count ? _users[id] : null;
        public void Add(User item) => _users.Add(item);
        public int Count => _users.Count;
    }

    public class StringRepo
    {
        public string? Find(int id) => id.ToString();
        public void Add(string item) { }
        public int Count => 7;
    }

    public class ConstReader
    {
        public int Read() => 99;
    }

    public class Sink
    {
        public List<object> Items = new();
        public void Write(object value) => Items.Add(value);
    }

    public static class Test
    {
        public static string Run()
        {
            IRepository<User> users = IRepository<User>.Cast(new UserRepo());
            users.Add(new User { Name = "ann" });
            if (users.Count != 1 || users.Find(0)?.Name != "ann") return "users";

            IRepository<string> strings = IRepository<string>.Cast(new StringRepo());
            if (strings.Count != 7 || strings.Find(5) != "5") return "strings";

            IReader<int> reader = IReader<int>.Cast(new ConstReader());
            if (reader.Read() != 99) return "reader";

            IReader<object> boxed = IReader<object>.Cast(new ConstReader());
            if (!Equals(boxed.Read(), 99)) return "boxed reader";

            var sink = new Sink();
            IWriter<string> writer = IWriter<string>.Cast(sink);
            writer.Write("hello");
            if (sink.Items.Count != 1) return "writer";

            // Same source type, different type argument: must pick the right adapter at run time.
            if (IReader<int>.Cast(new ConstReader()).GetType() == IReader<object>.Cast(new ConstReader()).GetType()) return "adapter per type argument";
            return "ok";
        }
    }
}
