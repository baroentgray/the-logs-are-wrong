using TheLogsAreWrong.Domain.Identifiers;

namespace TheLogsAreWrong.Domain.Intents;

/// <summary>Typed player-action identifiers owned by the existing procedure lifecycle.</summary>
public static class ProcedureIntentActions
{
    public static readonly IntentActionId StartProcedureAction = IntentActionId.From("start_procedure_action");
}

/// <summary>One immutable procedure-action request parameter; the envelope target remains the log target.</summary>
public sealed record ProcedureActionIntentParameters : IIntentParameters
{
    public ProcedureActionIntentParameters(ItemId attemptedItem)
    {
        if (attemptedItem.IsDefault)
        {
            throw new ArgumentException("Procedure action parameters require an initialized attempted item.", nameof(attemptedItem));
        }

        AttemptedItem = attemptedItem;
    }

    public ItemId AttemptedItem { get; }
}
