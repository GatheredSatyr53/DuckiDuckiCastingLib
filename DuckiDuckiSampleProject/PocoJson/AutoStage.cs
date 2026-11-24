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
public sealed class AutoStageProto;

public sealed class AutoStageParameters;

public partial class ManualStage
{
    public override string ToString() => "Ручная реализация";
}

public abstract class LegacyStage
{
    public abstract string Caption { get; }
}
