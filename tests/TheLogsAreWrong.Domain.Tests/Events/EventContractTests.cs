using System.Reflection;
using TheLogsAreWrong.Domain.Events;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Tests.Events;

public sealed class EventContractTests
{
    [Fact]
    public void Event_type_id_rejects_blank_values_and_preserves_value_equality()
    {
        Assert.Throws<ArgumentException>(() => EventTypeId.From(" "));
        Assert.True(EventTypeId.From("LOG_CREATED") == EventTypeId.From("LOG_CREATED"));
        Assert.True(default(EventTypeId).IsDefault);
    }

    [Fact]
    public void Envelope_allows_only_the_gate_zero_causation_field_and_marker_payloads()
    {
        var envelope = EventTestFixture.Event(1);

        Assert.Null(envelope.CausedByIntentId);
        Assert.IsAssignableFrom<IDomainEventPayload>(envelope.Payload);
        Assert.DoesNotContain(typeof(EventEnvelope).GetProperties(), property =>
            property.PropertyType == typeof(System.DateTime) ||
            property.PropertyType == typeof(System.DateTimeOffset) ||
            property.Name.Contains("correlation", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("trace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Envelope_has_value_equality_and_exact_gate_zero_field_names()
    {
        var payload = new TestEventPayload("payload");
        var first = new EventEnvelope
        {
            ShiftId = ShiftId.From("P0_SHIFT_A"),
            EventId = EventId.From("event_1"),
            Sequence = EventSequence.First,
            ServerTick = ServerTick.Zero,
            StateVersionAfter = StateVersion.Zero,
            EventType = EventTypeId.From("TEST_EVENT"),
            Payload = payload
        };
        var second = first with { };

        Assert.Equal(first, second);
        Assert.Equal(
            new[] { "ShiftId", "EventId", "Sequence", "CausedByIntentId", "ServerTick", "StateVersionAfter", "EventType", "Payload" },
            typeof(EventEnvelope).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(static property => property.Name));
    }

    [Fact]
    public void Rejection_contract_is_value_equal_has_all_twenty_reasons_and_has_no_sequence()
    {
        var rejection = new RejectionEvent
        {
            ShiftId = ShiftId.From("P0_SHIFT_A"),
            IntentId = IntentId.From("intent_1"),
            ServerTick = ServerTick.Zero,
            CurrentStateVersion = StateVersion.Zero,
            Reason = RejectionReason.STALE_STATE_VERSION
        };

        Assert.Equal(rejection, rejection with { });
        Assert.Equal(new[]
        {
            RejectionReason.SHIFT_MISMATCH,
            RejectionReason.ACTOR_NOT_BOUND,
            RejectionReason.STALE_STATE_VERSION,
            RejectionReason.TARGET_NOT_FOUND,
            RejectionReason.TARGET_NOT_IN_STATE,
            RejectionReason.TARGET_OCCUPIED,
            RejectionReason.MISSING_ITEM,
            RejectionReason.HOLD_NOT_COMPLETE,
            RejectionReason.FEED_ALREADY_PENDING,
            RejectionReason.FEED_GATE_OCCUPIED,
            RejectionReason.LINE_NOT_CLEAR,
            RejectionReason.BLOCKING_CONDITION_REMAINS,
            RejectionReason.NO_ACTIVE_REQUEST,
            RejectionReason.NO_MORE_LOGS,
            RejectionReason.MALFORMED_PROCEDURE_PARAMETERS,
            RejectionReason.PROCEDURE_HOLD_ACTIVE,
            RejectionReason.PROCEDURE_NO_PLAN,
            RejectionReason.PROCEDURE_OUT_OF_ORDER_ITEM,
            RejectionReason.PROCEDURE_REPEATED_STEP,
            RejectionReason.PROCEDURE_UNCONFIGURED_ITEM
        }, Enum.GetValues<RejectionReason>());
        Assert.DoesNotContain(typeof(RejectionEvent).GetProperties(), property => property.PropertyType == typeof(EventSequence));
    }
}
