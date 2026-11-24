namespace TTS.Standard.Json;

[AttributeUsage(AttributeTargets.Class)]
public sealed class FastJsonContractAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class FastJsonIgnoreAttribute : Attribute;
