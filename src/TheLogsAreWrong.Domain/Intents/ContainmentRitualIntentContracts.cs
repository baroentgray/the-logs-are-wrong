namespace TheLogsAreWrong.Domain.Intents;

/// <summary>Authoritative stage-2 action identifiers for the bounded containment-ritual intent family.</summary>
public static class ContainmentRitualIntentActions
{
    public static readonly IntentActionId StartContainmentRitual = IntentActionId.From("start_containment_ritual");
}

/// <summary>The one fixed global containment target accepted by the containment-ritual intent boundary.</summary>
public static class ContainmentRitualIntentTargets
{
    public static readonly TargetId Containment = TargetId.From("CONTAINMENT");
}
