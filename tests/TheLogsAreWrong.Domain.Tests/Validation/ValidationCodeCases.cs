using System.Collections.Immutable;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;

namespace TheLogsAreWrong.Domain.Tests.Validation;

public sealed record ValidationCase(
    string Code,
    ConfigurationDocument Document,
    string Path,
    bool IsSuccess,
    DiagnosticSeverity Severity,
    Func<YamlInputs, YamlInputs> Mutate);

public sealed record YamlInputs(string Shift, string Anomalies);

public static class ValidationCodeCases
{
    // This is contract-owned test data. It deliberately does not derive its expected set from production code.
    public static ImmutableHashSet<string> ApprovedCodes { get; } = ImmutableHashSet.Create(StringComparer.Ordinal,
        "TLAW-CFG-001", "TLAW-CFG-002", "TLAW-CFG-003", "TLAW-CFG-004", "TLAW-CFG-005", "TLAW-CFG-006", "TLAW-CFG-007", "TLAW-CFG-008", "TLAW-CFG-009", "TLAW-CFG-010", "TLAW-CFG-011", "TLAW-CFG-012", "TLAW-CFG-013", "TLAW-CFG-014",
        "TLAW-CFG-101", "TLAW-CFG-102", "TLAW-CFG-103", "TLAW-CFG-104", "TLAW-CFG-105", "TLAW-CFG-106", "TLAW-CFG-107", "TLAW-CFG-108", "TLAW-CFG-109", "TLAW-CFG-110", "TLAW-CFG-111", "TLAW-CFG-112", "TLAW-CFG-113", "TLAW-CFG-114", "TLAW-CFG-115", "TLAW-CFG-116", "TLAW-CFG-117", "TLAW-CFG-118", "TLAW-CFG-119", "TLAW-CFG-120", "TLAW-CFG-121", "TLAW-CFG-122", "TLAW-CFG-123", "TLAW-CFG-124", "TLAW-CFG-125", "TLAW-CFG-126", "TLAW-CFG-127", "TLAW-CFG-128", "TLAW-CFG-129", "TLAW-CFG-130", "TLAW-CFG-131", "TLAW-CFG-132", "TLAW-CFG-133", "TLAW-CFG-134", "TLAW-CFG-135", "TLAW-CFG-136", "TLAW-CFG-137", "TLAW-CFG-138", "TLAW-CFG-139", "TLAW-CFG-140", "TLAW-CFG-141", "TLAW-CFG-142", "TLAW-CFG-143", "TLAW-CFG-144", "TLAW-CFG-145", "TLAW-CFG-146", "TLAW-CFG-147", "TLAW-CFG-148", "TLAW-CFG-149", "TLAW-CFG-150", "TLAW-CFG-151",
        "TLAW-CFG-201", "TLAW-CFG-202", "TLAW-CFG-203", "TLAW-CFG-204", "TLAW-CFG-205", "TLAW-CFG-206", "TLAW-CFG-207", "TLAW-CFG-208", "TLAW-CFG-209", "TLAW-CFG-210", "TLAW-CFG-211", "TLAW-CFG-212", "TLAW-CFG-213", "TLAW-CFG-214", "TLAW-CFG-215", "TLAW-CFG-216", "TLAW-CFG-217", "TLAW-CFG-218", "TLAW-CFG-219", "TLAW-CFG-220", "TLAW-CFG-221", "TLAW-CFG-222", "TLAW-CFG-223", "TLAW-CFG-224", "TLAW-CFG-225", "TLAW-CFG-226", "TLAW-CFG-227", "TLAW-CFG-228", "TLAW-CFG-229", "TLAW-CFG-230", "TLAW-CFG-231", "TLAW-CFG-232", "TLAW-CFG-233", "TLAW-CFG-234", "TLAW-CFG-235", "TLAW-CFG-236",
        "TLAW-CFG-301", "TLAW-CFG-302", "TLAW-CFG-303", "TLAW-CFG-304", "TLAW-CFG-305", "TLAW-CFG-306", "TLAW-CFG-307", "TLAW-CFG-308", "TLAW-CFG-309", "TLAW-CFG-310");

    public static ImmutableArray<ValidationCase> Cases { get; } =
    [
        Shift("TLAW-CFG-001", "(document)", static input => input with { Shift = "schema_version: [" }),
        Anomalies("TLAW-CFG-002", "(document)", static input => input with { Anomalies = "schema_version: [" }),
        Shift("TLAW-CFG-003", "schema_version", static input => input with { Shift = WithoutFirstLine(input.Shift) }),
        Shift("TLAW-CFG-004", "schema_version", static input => input with { Shift = Once(input.Shift, "schema_version: 2", "schema_version: 3") }),
        Shift("TLAW-CFG-005", "seed", static input => input with { Shift = Once(input.Shift, "seed: 47001", "seed: 47001\nseed: 47002") }),
        Shift("TLAW-CFG-006", "bogus_key", static input => input with { Shift = Once(input.Shift, "seed: 47001", "seed: 47001\nbogus_key: 1") }),
        Shift("TLAW-CFG-007", "supply.total", static input => input with { Shift = Once(input.Shift, "supply:\n  total: 12\n", "supply:\n") }),
        Shift("TLAW-CFG-008", "seed", static input => input with { Shift = Once(input.Shift, "seed: 47001", "seed: forty") }),
        Shift("TLAW-CFG-009", "(node)", static input => input with { Shift = Once(input.Shift, "shift_id: P0_SHIFT_A", "shift_id: &shift P0_SHIFT_A") }),
        Shift("TLAW-CFG-010", "(stream)", static input => input with { Shift = input.Shift + "\n---\nschema_version: 2\n" }),
        Shift("TLAW-CFG-011", "(document)", static input => input with { Shift = string.Empty }),
        Shift("TLAW-CFG-012", "shift_id", static input => input with { Shift = Once(input.Shift, "shift_id: P0_SHIFT_A", "shift_id: \" \"") }),
        Shift("TLAW-CFG-013", "shift_id", static input => input with { Shift = Once(input.Shift, "shift_id: P0_SHIFT_A", "shift_id: \" P0_SHIFT_A \"") }),
        Shift("TLAW-CFG-014", "(root)", static input => new YamlInputs(input.Anomalies, input.Shift)),

        Shift("TLAW-CFG-101", "shift_id", static input => input with { Shift = Once(input.Shift, "shift_id: P0_SHIFT_A", BlankScalar) }),
        Shift("TLAW-CFG-102", "seed", static input => input with { Shift = Once(input.Shift, "seed: 47001\n", string.Empty) }),
        Shift("TLAW-CFG-103", "profiles", static input => input with { Shift = ReplaceBlock(input.Shift, "profiles:\n", "objectives:\n", "profiles: {}\n") }),
        Shift("TLAW-CFG-104", "profiles.learning.intake_timeout_seconds", static input => input with { Shift = Once(input.Shift, "intake_timeout_seconds: 60", "intake_timeout_seconds: 0") }),
        Shift("TLAW-CFG-105", "profiles.learning.hard_shift_deadline_seconds", static input => input with { Shift = Once(input.Shift, "hard_shift_deadline_seconds: 840", "hard_shift_deadline_seconds: -1") }),
        Shift("TLAW-CFG-106", "profiles.learning", static input => input with { Shift = Once(input.Shift, "hard_shift_deadline_seconds: 840", "hard_shift_deadline_seconds: 10") }, true),
        Shift("TLAW-CFG-107", "objectives.quota.total", static input => input with { Shift = Once(input.Shift, "total: 9", "total: 0") }),
        Shift("TLAW-CFG-108", "objectives.quota.by_species", static input => input with { Shift = Once(input.Shift, "by_species:\n      pine: 5\n      oak: 4", "by_species: {}") }),
        Shift("TLAW-CFG-109", "objectives.quota.by_species.pine", static input => input with { Shift = Once(input.Shift, "pine: 5", "pine: -1") }),
        Shift("TLAW-CFG-110", "objectives.quota.total", static input => input with { Shift = Once(input.Shift, "total: 9", "total: 10") }),
        Shift("TLAW-CFG-111", "objectives.min_correctly_processed_anomalies", static input => input with { Shift = Once(input.Shift, "min_correctly_processed_anomalies: 2", "min_correctly_processed_anomalies: -1") }),
        Shift("TLAW-CFG-112", "supply.total", static input => input with { Shift = Once(input.Shift, "supply:\n  total: 12", "supply:\n  total: 0") }),
        Shift("TLAW-CFG-113", "supply.total", static input => input with { Shift = Once(input.Shift, "supply:\n  total: 12", "supply:\n  total: 11") }),
        Shift("TLAW-CFG-114", "supply.free_writeoff_buffer", static input => input with { Shift = Once(input.Shift, "free_writeoff_buffer: 3", "free_writeoff_buffer: 4") }),
        Shift("TLAW-CFG-115", "supply.free_writeoff_buffer", static input => input with { Shift = Once(input.Shift, "free_writeoff_buffer: 3", "free_writeoff_buffer: -1") }),
        Shift("TLAW-CFG-116", "manifest", static input => input with { Shift = Before(input.Shift, "manifest:") + "manifest: []\n" }),
        Shift("TLAW-CFG-117", "manifest[1].id", static input => input with { Shift = Once(input.Shift, "- id: log_02", "- id: log_01") }),
        Shift("TLAW-CFG-118", "manifest[0].true_species", static input => input with { Shift = Once(input.Shift, "true_species: pine", "true_species: \"\"") }),
        Shift("TLAW-CFG-119", "manifest[0].declared_species", static input => input with { Shift = Once(input.Shift, "declared_species: pine", "declared_species: \"\"") }),
        Shift("TLAW-CFG-120", "manifest[0].anomaly", static input => input with { Shift = Once(input.Shift, "anomaly: none", "anomaly: \"\"") }),
        Shift("TLAW-CFG-121", "scheduler.capacities.saw", static input => input with { Shift = Once(input.Shift, "    saw: 1\n", string.Empty) }),
        Shift("TLAW-CFG-122", "scheduler.capacities.intake", static input => input with { Shift = Once(input.Shift, "    intake: 1", "    intake: 0") }),
        Shift("TLAW-CFG-123", "scheduler.capacities.containment", static input => input with { Shift = Once(input.Shift, "containment: unlimited", "containment: 5") }),
        Shift("TLAW-CFG-124", "scheduler.initial_admission_delay_seconds", static input => input with { Shift = Once(input.Shift, "initial_admission_delay_seconds: 0", "initial_admission_delay_seconds: -1") }),
        Shift("TLAW-CFG-125", "scheduler.normal_feed_delay_seconds", static input => input with { Shift = Once(input.Shift, "normal_feed_delay_seconds: 5", "normal_feed_delay_seconds: 0") }),
        Shift("TLAW-CFG-126", "scheduler.early_feed_delay_seconds", static input => input with { Shift = Once(input.Shift, "early_feed_delay_seconds: 2", "early_feed_delay_seconds: 0") }),
        Shift("TLAW-CFG-127", "scheduler", static input => input with { Shift = Once(input.Shift, "early_feed_delay_seconds: 2", "early_feed_delay_seconds: 9") }, true),
        Shift("TLAW-CFG-128", "scheduler.saw_cycle_seconds", static input => input with { Shift = Once(input.Shift, "saw_cycle_seconds: 6", "saw_cycle_seconds: 0") }),
        Shift("TLAW-CFG-129", "scheduler.repair_hold_seconds", static input => input with { Shift = Once(input.Shift, "repair_hold_seconds: 6", "repair_hold_seconds: 0") }),
        Shift("TLAW-CFG-130", "scheduler.movement_noise_seconds", static input => input with { Shift = Once(input.Shift, "movement_noise_seconds: 2", "movement_noise_seconds: 0") }),
        Shift("TLAW-CFG-131", "scheduler.default_timeout_route", static input => input with { Shift = Once(input.Shift, "default_timeout_route: saw_queue", "default_timeout_route: procedure") }),
        Shift("TLAW-CFG-132", "scheduler.same_tick_order", static input => input with { Shift = Once(input.Shift, "  - hold_and_procedure_completions\n  - accepted_intents_by_server_receive_sequence", "  - accepted_intents_by_server_receive_sequence\n  - hold_and_procedure_completions") }),
        Shift("TLAW-CFG-133", "line_noise.quiet_when_all_inactive", static input => input with { Shift = Once(input.Shift, "  - repair\n", string.Empty) }),
        Shift("TLAW-CFG-134", "line_noise.penitent_confirm_requires_continuous_quiet_seconds", static input => input with { Shift = Once(input.Shift, "penitent_confirm_requires_continuous_quiet_seconds: 4", "penitent_confirm_requires_continuous_quiet_seconds: 0") }),
        Shift("TLAW-CFG-135", "line_noise.reset_test_progress_when_loud", static input => input with { Shift = Once(input.Shift, "  reset_test_progress_when_loud: true\n", string.Empty) }),
        Shift("TLAW-CFG-136", "line_noise.pause_intake_timer_during_test", static input => input with { Shift = Once(input.Shift, "pause_intake_timer_during_test: false", "pause_intake_timer_during_test: true") }),
        Shift("TLAW-CFG-137", "resources.consumable.holy_water", static input => input with { Shift = Once(input.Shift, "holy_water: 2", "holy_water: -1") }),
        Shift("TLAW-CFG-138", "resources.reusable[1]", static input => input with { Shift = Once(input.Shift, "  - choir_cassette", "  - sound_meter") }),
        Shift("TLAW-CFG-139", "resources", static input => input with { Shift = Once(input.Shift, "  - sound_meter", "  - salt\n  - sound_meter") }),
        Shift("TLAW-CFG-140", "containment.unlimited_capacity", static input => input with { Shift = Once(input.Shift, "unlimited_capacity: true", "unlimited_capacity: false") }),
        Shift("TLAW-CFG-141", "containment.ritual_hold_seconds", static input => input with { Shift = Once(input.Shift, "ritual_hold_seconds: 4", "ritual_hold_seconds: 0") }),
        Shift("TLAW-CFG-142", "containment.service_requested_grace_seconds", static input => input with { Shift = Once(input.Shift, "service_requested_grace_seconds: 20", "service_requested_grace_seconds: 0") }),
        Shift("TLAW-CFG-143", "containment.overdue_seconds", static input => input with { Shift = Once(input.Shift, "overdue_seconds: 10", "overdue_seconds: 0") }),
        Shift("TLAW-CFG-144", "containment.interval_by_danger_weight.4", static input => input with { Shift = Once(input.Shift, "    3_or_more: 60", "    3_or_more: 60\n    '4': 50") }),
        Shift("TLAW-CFG-145", "containment.after_successful_ritual.return_state", static input => input with { Shift = Once(input.Shift, "return_state: STABLE", "return_state: MELTDOWN") }),
        Shift("TLAW-CFG-146", "containment.after_successful_ritual", static input => input with { Shift = Once(input.Shift, "retain_logs: true", "retain_logs: false") }),
        Shift("TLAW-CFG-147", "containment.prototype_incident", static input => input with { Shift = Once(input.Shift, "repeat_before_resolution: false", "repeat_before_resolution: true") }),
        Shift("TLAW-CFG-148", "containment.post_facto_interval_inference", static input => input with { Shift = Once(input.Shift, "seeded_jitter: false", "seeded_jitter: true") }),
        Shift("TLAW-CFG-149", "success_predicate.all[0]", static input => input with { Shift = Once(input.Shift, "  - quota_by_species_met", "  - bogus_token") }),
        Shift("TLAW-CFG-150", "success_predicate.all", static input => input with { Shift = Once(input.Shift, "correctly_processed_anomalies_at_least_2", "correctly_processed_anomalies_at_least_3") }),
        Shift("TLAW-CFG-151", "objectives.min_correctly_processed_anomalies", static input => input with { Shift = Once(input.Shift, "min_correctly_processed_anomalies: 2", "min_correctly_processed_anomalies: 6") }),

        Anomalies("TLAW-CFG-201", "anomalies", static input => input with { Anomalies = Before(input.Anomalies, "anomalies:") + "anomalies: {}\n" }),
        Anomalies("TLAW-CFG-202", "anomalies.", static input => input with { Anomalies = Once(input.Anomalies, "  PENITENT_TRUNK:", "  \"\":") }),
        Anomalies("TLAW-CFG-203", "anomalies.PENITENT_TRUNK.danger_weight", static input => input with { Anomalies = Once(input.Anomalies, "    danger_weight: 1", "    danger_weight: -1") }),
        Anomalies("TLAW-CFG-204", "anomalies.PENITENT_TRUNK.instant_clues", static input => input with { Anomalies = Once(input.Anomalies, "    instant_clues:\n    - dark_resin\n    - cold_bark", "    instant_clues: []") }),
        Anomalies("TLAW-CFG-205", "anomalies.PENITENT_TRUNK.observed_clues[1]", static input => input with { Anomalies = Once(input.Anomalies, "    observed_clues:\n    - whisper_under_line_noise", "    observed_clues:\n    - whisper_under_line_noise\n    - whisper_under_line_noise") }),
        Anomalies("TLAW-CFG-206", "anomalies.PENITENT_TRUNK.confirm_test", static input => input with { Anomalies = ReplaceBlock(input.Anomalies, "    confirm_test:\n", "    processing:\n", string.Empty) }),
        Anomalies("TLAW-CFG-207", "anomalies.PENITENT_TRUNK.confirm_test.duration_seconds", static input => input with { Anomalies = Once(input.Anomalies, "      duration_seconds: 4", "      duration_seconds: 0") }),
        Anomalies("TLAW-CFG-208", "anomalies.PENITENT_TRUNK.confirm_test.continuous", static input => input with { Anomalies = Once(input.Anomalies, "      continuous: true\n", string.Empty) }),
        Anomalies("TLAW-CFG-209", "anomalies.PENITENT_TRUNK.confirm_test.result", static input => input with { Anomalies = Once(input.Anomalies, "result: spoken_names_detected", "result: \"\"") }),
        Anomalies("TLAW-CFG-210", "anomalies.PENITENT_TRUNK.confirm_test.tools[1]", static input => input with { Anomalies = Once(input.Anomalies, "      - sound_meter", "      - sound_meter\n      - sound_meter") }),
        Anomalies("TLAW-CFG-211", "anomalies.PENITENT_TRUNK.confirm_test.required_line_noise", static input => input with { Anomalies = Once(input.Anomalies, "required_line_noise: QUIET", "required_line_noise: MEDIUM") }),
        Anomalies("TLAW-CFG-212", "anomalies.PENITENT_TRUNK.confirm_test.reset_when_condition_lost", static input => input with { Anomalies = Once(input.Anomalies, "      reset_when_condition_lost: true\n", string.Empty) }),
        Anomalies("TLAW-CFG-213", "anomalies.PENITENT_TRUNK.processing", static input => input with { Anomalies = ReplaceBlock(input.Anomalies, "    processing:\n", "    procedure:\n", string.Empty) }),
        Anomalies("TLAW-CFG-214", "anomalies.PENITENT_TRUNK.processing.required_flags[1]", static input => input with { Anomalies = Once(input.Anomalies, "      - SANITIZED_PENITENT", "      - SANITIZED_PENITENT\n      - SANITIZED_PENITENT") }),
        Anomalies("TLAW-CFG-215", "anomalies.PENITENT_TRUNK.processing.route_without_flags", static input => input with { Anomalies = Once(input.Anomalies, "route_without_flags: allowed", "route_without_flags: forbidden") }),
        Anomalies("TLAW-CFG-216", "anomalies.PENITENT_TRUNK.processing.on_correct", static input => input with { Anomalies = ReplaceBlock(input.Anomalies, "      on_correct:\n", "      on_incorrect:\n", string.Empty) }),
        Anomalies("TLAW-CFG-217", "anomalies.PENITENT_TRUNK.processing.on_incorrect", static input => input with { Anomalies = ReplaceBlock(input.Anomalies, "      on_incorrect:\n", "    procedure:\n", string.Empty) }),
        Anomalies("TLAW-CFG-218", "anomalies.PENITENT_TRUNK.processing.on_correct.terminal_state", static input => input with { Anomalies = Once(input.Anomalies, "terminal_state: PROCESSED", "terminal_state: AT_INTAKE") }),
        Anomalies("TLAW-CFG-219", "anomalies.PENITENT_TRUNK.processing.on_correct.quota_credit.species", static input => input with { Anomalies = Once(input.Anomalies, "species: true_species", "species: pine") }),
        Anomalies("TLAW-CFG-220", "anomalies.PENITENT_TRUNK.processing.on_correct.quota_credit.units", static input => input with { Anomalies = Once(input.Anomalies, "units: 1", "units: -1") }),
        Anomalies("TLAW-CFG-221", "anomalies.PENITENT_TRUNK.processing.on_incorrect.quota_credit", static input => input with { Anomalies = Once(input.Anomalies, "species: none\n          units: 0", "species: none\n          units: 1") }),
        Anomalies("TLAW-CFG-222", "anomalies.PENITENT_TRUNK.processing.on_correct.correct_anomaly_delta", static input => input with { Anomalies = Once(input.Anomalies, "correct_anomaly_delta: 1", "correct_anomaly_delta: -1") }),
        Anomalies("TLAW-CFG-223", "anomalies.PENITENT_TRUNK.processing.on_correct.effects", static input => input with { Anomalies = Once(input.Anomalies, "        effects: []\n", string.Empty) }),
        Anomalies("TLAW-CFG-224", "anomalies.PENITENT_TRUNK.processing.on_incorrect.effects[0].type", static input => input with { Anomalies = Once(input.Anomalies, "type: time_penalty", "type: explosion") }),
        Anomalies("TLAW-CFG-225", "anomalies.PENITENT_TRUNK.processing.on_incorrect.effects[0].event", static input => input with { Anomalies = Once(input.Anomalies, "event: FALSE_PA_ANNOUNCEMENT", "event: \"\"") }),
        Anomalies("TLAW-CFG-226", "anomalies.RESIN_BLASPHEMER.processing.on_incorrect.effects[0]", static input => input with { Anomalies = Once(input.Anomalies, "          target: nearest_line_button\n          duration_seconds: 10\n    procedure:", "          duration_seconds: 10\n    procedure:") }),
        Anomalies("TLAW-CFG-227", "anomalies.PENITENT_TRUNK.procedure.steps", static input => input with { Anomalies = Once(input.Anomalies, "      steps:\n      - item: holy_water\n        hold_seconds: 3\n        consumes: true", "      steps: []") }),
        Anomalies("TLAW-CFG-228", "anomalies.PENITENT_TRUNK.procedure.steps[0].item", static input => input with { Anomalies = Once(input.Anomalies, "item: holy_water", "item: \"\"") }),
        Anomalies("TLAW-CFG-229", "anomalies.PENITENT_TRUNK.procedure.steps[0].consumes", static input => input with { Anomalies = Once(input.Anomalies, "        consumes: true\n", string.Empty) }),
        Anomalies("TLAW-CFG-230", "anomalies.PENITENT_TRUNK.procedure.steps[0].hold_seconds", static input => input with { Anomalies = Once(input.Anomalies, "hold_seconds: 3", "hold_seconds: 0") }),
        Anomalies("TLAW-CFG-231", "anomalies.PENITENT_TRUNK.procedure.grants_flags", static input => input with { Anomalies = Once(input.Anomalies, "      grants_flags:\n      - SANITIZED_PENITENT", "      grants_flags: []") }),
        Anomalies("TLAW-CFG-232", "anomalies.PENITENT_TRUNK", static input => input with { Anomalies = Once(input.Anomalies, "      - SANITIZED_PENITENT", "      - DIFFERENT_FLAG") }),
        Anomalies("TLAW-CFG-233", "anomalies.RESIN_BLASPHEMER.wrong_actions.", static input => input with { Anomalies = Once(input.Anomalies, "      holy_water:", "      \"\":") }),
        Anomalies("TLAW-CFG-234", "anomalies.RESIN_BLASPHEMER.wrong_actions.holy_water.consumes", static input => input with { Anomalies = Once(input.Anomalies, "        effects:\n        - type: lock\n          event: RESIN_BUTTON_LOCK\n          target: nearest_line_button\n          duration_seconds: 10\n        consumes: true", "        effects:\n        - type: lock\n          event: RESIN_BUTTON_LOCK\n          target: nearest_line_button\n          duration_seconds: 10") }),
        Anomalies("TLAW-CFG-235", "anomalies.RESIN_BLASPHEMER.wrong_actions.holy_water.terminal_state", static input => input with { Anomalies = Once(input.Anomalies, "terminal_state: unchanged", "terminal_state: IN_SAW") }),
        Anomalies("TLAW-CFG-236", "anomalies.RESIN_BLASPHEMER.wrong_actions.holy_water.effects", static input => input with { Anomalies = Once(input.Anomalies, "        effects:\n        - type: lock\n          event: RESIN_BUTTON_LOCK\n          target: nearest_line_button\n          duration_seconds: 10\n        consumes: true", "        effects:\n        - type: lock\n          event: RESIN_BUTTON_LOCK\n          duration_seconds: 10\n        consumes: true") }),

        Cross("TLAW-CFG-301", "schema_version", static input => input with { Shift = Once(input.Shift, "schema_version: 2", "schema_version: 3") }),
        Cross("TLAW-CFG-302", "manifest[2].anomaly", static input => input with { Shift = Once(input.Shift, "anomaly: PENITENT_TRUNK", "anomaly: GHOST_LOG") }),
        Cross("TLAW-CFG-303", "anomalies.PENITENT_TRUNK.procedure.steps[0].item", static input => input with { Anomalies = Once(input.Anomalies, "item: holy_water", "item: crowbar") }),
        Cross("TLAW-CFG-304", "anomalies.PENITENT_TRUNK.procedure.steps[0]", static input => input with { Anomalies = Once(input.Anomalies, "item: holy_water", "item: sound_meter") }),
        Cross("TLAW-CFG-305", "anomalies.RESIN_BLASPHEMER.procedure.steps[0]", static input => input with { Anomalies = Once(input.Anomalies, "item: salt\n        consumes: true", "item: salt\n        consumes: false") }),
        Cross("TLAW-CFG-306", "anomalies.PENITENT_TRUNK.confirm_test.tools[0]", static input => input with { Anomalies = Once(input.Anomalies, "- sound_meter", "- holy_water") }),
        Cross("TLAW-CFG-307", "anomalies.RESIN_BLASPHEMER.wrong_actions.scale", static input => input with { Anomalies = Once(input.Anomalies, "      holy_water:", "      scale:") }),
        Cross("TLAW-CFG-308", "anomalies.PENITENT_TRUNK.confirm_test.duration_seconds", static input => input with { Anomalies = Once(input.Anomalies, "      duration_seconds: 4", "      duration_seconds: 5") }),
        Cross("TLAW-CFG-309", "anomalies.PENITENT_TRUNK.confirm_test.reset_when_condition_lost", static input => input with { Anomalies = Once(input.Anomalies, "reset_when_condition_lost: true", "reset_when_condition_lost: false") }),
        Cross("TLAW-CFG-310", "manifest[0].true_species", static input => input with { Shift = Once(input.Shift, "true_species: pine", "true_species: birch") }, true),
    ];

    public static IEnumerable<object?[]> TheoryData => Cases.Select(static testCase => new object?[] { testCase });

    private static ValidationCase Shift(string code, string path, Func<YamlInputs, YamlInputs> mutation, bool isSuccess = false) => new(code, ConfigurationDocument.Shift, path, isSuccess, isSuccess ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, mutation);
    private static ValidationCase Anomalies(string code, string path, Func<YamlInputs, YamlInputs> mutation) => new(code, ConfigurationDocument.Anomalies, path, false, DiagnosticSeverity.Error, mutation);
    private static ValidationCase Cross(string code, string path, Func<YamlInputs, YamlInputs> mutation, bool isSuccess = false) => new(code, ConfigurationDocument.CrossDocument, path, isSuccess, isSuccess ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, mutation);
    private const string BlankScalar = "shift_id: \" \"";
    private static string Once(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException($"Test mutation source was not found: {oldValue}");
        return string.Concat(text.AsSpan(0, index), newValue, text.AsSpan(index + oldValue.Length));
    }

    private static string WithoutFirstLine(string text) => text[(text.IndexOf('\n') + 1)..];
    private static string Before(string text, string marker) => text[..text.IndexOf(marker, StringComparison.Ordinal)];
    private static string ReplaceBlock(string text, string start, string end, string replacement)
    {
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        var endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < 0) throw new InvalidOperationException("Test mutation block was not found.");
        return string.Concat(text.AsSpan(0, startIndex), replacement, text.AsSpan(endIndex));
    }
}
