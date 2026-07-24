using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Intents;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Intents;

[Trait("Scope", "TLAW-003")]
public sealed class IntentContractTests
{
    [Fact]
    public void Target_and_action_identifiers_preserve_exact_values_and_detect_defaults()
    {
        var target = TargetId.From(" log_01 ");
        var action = IntentActionId.From("route_to_procedure");

        Assert.Equal(" log_01 ", target.Value);
        Assert.Equal("route_to_procedure", action.Value);
        Assert.NotEqual(TargetId.From("log_01"), TargetId.From("LOG_01"));
        Assert.True(default(TargetId).IsDefault);
        Assert.True(default(IntentActionId).IsDefault);
        Assert.Throws<ArgumentException>(() => TargetId.From("   "));
        Assert.Throws<ArgumentException>(() => IntentActionId.From(string.Empty));
    }

    [Fact]
    public void Routing_actions_are_exact_frozen_discriminators()
    {
        Assert.Equal("route_to_procedure", LogIntentActions.RouteToProcedure.Value);
        Assert.Equal("return_from_procedure", LogIntentActions.ReturnFromProcedure.Value);
        Assert.Equal("route_to_saw_queue", LogIntentActions.RouteToSawQueue.Value);
        Assert.Equal("write_off", LogIntentActions.WriteOff.Value);
    }

    [Fact]
    public void Intent_envelope_retains_the_frozen_field_list_without_trusting_the_hint()
    {
        var parameters = new TestIntentParameters("opaque");
        var envelope = new IntentEnvelope(
            ShiftId.From("P0_SHIFT_A"),
            IntentId.From("intent_01"),
            ActorId.From("untrusted_actor"),
            TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure,
            StateVersion.Zero,
            ServerTick.Zero,
            parameters);

        Assert.Equal("P0_SHIFT_A", envelope.ShiftId.Value);
        Assert.Equal("intent_01", envelope.IntentId.Value);
        Assert.Equal("untrusted_actor", envelope.ActorIdHint.Value);
        Assert.Equal("log_01", envelope.TargetId.Value);
        Assert.Equal(LogIntentActions.RouteToProcedure, envelope.Action);
        Assert.Equal(StateVersion.Zero, envelope.ExpectedStateVersion);
        Assert.Equal(ServerTick.Zero, envelope.ClientObservedTick);
        Assert.Same(parameters, envelope.Parameters);
    }

    [Fact]
    public void No_intent_parameters_is_an_immutable_singleton()
    {
        Assert.Same(NoIntentParameters.Instance, NoIntentParameters.Instance);
        Assert.IsAssignableFrom<IIntentParameters>(NoIntentParameters.Instance);
    }

    [Fact]
    public void Intent_envelope_rejects_programmer_default_values_loudly()
    {
        Assert.Throws<ArgumentException>(() => new IntentEnvelope(
            default,
            IntentId.From("intent_01"),
            ActorId.From("actor_01"),
            TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure,
            StateVersion.Zero,
            ServerTick.Zero,
            NoIntentParameters.Instance));

        Assert.Throws<ArgumentException>(() => new IntentEnvelope(
            ShiftId.From("P0_SHIFT_A"),
            IntentId.From("intent_01"),
            ActorId.From("actor_01"),
            TargetId.From("log_01"),
            LogIntentActions.RouteToProcedure,
            default,
            ServerTick.Zero,
            NoIntentParameters.Instance));
    }

    private sealed record TestIntentParameters(string Value) : IIntentParameters;
}
