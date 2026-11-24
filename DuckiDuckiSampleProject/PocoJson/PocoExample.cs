using System.Reflection;
using TTS.Controllers.Auto;
using TTS.Standard.Json;

namespace DuckiDuckiSampleProject;

internal static class PocoExample
{
    internal static void Run()
    {
        AutoStage stage = new ThermalCycleStage();
        if (stage.Caption != "Термоцикл" || stage.ToString() != "Этап термоцикла" ||
            stage.CreateParameters() is not null ||
            stage.GetType().GetCustomAttribute<FastJsonContractAttribute>(inherit: false)?.Name != "thermal_cycle")
        {
            throw new InvalidOperationException("Unexpected generated stage.");
        }

        if (!typeof(IncompleteStage).IsAbstract || new ManualStage().ToString() != "Ручная реализация")
            throw new InvalidOperationException("Unexpected partial class behavior.");

        if (typeof(ManualStage).GetCustomAttribute<FastJsonContractAttribute>(inherit: false)?.Name != "manual" ||
            typeof(LegacyJsonStage).GetCustomAttribute<FastJsonContractAttribute>(inherit: false)?.Name != "legacy_json")
        {
            throw new InvalidOperationException("Unexpected prototype selection.");
        }

        Console.WriteLine("OK");
    }
}
