using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;

namespace TheLogsAreWrong.Domain.Tests.Intents;

[Trait("Scope", "TLAW-039")]
public sealed class ConfirmationTestIntentContractsTests
{
    [Fact]
    public void Confirmation_start_action_uses_the_existing_parameterless_intent_contract()
    {
        Assert.Equal(IntentActionId.From("start_confirmation_test"), ConfirmationIntentActions.StartConfirmationTest);
        Assert.Same(NoIntentParameters.Instance, NoIntentParameters.Instance);
        Assert.Empty(typeof(NoIntentParameters).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
        Assert.Empty(typeof(NoIntentParameters).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
    }
}
