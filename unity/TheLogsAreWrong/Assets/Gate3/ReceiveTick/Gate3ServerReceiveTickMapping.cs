using System;
using TheLogsAreWrong.Domain.Primitives;
using TheLogsAreWrong.Domain.Runtime;
using TheLogsAreWrong.Gate2;

namespace TheLogsAreWrong.Gate3
{
    /// <summary>Bounded result of observing the authoritative host receive tick for a future Gate-3 ingress.</summary>
    public enum Gate3ServerReceiveTickObservationStatus
    {
        Observed,
        OwnerNotRunning,
        ClockFaulted
    }

    /// <summary>Contains an exact receive tick only when server-owned elapsed-time observation succeeded.</summary>
    public readonly struct Gate3ServerReceiveTickObservation
    {
        private Gate3ServerReceiveTickObservation(Gate3ServerReceiveTickObservationStatus status, ServerTick receiveTick)
        {
            Status = status;
            ReceiveTick = receiveTick;
        }

        public Gate3ServerReceiveTickObservationStatus Status { get; }
        public ServerTick ReceiveTick { get; }
        public bool HasReceiveTick => Status == Gate3ServerReceiveTickObservationStatus.Observed;

        internal static Gate3ServerReceiveTickObservation Observed(ServerTick receiveTick)
        {
            if (receiveTick.IsDefault)
            {
                throw new ArgumentException("An observed receive tick must be initialized.", nameof(receiveTick));
            }

            return new Gate3ServerReceiveTickObservation(Gate3ServerReceiveTickObservationStatus.Observed, receiveTick);
        }

        internal static Gate3ServerReceiveTickObservation Rejected(Gate3ServerReceiveTickObservationStatus status)
        {
            if (status == Gate3ServerReceiveTickObservationStatus.Observed)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new Gate3ServerReceiveTickObservation(status, default);
        }
    }

    /// <summary>
    /// Session-scoped server-only observation source. It observes the exact same elapsed-time bridge consumed by
    /// cadence; it never samples a cadence delta or reaches gameplay state.
    /// </summary>
    public sealed class Gate3ServerReceiveTickObservationSource
    {
        private readonly IAuthoritativeElapsedTimeSource _elapsedTimeSource;

        public Gate3ServerReceiveTickObservationSource(IAuthoritativeElapsedTimeSource elapsedTimeSource)
        {
            _elapsedTimeSource = elapsedTimeSource ?? throw new ArgumentNullException(nameof(elapsedTimeSource));
        }

        public ServerTick ObserveReceiveTick()
        {
            return Gate3ServerReceiveTickMapper.Map(_elapsedTimeSource.ObserveElapsedMilliseconds());
        }
    }

    /// <summary>Pure inclusive-deadline mapping from exact authoritative elapsed milliseconds to one receive tick.</summary>
    public static class Gate3ServerReceiveTickMapper
    {
        public const long MillisecondsPerServerTick = 1000;

        public static ServerTick Map(AuthoritativeElapsedMilliseconds elapsedMilliseconds)
        {
            if (elapsedMilliseconds.Value == 0)
            {
                return ServerTick.Zero;
            }

            return ServerTick.From(checked((elapsedMilliseconds.Value - 1) / MillisecondsPerServerTick));
        }
    }
}
