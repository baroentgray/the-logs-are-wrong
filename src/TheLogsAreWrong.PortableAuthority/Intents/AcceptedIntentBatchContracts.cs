using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Sequencing;

namespace TheLogsAreWrong.Domain.Intents;

/// <summary>Trusted adapter evidence for one intent accepted for consideration during one host tick.</summary>
public sealed class AuthoritativeAcceptedIntent
{
    public AuthoritativeAcceptedIntent(
        IntentEnvelope envelope,
        ActorId authoritativeActor,
        ServerTick receivedAtTick,
        ServerReceiveSequence receiveSequence)
    {
        if (envelope is null) { throw new ArgumentNullException("envelope"); }
        if (authoritativeActor.IsDefault)
        {
            throw new ArgumentException("Authoritative actor must be initialized.", nameof(authoritativeActor));
        }

        if (receivedAtTick.IsDefault)
        {
            throw new ArgumentException("Received tick must be initialized.", nameof(receivedAtTick));
        }

        if (receiveSequence.IsDefault)
        {
            throw new ArgumentException("Receive sequence must be initialized.", nameof(receiveSequence));
        }

        Envelope = envelope;
        AuthoritativeActor = authoritativeActor;
        ReceivedAtTick = receivedAtTick;
        ReceiveSequence = receiveSequence;
    }

    public IntentEnvelope Envelope { get; }
    public ActorId AuthoritativeActor { get; }
    public ServerTick ReceivedAtTick { get; }
    public ServerReceiveSequence ReceiveSequence { get; }
}

/// <summary>Immutable, complete ordered accepted-intent evidence for one exact host tick.</summary>
public sealed class AcceptedIntentTickBatch
{
    internal AcceptedIntentTickBatch(
        ShiftId shiftId,
        ServerTick currentTick,
        ImmutableArray<AuthoritativeAcceptedIntent> intents)
    {
        ShiftId = shiftId;
        CurrentTick = currentTick;
        Intents = intents;
    }

    public ShiftId ShiftId { get; }
    public ServerTick CurrentTick { get; }
    public ImmutableArray<AuthoritativeAcceptedIntent> Intents { get; }
}

/// <summary>Validates and deterministically orders adapter-accepted intent evidence for one host tick.</summary>
public static class AcceptedIntentTickBatchFactory
{
    public static AcceptedIntentTickBatch Create(
        ShiftId shiftId,
        ServerTick currentTick,
        IEnumerable<AuthoritativeAcceptedIntent> acceptedIntents)
    {
        if (shiftId.IsDefault)
        {
            throw new ArgumentException("Shift identifier must be initialized.", nameof(shiftId));
        }

        if (currentTick.IsDefault)
        {
            throw new ArgumentException("Current tick must be initialized.", nameof(currentTick));
        }

        if (acceptedIntents is null) { throw new ArgumentNullException("acceptedIntents"); }

        var ordered = acceptedIntents.ToArray();
        var intentIds = new HashSet<IntentId>();
        var receiveSequences = new HashSet<ServerReceiveSequence>();
        foreach (var acceptedIntent in ordered)
        {
            if (acceptedIntent is null)
            {
                throw new ArgumentException("Accepted intent collection cannot contain null evidence.", nameof(acceptedIntents));
            }

            if (acceptedIntent.Envelope is null || acceptedIntent.Envelope.ShiftId != shiftId)
            {
                throw new ArgumentException("Every accepted envelope must belong to the batch shift.", nameof(acceptedIntents));
            }

            if (acceptedIntent.ReceivedAtTick != currentTick)
            {
                throw new ArgumentException("Every accepted intent must have been received at the batch tick.", nameof(acceptedIntents));
            }

            if (acceptedIntent.AuthoritativeActor.IsDefault || acceptedIntent.ReceiveSequence.IsDefault)
            {
                throw new ArgumentException("Accepted intent evidence must be initialized.", nameof(acceptedIntents));
            }

            if (!intentIds.Add(acceptedIntent.Envelope.IntentId))
            {
                throw new ArgumentException("Accepted intent identities must be unique within a batch.", nameof(acceptedIntents));
            }

            if (!receiveSequences.Add(acceptedIntent.ReceiveSequence))
            {
                throw new ArgumentException("Receive sequences must be unique within a batch.", nameof(acceptedIntents));
            }
        }

        Array.Sort(ordered, static (left, right) => left.ReceiveSequence.CompareTo(right.ReceiveSequence));
        ValidateContiguousSequences(ordered);
        return new AcceptedIntentTickBatch(shiftId, currentTick, ImmutableArray.CreateRange(ordered));
    }

    private static void ValidateContiguousSequences(AuthoritativeAcceptedIntent[] ordered)
    {
        if (ordered.Length == 0)
        {
            return;
        }

        if (ordered[0].ReceiveSequence != ServerReceiveSequence.Zero)
        {
            throw new ArgumentException("A non-empty accepted-intent batch must begin at receive sequence zero.", nameof(ordered));
        }

        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].ReceiveSequence != NextSequence(ordered[index - 1].ReceiveSequence))
            {
                throw new ArgumentException("Receive sequences must be contiguous checked successors.", nameof(ordered));
            }
        }
    }

    private static ServerReceiveSequence NextSequence(ServerReceiveSequence sequence) => sequence.Next();
}
