namespace TheLogsAreWrong.Domain.Intents;

/// <summary>Authoritative stage-2 action identifiers for the bounded line-repair intent family.</summary>
public static class LineRepairIntentActions
{
    public static readonly IntentActionId StartLineRepair = IntentActionId.From("start_line_repair");
}

/// <summary>The one fixed global line target accepted by the line-repair intent boundary.</summary>
public static class LineRepairIntentTargets
{
    public static readonly TargetId Line = TargetId.From("LINE");
}
