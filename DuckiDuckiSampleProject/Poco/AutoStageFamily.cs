using DuckiDuckiCastingLib.Annotations;

namespace TTS.Controllers.Auto;

[PocoFamily(typeof(AutoStage))]
internal enum AutoStageFamily
{
    [PocoValue("caption", "Термоцикл")]
    [PocoValue("label", "Этап термоцикла")]
    [PocoValue("stage_contract", "thermal_cycle")]
    [PocoValue("retry_limit", 3)]
    [PocoValue("enabled", true)]
    [PocoValue("stage_mode", StageMode.Extended)]
    ThermalCycleStage,

    [PocoValue("caption", "IncompleteStage")]
    [PocoValue("stage_contract", "incomplete")]
    [PocoValue("retry_limit", 2)]
    [PocoValue("enabled", false)]
    [PocoValue("stage_mode", StageMode.Standard)]
    IncompleteStage,

    [PocoValue("caption", "ManualStage")]
    [PocoValue("stage_contract", "manual")]
    [PocoValue("retry_limit", 1)]
    [PocoValue("enabled", true)]
    [PocoValue("stage_mode", StageMode.Standard)]
    ManualStage
}
