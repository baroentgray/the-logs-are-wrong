using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;

namespace TheLogsAreWrong.Domain.Configuration;

public sealed record ValidatedConfiguration(ShiftConfiguration Shift, AnomalyCatalog Anomalies);

public sealed record ShiftConfiguration(
    ShiftId ShiftId,
    ShiftSeed Seed,
    ImmutableDictionary<ProfileId, ShiftProfile> Profiles,
    ObjectivesDefinition Objectives,
    SupplyDefinition Supply,
    SchedulerConfiguration Scheduler,
    LineNoiseConfiguration LineNoise,
    ResourcesConfiguration Resources,
    ContainmentConfiguration Containment,
    ImmutableArray<string> SuccessPredicate,
    ImmutableArray<ManifestLogDefinition> Manifest);

public sealed record ShiftProfile(int IntakeTimeoutSeconds, int HardShiftDeadlineSeconds);
public sealed record ObjectivesDefinition(QuotaDefinition Quota, int MinCorrectlyProcessedAnomalies);
public sealed record QuotaDefinition(int Total, ImmutableDictionary<SpeciesId, int> BySpecies);
public sealed record SupplyDefinition(int Total, int FreeWriteoffBuffer);
public sealed record ManifestLogDefinition(LogId Id, SpeciesId TrueSpecies, SpeciesId DeclaredSpecies, AnomalyId? Anomaly);
public sealed record SchedulerConfiguration(
    ImmutableDictionary<NodeId, NodeCapacity> Capacities,
    int InitialAdmissionDelaySeconds,
    int NormalFeedDelaySeconds,
    int EarlyFeedDelaySeconds,
    int SawCycleSeconds,
    int RepairHoldSeconds,
    int MovementNoiseSeconds,
    string DefaultTimeoutRoute,
    ImmutableArray<HostTickStage> SameTickOrder);
public sealed record LineNoiseConfiguration(ImmutableArray<string> QuietWhenAllInactive, int PenitentConfirmRequiresContinuousQuietSeconds, bool ResetTestProgressWhenLoud, bool PauseIntakeTimerDuringTest);
public sealed record ResourcesConfiguration(ImmutableDictionary<ItemId, int> Consumable, ImmutableHashSet<ItemId> Reusable);
public sealed record ContainmentConfiguration(
    bool UnlimitedCapacity,
    int RitualHoldSeconds,
    int ServiceRequestedGraceSeconds,
    int OverdueSeconds,
    ImmutableDictionary<string, int> IntervalByDangerWeight,
    AfterSuccessfulRitualDefinition AfterSuccessfulRitual,
    PrototypeIncidentDefinition PrototypeIncident,
    PostFactoIntervalInferenceDefinition PostFactoIntervalInference);
public sealed record AfterSuccessfulRitualDefinition(ContainmentState ReturnState, bool RetainLogs, bool RetainDangerWeight, bool RestartIntervalFromCurrentWeight);
public sealed record PrototypeIncidentDefinition(string Type, int DurationSeconds, bool RemainsIncidentUntilRitual, bool RepeatBeforeResolution);
public sealed record PostFactoIntervalInferenceDefinition(bool Allowed, string Rationale, bool SeededJitter);

public sealed record AnomalyCatalog(ImmutableDictionary<AnomalyId, AnomalyDefinition> Definitions);
public sealed record AnomalyDefinition(
    AnomalyId Id,
    int DangerWeight,
    ImmutableArray<string> InstantClues,
    ImmutableArray<string> ObservedClues,
    ConfirmTestDefinition ConfirmTest,
    ProcessingDefinition Processing,
    ProcedureDefinition Procedure,
    ImmutableDictionary<ItemId, WrongActionDefinition> WrongActions);
public sealed record ConfirmTestDefinition(LineNoise? RequiredLineNoise, int DurationSeconds, bool Continuous, bool? ResetWhenConditionLost, string Result, ImmutableArray<ItemId> Tools);
public sealed record ProcessingDefinition(ImmutableHashSet<FlagId> RequiredFlags, RouteWithoutFlagsPolicy RouteWithoutFlags, ProcessingOutcome OnCorrect, ProcessingOutcome OnIncorrect);
public sealed record ProcessingOutcome(LogState TerminalState, QuotaCreditDefinition QuotaCredit, int CorrectAnomalyDelta, ImmutableArray<EffectDefinition> Effects);
public sealed record QuotaCreditDefinition(SpeciesCreditRule Species, int Units);
public sealed record EffectDefinition(EffectType Type, EffectEventId Event, int? DurationSeconds, string? Target);
public sealed record ProcedureDefinition(ImmutableArray<ProcedureStepDefinition> Steps, ImmutableHashSet<FlagId> GrantsFlags);
public sealed record ProcedureStepDefinition(ItemId Item, bool Consumes, int? HoldSeconds);
public sealed record WrongActionDefinition(bool LeavesStateUnchanged, LogState? TerminalState, bool Consumes, ImmutableArray<EffectDefinition> Effects);
