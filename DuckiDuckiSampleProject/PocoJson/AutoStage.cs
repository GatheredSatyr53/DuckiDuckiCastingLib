using System.ComponentModel;
using DuckiDuckiCastingLib.Annotations;
using TTS.Standard.Json;

namespace TTS.Controllers.Auto;

[PocoPrototype(typeof(AutoStageProto))]
[FastJsonContract("auto_stage")]
[TypeConverter(typeof(ExpandableObjectConverter))]
public abstract class AutoStage
{
    [Browsable(false)]
    [FastJsonIgnore]
    public abstract string Caption { get; }

    public virtual AutoStageParameters? CreateParameters() => null;

    public abstract override string ToString();
}

[FastJsonContract("$stage_contract")]
public sealed class AutoStageProto
{
    public const string Contract = "$stage_contract";
    public const decimal DefaultScale = 1.25m;

    [PocoVariable("retry_limit")]
    public const int RetryLimit = 0;

    [PocoVariable("enabled")]
    public const bool Enabled = false;

    [PocoVariable("stage_mode")]
    public const StageMode Mode = StageMode.Standard;
}

public enum StageMode
{
    Standard,
    Extended
}

public sealed class AutoStageParameters;

public partial class ManualStage
{
    public override string ToString() => "Ручная реализация";
}

public abstract class LegacyStage
{
    public abstract string Caption { get; }
}
