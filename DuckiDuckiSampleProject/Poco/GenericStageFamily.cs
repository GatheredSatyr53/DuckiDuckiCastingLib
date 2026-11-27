using DuckiDuckiCastingLib.Annotations;
using TTS.Standard.Json;

namespace DuckiDuckiSampleProject.GenericStages;

[PocoPrototype(typeof(StageContract))]
public abstract class AutoStage
{
    public abstract string Caption { get; }
    public virtual AutoStageParameters? CreateParameters() => null;

    [PocoVariable("label")]
    public abstract override string ToString();
}

public interface IAutoStage
{
    [PocoVariable("caption")]
    static abstract string StageCaption { get; }
}

public abstract class AutoStage<TStage> : AutoStage
    where TStage : AutoStage<TStage>, IAutoStage
{
    public override string Caption => TStage.StageCaption;
}

public abstract class AutoStage<TStage, TParameters> : AutoStage<TStage>
    where TStage : AutoStage<TStage, TParameters>, IAutoStage
    where TParameters : AutoStageParameters, new()
{
    public override AutoStageParameters CreateParameters() => new TParameters();
}

public class AutoStageParameters;
public sealed class PkiMeasurementStageParameters : AutoStageParameters;
public sealed class CombustionStageParameters : AutoStageParameters;

[FastJsonContract("$contract")]
internal sealed class StageContract;

[PocoFamily(typeof(AutoStage), Interface = typeof(IAutoStage), Sealed = true)]
internal enum StageFamily
{
    [PocoBase(typeof(AutoStage<>))]
    [PocoValue("contract", "breakdown_stage")]
    [PocoValue("caption", "Пробой")]
    [PocoValue("label", "Измерение пробоя")]
    BreakdownStage,

    [PocoBase(typeof(AutoStage<>))]
    [PocoValue("contract", "rotation_signal_stage")]
    [PocoValue("caption", "Сигналы вращения")]
    [PocoValue("label", "Съём сигналов вращения (AB)")]
    RotationSignalStage,

    [PocoBase(typeof(AutoStage<,>), typeof(PkiMeasurementStageParameters))]
    [PocoValue("contract", "pki_measurement_stage")]
    [PocoValue("caption", "Съём данных с ПКИ")]
    [PocoValue("label", "Съём данных с ПКИ")]
    PkiMeasurementStage,

    [PocoBase(typeof(AutoStage<,>), typeof(CombustionStageParameters))]
    [PocoValue("contract", "combustion_stage")]
    [PocoValue("caption", "Напряжение горения")]
    [PocoValue("label", "Съём напряжения горения")]
    CombustionStage
}

internal static class GenericStageExample
{
    internal static void Run()
    {
        Check(new BreakdownStage(), "Пробой", "Измерение пробоя", "breakdown_stage", null);
        Check(new RotationSignalStage(), "Сигналы вращения", "Съём сигналов вращения (AB)", "rotation_signal_stage", null);
        Check(new PkiMeasurementStage(), "Съём данных с ПКИ", "Съём данных с ПКИ",
            "pki_measurement_stage", typeof(PkiMeasurementStageParameters));
        Check(new CombustionStage(), "Напряжение горения", "Съём напряжения горения",
            "combustion_stage", typeof(CombustionStageParameters));
    }

    private static void Check<T>(T stage, string caption, string label, string contract, Type? parameterType)
        where T : AutoStage, IAutoStage
    {
        var type = typeof(T);
        var attribute = (FastJsonContractAttribute?)Attribute.GetCustomAttribute(type, typeof(FastJsonContractAttribute));
        if (!type.IsSealed || stage.Caption != caption || T.StageCaption != caption || stage.ToString() != label ||
            stage.CreateParameters()?.GetType() != parameterType || attribute?.Name != contract)
            throw new InvalidOperationException($"Unexpected generated behavior for {type.Name}.");
    }
}
