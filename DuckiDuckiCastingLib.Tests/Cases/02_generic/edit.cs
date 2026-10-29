namespace Store
{
    public class OtherReader
    {
        public string Read() => "x";
    }

    public static class Extra
    {
        public static IReader<string> Make() => IReader<string>.Cast(new OtherReader());
    }
}
