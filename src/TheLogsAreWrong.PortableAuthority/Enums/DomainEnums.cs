namespace TheLogsAreWrong.Domain.Enums;

public enum LogState { SCHEDULED, AT_FEED_GATE, AT_INTAKE, AT_PROCEDURE, QUEUED_FOR_SAW, IN_SAW, PROCESSED, HELD_WRITTEN_OFF }
public enum LineNoise { QUIET, LOUD }
public enum LineState { LINE_CLEAR, LINE_JAMMED, REPAIRING }
public enum JamCause { FEED_GATE_BLOCKED = 1, INTAKE_AUTOFEED_BLOCKED = 2 }
public enum ContainmentState { STABLE, SERVICE_REQUESTED, OVERDUE, INCIDENT }
public enum NodeId { SUPPLY_QUEUE, FEED_GATE, INTAKE, PROCEDURE, SAW_QUEUE, SAW, CONTAINMENT }
public enum HostTickStage { hold_and_procedure_completions, accepted_intents_by_server_receive_sequence, deadline_expirations, saw_transitions, feed_and_auto_routes, derived_states, event_emission }
public enum EffectType { time_penalty, @lock, miscredit }
public enum SpeciesCreditRule { true_species, declared_species, none }
public enum RouteWithoutFlagsPolicy { allowed }
