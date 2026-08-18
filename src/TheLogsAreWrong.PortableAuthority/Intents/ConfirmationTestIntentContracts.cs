namespace TheLogsAreWrong.Domain.Intents;

/// <summary>Authoritative stage-2 action identifiers for the bounded confirmation-test intent family.</summary>
public static class ConfirmationIntentActions
{
    public static readonly IntentActionId StartConfirmationTest = IntentActionId.From("start_confirmation_test");
}
