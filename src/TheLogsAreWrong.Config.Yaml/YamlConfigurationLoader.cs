using System.Collections.Immutable;
using System.Globalization;
using TheLogsAreWrong.Config.Yaml.Dto;
using TheLogsAreWrong.Config.Yaml.Parsing;
using TheLogsAreWrong.Domain.Configuration;
using TheLogsAreWrong.Domain.Configuration.Diagnostics;
using TheLogsAreWrong.Domain.Enums;
using TheLogsAreWrong.Domain.Identifiers;
using TheLogsAreWrong.Domain.Primitives;
using YamlDotNet.RepresentationModel;

namespace TheLogsAreWrong.Config.Yaml;

/// <summary>Loads and validates the frozen Gate 0 YAML configuration without exposing YAML types to Domain.</summary>
public sealed class YamlConfigurationLoader
{
    public ConfigurationLoadResult Load(string shiftYaml, string anomaliesYaml)
    {
        ArgumentNullException.ThrowIfNull(shiftYaml);
        ArgumentNullException.ThrowIfNull(anomaliesYaml);

        var diagnostics = new List<ConfigurationDiagnostic>();
        var shiftRaw = YamlDocumentParser.Parse(shiftYaml, ConfigurationDocument.Shift, "TLAW-CFG-001", diagnostics);
        var anomaliesRaw = YamlDocumentParser.Parse(anomaliesYaml, ConfigurationDocument.Anomalies, "TLAW-CFG-002", diagnostics);
        var context = new ValidationContext(diagnostics);

        if (shiftRaw is not null && anomaliesRaw is not null)
        {
            var shiftSchema = TextValue(Find(shiftRaw.Root, "schema_version"));
            var anomaliesSchema = TextValue(Find(anomaliesRaw.Root, "schema_version"));
            if (shiftSchema is not null && anomaliesSchema is not null && !string.Equals(shiftSchema, anomaliesSchema, StringComparison.Ordinal))
            {
                context.Error("TLAW-CFG-301", ConfigurationDocument.CrossDocument, "schema_version", "Both documents must declare the same schema version.");
            }
        }

        var shift = shiftRaw is null ? null : BuildShift(shiftRaw, context);
        var anomalies = anomaliesRaw is null ? null : BuildAnomalies(anomaliesRaw, context);

        if (shift is not null && anomalies is not null)
        {
            ValidateCrossDocument(shift, anomalies, context);
        }

        var ordered = diagnostics
            .OrderBy(static diagnostic => diagnostic.Document)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToImmutableArray();

        return ordered.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ? ConfigurationLoadResult.Failure(ordered)
            : ConfigurationLoadResult.Success(new ValidatedConfiguration(shift!, anomalies!), ordered);
    }

    /// <summary>Reads both inputs and delegates to <see cref="Load(string, string)"/>.</summary>
    public ConfigurationLoadResult LoadFromFiles(string shiftPath, string anomaliesPath)
    {
        ArgumentNullException.ThrowIfNull(shiftPath);
        ArgumentNullException.ThrowIfNull(anomaliesPath);

        var diagnostics = new List<ConfigurationDiagnostic>();
        string? shift = TryRead(shiftPath, ConfigurationDocument.Shift, diagnostics);
        string? anomalies = TryRead(anomaliesPath, ConfigurationDocument.Anomalies, diagnostics);
        if (shift is null || anomalies is null)
        {
            return ConfigurationLoadResult.Failure(Order(diagnostics));
        }

        return Load(shift, anomalies);
    }

    private static string? TryRead(string path, ConfigurationDocument document, List<ConfigurationDiagnostic> diagnostics)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            var code = document == ConfigurationDocument.Shift ? "TLAW-CFG-001" : "TLAW-CFG-002";
            diagnostics.Add(new ConfigurationDiagnostic(code, DiagnosticSeverity.Error, document, "(file)", "YAML input file could not be read."));
            return null;
        }
    }

    private static ImmutableArray<ConfigurationDiagnostic> Order(IEnumerable<ConfigurationDiagnostic> diagnostics) => diagnostics
        .OrderBy(static diagnostic => diagnostic.Document)
        .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
        .ThenBy(static diagnostic => diagnostic.Path, StringComparer.Ordinal)
        .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
        .ToImmutableArray();

    private static ShiftConfiguration? BuildShift(RawYamlDocument document, ValidationContext context)
    {
        const ConfigurationDocument source = ConfigurationDocument.Shift;
        var start = context.ErrorCount;
        var root = document.Root;
        var schemaVersion = RequiredInt(root, "schema_version", "schema_version", source, context);
        if (schemaVersion is null) context.Error("TLAW-CFG-003", source, "schema_version", "Schema version is required.");
        if (schemaVersion is null)
        {
            return null;
        }

        if (schemaVersion != 2)
        {
            context.Error("TLAW-CFG-004", source, "schema_version", "Only schema version 2 is supported.");
            return null;
        }

        if (!Has(root, "shift_id") || !Has(root, "manifest"))
        {
            context.Error("TLAW-CFG-014", source, "(root)", "This input is not a shift document.");
            return null;
        }

        Known(root, source, context, "", "schema_version", "shift_id", "seed", "profiles", "objectives", "supply", "scheduler", "line_noise", "resources", "containment", "success_predicate", "manifest");
        var shiftIdText = RequiredId(root, "shift_id", "shift_id", source, context, "TLAW-CFG-101") ?? "invalid";
        var shiftId = ShiftId.From(shiftIdText);
        var seedValue = RequiredInt(root, "seed", "seed", source, context);
        if (seedValue is null) context.Error("TLAW-CFG-102", source, "seed", "Seed must be present and an integer.");
        var seed = seedValue ?? 0;

        var profilesNode = RequiredMap(root, "profiles", "profiles", source, context);
        var profiles = BuildProfiles(profilesNode, source, context);
        var objectives = BuildObjectives(RequiredMap(root, "objectives", "objectives", source, context), source, context);
        var supply = BuildSupply(RequiredMap(root, "supply", "supply", source, context), source, context);
        var scheduler = BuildScheduler(RequiredMap(root, "scheduler", "scheduler", source, context), source, context);
        var lineNoise = BuildLineNoise(RequiredMap(root, "line_noise", "line_noise", source, context), source, context);
        var resources = BuildResources(RequiredMap(root, "resources", "resources", source, context), source, context);
        var containment = BuildContainment(RequiredMap(root, "containment", "containment", source, context), source, context);
        var successMap = RequiredMap(root, "success_predicate", "success_predicate", source, context);
        if (successMap is not null) Known(successMap, source, context, "success_predicate", "all");
        var success = BuildSuccessPredicate(successMap is null ? null : RequiredSequence(successMap, "all", "success_predicate.all", source, context), source, context);
        var manifest = BuildManifest(RequiredSequence(root, "manifest", "manifest", source, context), source, context);

        if (supply.Total != manifest.Length)
        {
            context.Error("TLAW-CFG-113", source, "supply.total", "Supply total must equal manifest count.");
        }

        if (supply.FreeWriteoffBuffer != supply.Total - objectives.Quota.Total)
        {
            context.Error("TLAW-CFG-114", source, "supply.free_writeoff_buffer", "Writeoff buffer must equal supply minus quota.");
        }

        if (supply.FreeWriteoffBuffer < 0)
        {
            context.Error("TLAW-CFG-115", source, "supply.free_writeoff_buffer", "Writeoff buffer cannot be negative.");
        }

        var anomalousCount = manifest.Count(static log => log.Anomaly is not null);
        if (objectives.MinCorrectlyProcessedAnomalies > anomalousCount)
        {
            context.Error("TLAW-CFG-151", source, "objectives.min_correctly_processed_anomalies", "Required correctly processed anomalies exceed anomalous manifest entries.");
        }

        var matchingPredicate = $"correctly_processed_anomalies_at_least_{objectives.MinCorrectlyProcessedAnomalies}";
        if (!success.Contains(matchingPredicate, StringComparer.Ordinal))
        {
            context.Error("TLAW-CFG-150", source, "success_predicate.all", "Success predicate must match the configured anomaly objective.");
        }

        return context.ErrorCount == start
            ? new ShiftConfiguration(shiftId, new ShiftSeed(seed), profiles, objectives, supply, scheduler, lineNoise, resources, containment, success, manifest)
            : null;
    }

    private static ImmutableDictionary<ProfileId, ShiftProfile> BuildProfiles(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var builder = ImmutableDictionary.CreateBuilder<ProfileId, ShiftProfile>();
        if (node is null)
        {
            return builder.ToImmutable();
        }

        if (node.Children.Count == 0)
        {
            context.Error("TLAW-CFG-103", source, "profiles", "Profiles cannot be empty.");
        }

        foreach (var (key, value) in Entries(node))
        {
            var path = Child("profiles", key);
            var idText = ValidateId(key, path, source, context, "TLAW-CFG-103") ?? "invalid";
            var profile = Map(value, path, source, context);
            if (profile is null)
            {
                continue;
            }

            Known(profile, source, context, path, "intake_timeout_seconds", "hard_shift_deadline_seconds");
            var timeout = RequiredInt(profile, "intake_timeout_seconds", Child(path, "intake_timeout_seconds"), source, context) ?? 0;
            var deadline = RequiredInt(profile, "hard_shift_deadline_seconds", Child(path, "hard_shift_deadline_seconds"), source, context) ?? 0;
            if (timeout <= 0) context.Error("TLAW-CFG-104", source, Child(path, "intake_timeout_seconds"), "Intake timeout must be positive.");
            if (deadline <= 0) context.Error("TLAW-CFG-105", source, Child(path, "hard_shift_deadline_seconds"), "Hard deadline must be positive.");
            if (deadline < timeout) context.Warning("TLAW-CFG-106", source, path, "Hard deadline is lower than intake timeout.");
            builder[ProfileId.From(idText)] = new ShiftProfile(timeout, deadline);
        }

        return builder.ToImmutable();
    }

    private static ObjectivesDefinition BuildObjectives(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new ObjectivesDefinition(new QuotaDefinition(0, ImmutableDictionary<SpeciesId, int>.Empty), 0);
        Known(node, source, context, "objectives", "quota", "min_correctly_processed_anomalies");
        var quotaNode = RequiredMap(node, "quota", "objectives.quota", source, context);
        var quota = BuildQuota(quotaNode, source, context);
        var minimum = RequiredInt(node, "min_correctly_processed_anomalies", "objectives.min_correctly_processed_anomalies", source, context) ?? 0;
        if (minimum < 0) context.Error("TLAW-CFG-111", source, "objectives.min_correctly_processed_anomalies", "Minimum anomaly count cannot be negative.");
        return new ObjectivesDefinition(quota, minimum);
    }

    private static QuotaDefinition BuildQuota(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var species = ImmutableDictionary.CreateBuilder<SpeciesId, int>();
        if (node is null) return new QuotaDefinition(0, species.ToImmutable());
        Known(node, source, context, "objectives.quota", "total", "by_species");
        var total = RequiredInt(node, "total", "objectives.quota.total", source, context) ?? 0;
        if (total <= 0) context.Error("TLAW-CFG-107", source, "objectives.quota.total", "Quota total must be positive.");
        var bySpecies = RequiredMap(node, "by_species", "objectives.quota.by_species", source, context);
        if (bySpecies is not null)
        {
            if (bySpecies.Children.Count == 0) context.Error("TLAW-CFG-108", source, "objectives.quota.by_species", "Quota species cannot be empty.");
            foreach (var (key, value) in Entries(bySpecies))
            {
                var path = Child("objectives.quota.by_species", key);
                var id = ValidateId(key, path, source, context, "TLAW-CFG-108") ?? "invalid";
                var amount = Int(value, path, source, context) ?? 0;
                if (amount < 0) context.Error("TLAW-CFG-109", source, path, "Species quota cannot be negative.");
                species[SpeciesId.From(id)] = amount;
            }
        }

        if (total != species.Values.Sum()) context.Error("TLAW-CFG-110", source, "objectives.quota.total", "Quota total must equal the sum of species quotas.");
        return new QuotaDefinition(total, species.ToImmutable());
    }

    private static SupplyDefinition BuildSupply(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new SupplyDefinition(0, 0);
        Known(node, source, context, "supply", "total", "free_writeoff_buffer");
        var total = RequiredInt(node, "total", "supply.total", source, context) ?? 0;
        var buffer = RequiredInt(node, "free_writeoff_buffer", "supply.free_writeoff_buffer", source, context) ?? 0;
        if (total <= 0) context.Error("TLAW-CFG-112", source, "supply.total", "Supply total must be positive.");
        return new SupplyDefinition(total, buffer);
    }

    private static ImmutableArray<ManifestLogDefinition> BuildManifest(YamlSequenceNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var builder = ImmutableArray.CreateBuilder<ManifestLogDefinition>();
        if (node is null) return builder.ToImmutable();
        if (node.Children.Count == 0) context.Error("TLAW-CFG-116", source, "manifest", "Manifest cannot be empty.");
        var ids = new HashSet<LogId>();
        for (var index = 0; index < node.Children.Count; index++)
        {
            var path = $"manifest[{index}]";
            var entry = Map(node.Children[index], path, source, context);
            if (entry is null) continue;
            Known(entry, source, context, path, "id", "true_species", "declared_species", "anomaly");
            var id = LogId.From(RequiredId(entry, "id", Child(path, "id"), source, context, "TLAW-CFG-117") ?? "invalid");
            var trueSpecies = SpeciesId.From(RequiredId(entry, "true_species", Child(path, "true_species"), source, context, "TLAW-CFG-118") ?? "invalid");
            var declaredSpecies = SpeciesId.From(RequiredId(entry, "declared_species", Child(path, "declared_species"), source, context, "TLAW-CFG-119") ?? "invalid");
            var anomalyText = RequiredText(entry, "anomaly", Child(path, "anomaly"), source, context);
            AnomalyId? anomaly = null;
            if (anomalyText is null || string.IsNullOrWhiteSpace(anomalyText))
            {
                context.Error("TLAW-CFG-120", source, Child(path, "anomaly"), "Anomaly must be 'none' or a valid identifier.");
            }
            else if (!string.Equals(anomalyText, "none", StringComparison.Ordinal))
            {
                if (HasSurroundingWhitespace(anomalyText)) context.Error("TLAW-CFG-013", source, Child(path, "anomaly"), "Identifiers cannot have surrounding whitespace.");
                else anomaly = AnomalyId.From(anomalyText);
            }

            if (!ids.Add(id)) context.Error("TLAW-CFG-117", source, Child(path, "id"), "Manifest identifiers must be unique.");
            builder.Add(new ManifestLogDefinition(id, trueSpecies, declaredSpecies, anomaly));
        }

        return builder.ToImmutable();
    }

    private static SchedulerConfiguration BuildScheduler(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var capacities = ImmutableDictionary.CreateBuilder<NodeId, NodeCapacity>();
        var stages = ImmutableArray.CreateBuilder<HostTickStage>();
        if (node is null) return new SchedulerConfiguration(capacities.ToImmutable(), 0, 0, 0, 0, 0, 0, string.Empty, stages.ToImmutable());
        Known(node, source, context, "scheduler", "capacities", "initial_admission_delay_seconds", "normal_feed_delay_seconds", "early_feed_delay_seconds", "saw_cycle_seconds", "repair_hold_seconds", "movement_noise_seconds", "default_timeout_route", "same_tick_order");
        var capacityNode = RequiredMap(node, "capacities", "scheduler.capacities", source, context);
        var requiredCapacities = new[] { "feed_gate", "intake", "procedure", "saw_queue", "saw", "containment" };
        if (capacityNode is not null)
        {
            Known(capacityNode, source, context, "scheduler.capacities", requiredCapacities);
            foreach (var name in requiredCapacities)
            {
                var path = Child("scheduler.capacities", name);
                var raw = RequiredText(capacityNode, name, path, source, context);
                if (raw is null) { context.Error("TLAW-CFG-121", source, path, "Required capacity is missing."); continue; }
                if (name == "containment")
                {
                    if (!string.Equals(raw, "unlimited", StringComparison.Ordinal)) context.Error("TLAW-CFG-123", source, path, "Containment capacity must be unlimited.");
                    capacities[NodeId.CONTAINMENT] = NodeCapacity.Unlimited;
                    continue;
                }

                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity))
                {
                    context.Error("TLAW-CFG-008", source, path, "Capacity must be an integer.");
                    continue;
                }

                if (capacity <= 0) context.Error("TLAW-CFG-122", source, path, "Limited capacity must be positive.");
                capacities[ToNodeId(name)] = capacity > 0 ? NodeCapacity.Limited(capacity) : NodeCapacity.Limited(1);
            }
        }

        var initial = RequiredInt(node, "initial_admission_delay_seconds", "scheduler.initial_admission_delay_seconds", source, context) ?? 0;
        var normal = RequiredInt(node, "normal_feed_delay_seconds", "scheduler.normal_feed_delay_seconds", source, context) ?? 0;
        var early = RequiredInt(node, "early_feed_delay_seconds", "scheduler.early_feed_delay_seconds", source, context) ?? 0;
        var saw = RequiredInt(node, "saw_cycle_seconds", "scheduler.saw_cycle_seconds", source, context) ?? 0;
        var repair = RequiredInt(node, "repair_hold_seconds", "scheduler.repair_hold_seconds", source, context) ?? 0;
        var movement = RequiredInt(node, "movement_noise_seconds", "scheduler.movement_noise_seconds", source, context) ?? 0;
        if (initial < 0) context.Error("TLAW-CFG-124", source, "scheduler.initial_admission_delay_seconds", "Initial delay cannot be negative.");
        if (normal <= 0) context.Error("TLAW-CFG-125", source, "scheduler.normal_feed_delay_seconds", "Normal feed delay must be positive.");
        if (early <= 0) context.Error("TLAW-CFG-126", source, "scheduler.early_feed_delay_seconds", "Early feed delay must be positive.");
        if (early >= normal) context.Warning("TLAW-CFG-127", source, "scheduler", "Early feed delay should be lower than normal feed delay.");
        if (saw <= 0) context.Error("TLAW-CFG-128", source, "scheduler.saw_cycle_seconds", "Saw cycle must be positive.");
        if (repair <= 0) context.Error("TLAW-CFG-129", source, "scheduler.repair_hold_seconds", "Repair hold must be positive.");
        if (movement <= 0) context.Error("TLAW-CFG-130", source, "scheduler.movement_noise_seconds", "Movement noise duration must be positive.");
        var route = RequiredText(node, "default_timeout_route", "scheduler.default_timeout_route", source, context) ?? string.Empty;
        if (!string.Equals(route, "saw_queue", StringComparison.Ordinal)) context.Error("TLAW-CFG-131", source, "scheduler.default_timeout_route", "Only saw_queue is an approved timeout route.");
        var stageNode = RequiredSequence(node, "same_tick_order", "scheduler.same_tick_order", source, context);
        if (stageNode is not null)
        {
            foreach (var (index, item) in stageNode.Children.Select(static (item, index) => (index, item)))
            {
                var path = $"scheduler.same_tick_order[{index}]";
                var value = Scalar(item, path, source, context);
                if (value is not null && Enum.TryParse<HostTickStage>(value, false, out var stage)) stages.Add(stage);
                else context.Error("TLAW-CFG-132", source, path, "Unknown host tick stage.");
            }
        }

        var expectedStages = new[] { HostTickStage.hold_and_procedure_completions, HostTickStage.accepted_intents_by_server_receive_sequence, HostTickStage.deadline_expirations, HostTickStage.saw_transitions, HostTickStage.feed_and_auto_routes, HostTickStage.derived_states, HostTickStage.event_emission };
        if (!stages.SequenceEqual(expectedStages)) context.Error("TLAW-CFG-132", source, "scheduler.same_tick_order", "Host tick stages must be the approved ordered set.");
        return new SchedulerConfiguration(capacities.ToImmutable(), initial, normal, early, saw, repair, movement, route, stages.ToImmutable());
    }

    private static LineNoiseConfiguration BuildLineNoise(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var inactive = ImmutableArray.CreateBuilder<string>();
        if (node is null) return new LineNoiseConfiguration(inactive.ToImmutable(), 0, false, false);
        Known(node, source, context, "line_noise", "quiet_when_all_inactive", "penitent_confirm_requires_continuous_quiet_seconds", "reset_test_progress_when_loud", "pause_intake_timer_during_test");
        var quiet = RequiredSequence(node, "quiet_when_all_inactive", "line_noise.quiet_when_all_inactive", source, context);
        if (quiet is not null) foreach (var (index, item) in quiet.Children.Select(static (item, index) => (index, item))) inactive.Add(Scalar(item, $"line_noise.quiet_when_all_inactive[{index}]", source, context) ?? string.Empty);
        var expected = new[] { "saw", "movement_noise", "repair" };
        if (!inactive.OrderBy(static value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(static value => value, StringComparer.Ordinal))) context.Error("TLAW-CFG-133", source, "line_noise.quiet_when_all_inactive", "Quiet prerequisites must be the approved set.");
        var duration = RequiredInt(node, "penitent_confirm_requires_continuous_quiet_seconds", "line_noise.penitent_confirm_requires_continuous_quiet_seconds", source, context) ?? 0;
        if (duration <= 0) context.Error("TLAW-CFG-134", source, "line_noise.penitent_confirm_requires_continuous_quiet_seconds", "Quiet duration must be positive.");
        var resetValue = RequiredBool(node, "reset_test_progress_when_loud", "line_noise.reset_test_progress_when_loud", source, context);
        if (resetValue is null) context.Error("TLAW-CFG-135", source, "line_noise.reset_test_progress_when_loud", "Reset behavior must be explicitly boolean.");
        var reset = resetValue ?? false;
        var pause = RequiredBool(node, "pause_intake_timer_during_test", "line_noise.pause_intake_timer_during_test", source, context) ?? false;
        if (pause) context.Error("TLAW-CFG-136", source, "line_noise.pause_intake_timer_during_test", "The intake timer cannot pause during a test.");
        return new LineNoiseConfiguration(inactive.ToImmutable(), duration, reset, pause);
    }

    private static ResourcesConfiguration BuildResources(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var consumable = ImmutableDictionary.CreateBuilder<ItemId, int>();
        var reusable = ImmutableHashSet.CreateBuilder<ItemId>();
        if (node is null) return new ResourcesConfiguration(consumable.ToImmutable(), reusable.ToImmutable());
        Known(node, source, context, "resources", "consumable", "reusable");
        var consumables = RequiredMap(node, "consumable", "resources.consumable", source, context);
        if (consumables is not null) foreach (var (key, value) in Entries(consumables))
        {
            var path = Child("resources.consumable", key);
            var id = ItemId.From(ValidateId(key, path, source, context, "TLAW-CFG-137") ?? "invalid");
            var count = Int(value, path, source, context) ?? 0;
            if (count < 0) context.Error("TLAW-CFG-137", source, path, "Consumable count cannot be negative.");
            if (!consumable.TryAdd(id, count)) context.Error("TLAW-CFG-137", source, path, "Consumable identifiers must be unique.");
        }

        var reusableNode = RequiredSequence(node, "reusable", "resources.reusable", source, context);
        if (reusableNode is not null) foreach (var (index, value) in reusableNode.Children.Select(static (item, index) => (index, item)))
        {
            var path = $"resources.reusable[{index}]";
            var id = ItemId.From(ValidateId(Scalar(value, path, source, context), path, source, context, "TLAW-CFG-138") ?? "invalid");
            if (!reusable.Add(id)) context.Error("TLAW-CFG-138", source, path, "Reusable identifiers must be unique.");
        }

        foreach (var item in consumable.Keys.Intersect(reusable)) context.Error("TLAW-CFG-139", source, "resources", "An item cannot be both consumable and reusable.");
        return new ResourcesConfiguration(consumable.ToImmutable(), reusable.ToImmutable());
    }

    private static ContainmentConfiguration BuildContainment(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var intervals = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        if (node is null) return new ContainmentConfiguration(false, 0, 0, 0, intervals.ToImmutable(), new AfterSuccessfulRitualDefinition(default, false, false, false), new PrototypeIncidentDefinition(string.Empty, 0, false, false), new PostFactoIntervalInferenceDefinition(false, string.Empty, false));
        Known(node, source, context, "containment", "unlimited_capacity", "ritual_hold_seconds", "service_requested_grace_seconds", "overdue_seconds", "interval_by_danger_weight", "after_successful_ritual", "prototype_incident", "post_facto_interval_inference");
        var unlimited = RequiredBool(node, "unlimited_capacity", "containment.unlimited_capacity", source, context) ?? false;
        if (!unlimited) context.Error("TLAW-CFG-140", source, "containment.unlimited_capacity", "Containment must have unlimited capacity.");
        var ritual = RequiredInt(node, "ritual_hold_seconds", "containment.ritual_hold_seconds", source, context) ?? 0;
        var grace = RequiredInt(node, "service_requested_grace_seconds", "containment.service_requested_grace_seconds", source, context) ?? 0;
        var overdue = RequiredInt(node, "overdue_seconds", "containment.overdue_seconds", source, context) ?? 0;
        if (ritual <= 0) context.Error("TLAW-CFG-141", source, "containment.ritual_hold_seconds", "Ritual hold must be positive.");
        if (grace <= 0) context.Error("TLAW-CFG-142", source, "containment.service_requested_grace_seconds", "Grace period must be positive.");
        if (overdue <= 0) context.Error("TLAW-CFG-143", source, "containment.overdue_seconds", "Overdue duration must be positive.");
        var intervalNode = RequiredMap(node, "interval_by_danger_weight", "containment.interval_by_danger_weight", source, context);
        if (intervalNode is not null) foreach (var (key, value) in Entries(intervalNode))
        {
            var path = Child("containment.interval_by_danger_weight", key);
            var seconds = Int(value, path, source, context) ?? 0;
            if (key is not ("1" or "2" or "3_or_more") || seconds <= 0) context.Error("TLAW-CFG-144", source, path, "Containment intervals require approved keys and positive values.");
            intervals[key] = seconds;
        }

        if (!intervals.Keys.OrderBy(static value => value, StringComparer.Ordinal).SequenceEqual(new[] { "1", "2", "3_or_more" })) context.Error("TLAW-CFG-144", source, "containment.interval_by_danger_weight", "Containment interval keys must be exact.");
        var after = BuildAfterRitual(RequiredMap(node, "after_successful_ritual", "containment.after_successful_ritual", source, context), source, context);
        var incident = BuildIncident(RequiredMap(node, "prototype_incident", "containment.prototype_incident", source, context), source, context);
        var inference = BuildInference(RequiredMap(node, "post_facto_interval_inference", "containment.post_facto_interval_inference", source, context), source, context);
        return new ContainmentConfiguration(unlimited, ritual, grace, overdue, intervals.ToImmutable(), after, incident, inference);
    }

    private static AfterSuccessfulRitualDefinition BuildAfterRitual(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new AfterSuccessfulRitualDefinition(default, false, false, false);
        const string path = "containment.after_successful_ritual";
        Known(node, source, context, path, "return_state", "retain_logs", "retain_danger_weight", "restart_interval_from_current_weight");
        var stateText = RequiredText(node, "return_state", Child(path, "return_state"), source, context);
        var state = Enum.TryParse<ContainmentState>(stateText, false, out var parsed) ? parsed : default;
        if (stateText is null || !Enum.TryParse<ContainmentState>(stateText, false, out _)) context.Error("TLAW-CFG-145", source, Child(path, "return_state"), "Unknown containment state.");
        var retainLogs = RequiredBool(node, "retain_logs", Child(path, "retain_logs"), source, context) ?? false;
        var retainWeight = RequiredBool(node, "retain_danger_weight", Child(path, "retain_danger_weight"), source, context) ?? false;
        var restart = RequiredBool(node, "restart_interval_from_current_weight", Child(path, "restart_interval_from_current_weight"), source, context) ?? false;
        if (!retainLogs || !retainWeight || !restart) context.Error("TLAW-CFG-146", source, path, "Successful rituals must retain logs and danger and restart the interval.");
        return new AfterSuccessfulRitualDefinition(state, retainLogs, retainWeight, restart);
    }

    private static PrototypeIncidentDefinition BuildIncident(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new PrototypeIncidentDefinition(string.Empty, 0, false, false);
        const string path = "containment.prototype_incident";
        Known(node, source, context, path, "type", "duration_seconds", "remains_incident_until_ritual", "repeat_before_resolution");
        var type = RequiredText(node, "type", Child(path, "type"), source, context) ?? string.Empty;
        var duration = RequiredInt(node, "duration_seconds", Child(path, "duration_seconds"), source, context) ?? 0;
        var remains = RequiredBool(node, "remains_incident_until_ritual", Child(path, "remains_incident_until_ritual"), source, context) ?? false;
        var repeat = RequiredBool(node, "repeat_before_resolution", Child(path, "repeat_before_resolution"), source, context) ?? false;
        if (type != "forced_line_pause" || duration <= 0 || !remains || repeat) context.Error("TLAW-CFG-147", source, path, "Prototype incident must match the approved contract.");
        return new PrototypeIncidentDefinition(type, duration, remains, repeat);
    }

    private static PostFactoIntervalInferenceDefinition BuildInference(YamlMappingNode? node, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new PostFactoIntervalInferenceDefinition(false, string.Empty, false);
        const string path = "containment.post_facto_interval_inference";
        Known(node, source, context, path, "allowed", "rationale", "seeded_jitter");
        var allowed = RequiredBool(node, "allowed", Child(path, "allowed"), source, context) ?? false;
        var rationale = RequiredText(node, "rationale", Child(path, "rationale"), source, context) ?? string.Empty;
        var jitter = RequiredBool(node, "seeded_jitter", Child(path, "seeded_jitter"), source, context) ?? false;
        if (!allowed || jitter) context.Error("TLAW-CFG-148", source, path, "Post-facto interval inference must be allowed without jitter.");
        return new PostFactoIntervalInferenceDefinition(allowed, rationale, jitter);
    }

    private static ImmutableArray<string> BuildSuccessPredicate(YamlSequenceNode? node, ConfigurationDocument source, ValidationContext context)
    {
        var values = ImmutableArray.CreateBuilder<string>();
        if (node is null) return values.ToImmutable();
        if (node.Children.Count == 0) context.Error("TLAW-CFG-149", source, "success_predicate.all", "Success predicate cannot be empty.");
        foreach (var (index, item) in node.Children.Select(static (item, index) => (index, item)))
        {
            var path = $"success_predicate.all[{index}]";
            var token = Scalar(item, path, source, context) ?? string.Empty;
            if (token != "quota_by_species_met" && !token.StartsWith("correctly_processed_anomalies_at_least_", StringComparison.Ordinal)) context.Error("TLAW-CFG-149", source, path, "Unknown success predicate token.");
            values.Add(token);
        }

        return values.ToImmutable();
    }

    private static AnomalyCatalog? BuildAnomalies(RawYamlDocument document, ValidationContext context)
    {
        const ConfigurationDocument source = ConfigurationDocument.Anomalies;
        var start = context.ErrorCount;
        var root = document.Root;
        var schemaVersion = RequiredInt(root, "schema_version", "schema_version", source, context);
        if (schemaVersion is null) context.Error("TLAW-CFG-003", source, "schema_version", "Schema version is required.");
        if (schemaVersion is null) return null;
        if (schemaVersion != 2) { context.Error("TLAW-CFG-004", source, "schema_version", "Only schema version 2 is supported."); return null; }
        if (!Has(root, "anomalies")) { context.Error("TLAW-CFG-014", source, "(root)", "This input is not an anomaly document."); return null; }
        Known(root, source, context, "", "schema_version", "anomalies");
        var map = RequiredMap(root, "anomalies", "anomalies", source, context);
        var builder = ImmutableDictionary.CreateBuilder<AnomalyId, AnomalyDefinition>();
        if (map is null) return null;
        if (map.Children.Count == 0) context.Error("TLAW-CFG-201", source, "anomalies", "Anomaly catalog cannot be empty.");
        foreach (var (key, value) in Entries(map))
        {
            var path = Child("anomalies", key);
            var id = AnomalyId.From(ValidateId(key, path, source, context, "TLAW-CFG-202") ?? "invalid");
            var definition = BuildAnomaly(id, value, path, source, context);
            if (!builder.TryAdd(id, definition)) context.Error("TLAW-CFG-202", source, path, "Anomaly identifiers must be unique.");
        }

        return context.ErrorCount == start ? new AnomalyCatalog(builder.ToImmutable()) : null;
    }

    private static AnomalyDefinition BuildAnomaly(AnomalyId id, YamlNode node, string path, ConfigurationDocument source, ValidationContext context)
    {
        var map = Map(node, path, source, context) ?? new YamlMappingNode();
        Known(map, source, context, path, "danger_weight", "instant_clues", "observed_clues", "confirm_test", "processing", "procedure", "wrong_actions");
        var weight = RequiredInt(map, "danger_weight", Child(path, "danger_weight"), source, context) ?? 0;
        if (weight < 0) context.Error("TLAW-CFG-203", source, Child(path, "danger_weight"), "Danger weight cannot be negative.");
        var instant = RequiredStringList(map, "instant_clues", Child(path, "instant_clues"), source, context, "TLAW-CFG-204", false);
        var observed = RequiredStringList(map, "observed_clues", Child(path, "observed_clues"), source, context, "TLAW-CFG-205", false);
        if (!Has(map, "confirm_test")) context.Error("TLAW-CFG-206", source, Child(path, "confirm_test"), "Confirm test is required.");
        var confirm = BuildConfirmTest(RequiredMap(map, "confirm_test", Child(path, "confirm_test"), source, context), Child(path, "confirm_test"), source, context);
        if (!Has(map, "processing")) context.Error("TLAW-CFG-213", source, Child(path, "processing"), "Processing contract is required.");
        var processing = BuildProcessing(RequiredMap(map, "processing", Child(path, "processing"), source, context), Child(path, "processing"), source, context);
        var procedure = BuildProcedure(RequiredMap(map, "procedure", Child(path, "procedure"), source, context), Child(path, "procedure"), source, context);
        var wrong = BuildWrongActions(OptionalMap(map, "wrong_actions", Child(path, "wrong_actions"), source, context), Child(path, "wrong_actions"), source, context);
        if (!processing.RequiredFlags.IsSubsetOf(procedure.GrantsFlags)) context.Error("TLAW-CFG-232", source, path, "Required flags must be granted by the anomaly procedure.");
        return new AnomalyDefinition(id, weight, instant, observed, confirm, processing, procedure, wrong);
    }

    private static ConfirmTestDefinition BuildConfirmTest(YamlMappingNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new ConfirmTestDefinition(null, 0, false, null, string.Empty, ImmutableArray<ItemId>.Empty);
        Known(node, source, context, path, "required_line_noise", "duration_seconds", "continuous", "reset_when_condition_lost", "result", "tools");
        LineNoise? requiredNoise = null;
        if (Has(node, "required_line_noise"))
        {
            var value = RequiredText(node, "required_line_noise", Child(path, "required_line_noise"), source, context);
            if (Enum.TryParse<LineNoise>(value, false, out var parsed)) requiredNoise = parsed;
            else context.Error("TLAW-CFG-211", source, Child(path, "required_line_noise"), "Unknown line noise value.");
        }

        var duration = RequiredInt(node, "duration_seconds", Child(path, "duration_seconds"), source, context) ?? 0;
        if (duration <= 0) context.Error("TLAW-CFG-207", source, Child(path, "duration_seconds"), "Confirm duration must be positive.");
        var continuousValue = RequiredBool(node, "continuous", Child(path, "continuous"), source, context);
        if (continuousValue is null) context.Error("TLAW-CFG-208", source, Child(path, "continuous"), "Continuous must be explicitly boolean.");
        var continuous = continuousValue ?? false;
        var result = RequiredText(node, "result", Child(path, "result"), source, context) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(result)) context.Error("TLAW-CFG-209", source, Child(path, "result"), "Confirm result cannot be blank.");
        bool? reset = null;
        if (Has(node, "reset_when_condition_lost")) reset = RequiredBool(node, "reset_when_condition_lost", Child(path, "reset_when_condition_lost"), source, context);
        if ((requiredNoise is not null && reset is null) || (requiredNoise is null && reset is not null)) context.Error("TLAW-CFG-212", source, Child(path, "reset_when_condition_lost"), "Reset behavior is required exactly for noise-gated tests.");
        if (!Has(node, "tools")) context.Error("TLAW-CFG-210", source, Child(path, "tools"), "Confirm tools list is required.");
        var tools = RequiredItemList(node, "tools", Child(path, "tools"), source, context, "TLAW-CFG-210", allowEmpty: true);
        return new ConfirmTestDefinition(requiredNoise, duration, continuous, reset, result, tools);
    }

    private static ProcessingDefinition BuildProcessing(YamlMappingNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new ProcessingDefinition(ImmutableHashSet<FlagId>.Empty, default, DefaultOutcome(), DefaultOutcome());
        Known(node, source, context, path, "required_flags", "route_without_flags", "on_correct", "on_incorrect");
        var flags = RequiredFlagList(node, "required_flags", Child(path, "required_flags"), source, context, "TLAW-CFG-214", false);
        var routeValue = RequiredText(node, "route_without_flags", Child(path, "route_without_flags"), source, context);
        var route = Enum.TryParse<RouteWithoutFlagsPolicy>(routeValue, false, out var parsedRoute) ? parsedRoute : default;
        if (routeValue is null || !Enum.TryParse<RouteWithoutFlagsPolicy>(routeValue, false, out _)) context.Error("TLAW-CFG-215", source, Child(path, "route_without_flags"), "Unknown route policy.");
        if (!Has(node, "on_correct")) context.Error("TLAW-CFG-216", source, Child(path, "on_correct"), "Correct processing outcome is required.");
        var correct = BuildOutcome(RequiredMap(node, "on_correct", Child(path, "on_correct"), source, context), Child(path, "on_correct"), source, context, false);
        if (!Has(node, "on_incorrect")) context.Error("TLAW-CFG-217", source, Child(path, "on_incorrect"), "Incorrect processing outcome is required.");
        var incorrect = BuildOutcome(RequiredMap(node, "on_incorrect", Child(path, "on_incorrect"), source, context), Child(path, "on_incorrect"), source, context, false);
        return new ProcessingDefinition(flags, route, correct, incorrect);
    }

    private static ProcessingOutcome BuildOutcome(YamlMappingNode? node, string path, ConfigurationDocument source, ValidationContext context, bool allowUnchanged)
    {
        if (node is null) return DefaultOutcome();
        Known(node, source, context, path, "terminal_state", "quota_credit", "correct_anomaly_delta", "effects");
        var terminalText = RequiredText(node, "terminal_state", Child(path, "terminal_state"), source, context);
        var terminal = ParseTerminal(terminalText, Child(path, "terminal_state"), source, context, allowUnchanged, out _);
        var credit = BuildQuotaCredit(RequiredMap(node, "quota_credit", Child(path, "quota_credit"), source, context), Child(path, "quota_credit"), source, context);
        var delta = RequiredInt(node, "correct_anomaly_delta", Child(path, "correct_anomaly_delta"), source, context) ?? 0;
        if (delta < 0) context.Error("TLAW-CFG-222", source, Child(path, "correct_anomaly_delta"), "Correct anomaly delta cannot be negative.");
        if (!Has(node, "effects")) context.Error("TLAW-CFG-223", source, Child(path, "effects"), "Effects list is required.");
        var effects = BuildEffects(RequiredSequence(node, "effects", Child(path, "effects"), source, context), Child(path, "effects"), source, context);
        return new ProcessingOutcome(terminal ?? LogState.PROCESSED, credit, delta, effects);
    }

    private static ProcessingOutcome DefaultOutcome() => new(LogState.PROCESSED, new QuotaCreditDefinition(SpeciesCreditRule.none, 0), 0, ImmutableArray<EffectDefinition>.Empty);

    private static QuotaCreditDefinition BuildQuotaCredit(YamlMappingNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return new QuotaCreditDefinition(SpeciesCreditRule.none, 0);
        Known(node, source, context, path, "species", "units");
        var speciesText = RequiredText(node, "species", Child(path, "species"), source, context);
        var species = Enum.TryParse<SpeciesCreditRule>(speciesText, false, out var parsed) ? parsed : SpeciesCreditRule.none;
        if (speciesText is null || !Enum.TryParse<SpeciesCreditRule>(speciesText, false, out _)) context.Error("TLAW-CFG-219", source, Child(path, "species"), "Unknown species credit rule.");
        var units = RequiredInt(node, "units", Child(path, "units"), source, context) ?? 0;
        if (units < 0) context.Error("TLAW-CFG-220", source, Child(path, "units"), "Credit units cannot be negative.");
        if (species == SpeciesCreditRule.none && units != 0) context.Error("TLAW-CFG-221", source, path, "No-species credit must have zero units.");
        return new QuotaCreditDefinition(species, units);
    }

    private static ImmutableArray<EffectDefinition> BuildEffects(YamlSequenceNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        var effects = ImmutableArray.CreateBuilder<EffectDefinition>();
        if (node is null) return effects.ToImmutable();
        for (var index = 0; index < node.Children.Count; index++)
        {
            var itemPath = $"{path}[{index}]";
            var map = Map(node.Children[index], itemPath, source, context);
            if (map is null) continue;
            Known(map, source, context, itemPath, "type", "event", "duration_seconds", "target");
            var typeText = RequiredText(map, "type", Child(itemPath, "type"), source, context);
            var type = Enum.TryParse<EffectType>(typeText, false, out var parsed) ? parsed : default;
            if (typeText is null || !Enum.TryParse<EffectType>(typeText, false, out _)) context.Error("TLAW-CFG-224", source, Child(itemPath, "type"), "Unknown effect type.");
            var eventId = EffectEventId.From(RequiredId(map, "event", Child(itemPath, "event"), source, context, "TLAW-CFG-225") ?? "invalid");
            int? duration = Has(map, "duration_seconds") ? RequiredInt(map, "duration_seconds", Child(itemPath, "duration_seconds"), source, context) : null;
            var target = Has(map, "target") ? RequiredText(map, "target", Child(itemPath, "target"), source, context) : null;
            var invalidPayload = type switch
            {
                EffectType.time_penalty => duration is null or <= 0,
                EffectType.@lock => duration is null or <= 0 || string.IsNullOrWhiteSpace(target),
                EffectType.miscredit => false,
                _ => true,
            };
            if (invalidPayload) context.Error("TLAW-CFG-226", source, itemPath, "Effect payload does not match its type.");
            effects.Add(new EffectDefinition(type, eventId, duration, target));
        }

        return effects.ToImmutable();
    }

    private static ProcedureDefinition BuildProcedure(YamlMappingNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        var steps = ImmutableArray.CreateBuilder<ProcedureStepDefinition>();
        var flags = ImmutableHashSet.CreateBuilder<FlagId>();
        if (node is null) return new ProcedureDefinition(steps.ToImmutable(), flags.ToImmutable());
        Known(node, source, context, path, "steps", "grants_flags");
        var sequence = RequiredSequence(node, "steps", Child(path, "steps"), source, context);
        if (sequence is not null)
        {
            if (sequence.Children.Count == 0) context.Error("TLAW-CFG-227", source, Child(path, "steps"), "Procedure requires at least one step.");
            for (var index = 0; index < sequence.Children.Count; index++)
            {
                var stepPath = $"{path}.steps[{index}]";
                var map = Map(sequence.Children[index], stepPath, source, context);
                if (map is null) continue;
                Known(map, source, context, stepPath, "item", "consumes", "hold_seconds");
                var item = ItemId.From(RequiredId(map, "item", Child(stepPath, "item"), source, context, "TLAW-CFG-228") ?? "invalid");
                var consumesValue = RequiredBool(map, "consumes", Child(stepPath, "consumes"), source, context);
                if (consumesValue is null) context.Error("TLAW-CFG-229", source, Child(stepPath, "consumes"), "Procedure step consumption must be explicit and boolean.");
                var consumes = consumesValue ?? false;
                int? hold = Has(map, "hold_seconds") ? RequiredInt(map, "hold_seconds", Child(stepPath, "hold_seconds"), source, context) : null;
                if (hold is <= 0) context.Error("TLAW-CFG-230", source, Child(stepPath, "hold_seconds"), "Optional hold duration must be positive.");
                steps.Add(new ProcedureStepDefinition(item, consumes, hold));
            }
        }

        var flagSequence = RequiredSequence(node, "grants_flags", Child(path, "grants_flags"), source, context);
        if (flagSequence is not null)
        {
            if (flagSequence.Children.Count == 0) context.Error("TLAW-CFG-231", source, Child(path, "grants_flags"), "Procedures must grant at least one flag.");
            foreach (var (index, value) in flagSequence.Children.Select(static (value, index) => (index, value)))
            {
                var flagPath = $"{path}.grants_flags[{index}]";
                var flag = FlagId.From(ValidateId(Scalar(value, flagPath, source, context), flagPath, source, context, "TLAW-CFG-231") ?? "invalid");
                if (!flags.Add(flag)) context.Error("TLAW-CFG-231", source, flagPath, "Granted flags must be unique.");
            }
        }

        return new ProcedureDefinition(steps.ToImmutable(), flags.ToImmutable());
    }

    private static ImmutableDictionary<ItemId, WrongActionDefinition> BuildWrongActions(YamlMappingNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        var actions = ImmutableDictionary.CreateBuilder<ItemId, WrongActionDefinition>();
        if (node is null) return actions.ToImmutable();
        foreach (var (key, value) in Entries(node))
        {
            var itemPath = Child(path, key);
            var item = ItemId.From(ValidateId(key, itemPath, source, context, "TLAW-CFG-233") ?? "invalid");
            var map = Map(value, itemPath, source, context);
            if (map is null) continue;
            Known(map, source, context, itemPath, "terminal_state", "effects", "consumes");
            var terminalText = RequiredText(map, "terminal_state", Child(itemPath, "terminal_state"), source, context);
            var terminal = ParseTerminal(terminalText, Child(itemPath, "terminal_state"), source, context, true, out var unchanged);
            var consumesValue = RequiredBool(map, "consumes", Child(itemPath, "consumes"), source, context);
            if (consumesValue is null) context.Error("TLAW-CFG-234", source, Child(itemPath, "consumes"), "Wrong-action consumption must be explicit and boolean.");
            var consumes = consumesValue ?? false;
            var effectErrors = context.ErrorCount;
            var effects = BuildEffects(RequiredSequence(map, "effects", Child(itemPath, "effects"), source, context), Child(itemPath, "effects"), source, context);
            if (context.ErrorCount != effectErrors) context.Error("TLAW-CFG-236", source, Child(itemPath, "effects"), "Wrong-action effects must satisfy the effect contract.");
            if (!actions.TryAdd(item, new WrongActionDefinition(unchanged, terminal, consumes, effects))) context.Error("TLAW-CFG-233", source, itemPath, "Wrong action items must be unique.");
        }

        return actions.ToImmutable();
    }

    private static ImmutableArray<string> RequiredStringList(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context, string code, bool allowEmpty)
    {
        var sequence = RequiredSequence(map, key, path, source, context);
        var values = ImmutableArray.CreateBuilder<string>();
        if (sequence is null) return values.ToImmutable();
        if (!allowEmpty && sequence.Children.Count == 0) context.Error(code, source, path, "List cannot be empty.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (index, item) in sequence.Children.Select(static (item, index) => (index, item)))
        {
            var itemPath = $"{path}[{index}]";
            var value = Scalar(item, itemPath, source, context) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) context.Error(code, source, itemPath, "List entries must be non-blank and unique.");
            values.Add(value);
        }

        return values.ToImmutable();
    }

    private static ImmutableArray<ItemId> RequiredItemList(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context, string code, bool allowEmpty)
    {
        var sequence = RequiredSequence(map, key, path, source, context);
        var values = ImmutableArray.CreateBuilder<ItemId>();
        if (sequence is null) return values.ToImmutable();
        if (!allowEmpty && sequence.Children.Count == 0) context.Error(code, source, path, "List cannot be empty.");
        var seen = new HashSet<ItemId>();
        foreach (var (index, item) in sequence.Children.Select(static (item, index) => (index, item)))
        {
            var itemPath = $"{path}[{index}]";
            var id = ItemId.From(ValidateId(Scalar(item, itemPath, source, context), itemPath, source, context, code) ?? "invalid");
            if (!seen.Add(id)) context.Error(code, source, itemPath, "Item identifiers must be unique.");
            values.Add(id);
        }

        return values.ToImmutable();
    }

    private static ImmutableHashSet<FlagId> RequiredFlagList(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context, string code, bool allowEmpty)
    {
        var sequence = RequiredSequence(map, key, path, source, context);
        var values = ImmutableHashSet.CreateBuilder<FlagId>();
        if (sequence is null) return values.ToImmutable();
        if (!allowEmpty && sequence.Children.Count == 0) context.Error(code, source, path, "List cannot be empty.");
        foreach (var (index, item) in sequence.Children.Select(static (item, index) => (index, item)))
        {
            var itemPath = $"{path}[{index}]";
            var id = FlagId.From(ValidateId(Scalar(item, itemPath, source, context), itemPath, source, context, code) ?? "invalid");
            if (!values.Add(id)) context.Error(code, source, itemPath, "Flag identifiers must be unique.");
        }

        return values.ToImmutable();
    }

    private static void ValidateCrossDocument(ShiftConfiguration shift, AnomalyCatalog catalog, ValidationContext context)
    {
        const ConfigurationDocument source = ConfigurationDocument.CrossDocument;
        foreach (var (index, log) in shift.Manifest.Select(static (log, index) => (index, log)))
        {
            var path = $"manifest[{index}].anomaly";
            if (log.Anomaly is { } anomaly && !catalog.Definitions.ContainsKey(anomaly)) context.Error("TLAW-CFG-302", source, path, "Manifest anomaly must exist in the anomaly catalog.");
            if (!shift.Objectives.Quota.BySpecies.ContainsKey(log.TrueSpecies)) context.Warning("TLAW-CFG-310", source, $"manifest[{index}].true_species", "True species is absent from the quota table.");
            if (!shift.Objectives.Quota.BySpecies.ContainsKey(log.DeclaredSpecies)) context.Warning("TLAW-CFG-310", source, $"manifest[{index}].declared_species", "Declared species is absent from the quota table.");
        }

        foreach (var definition in catalog.Definitions.Values)
        {
            var basePath = $"anomalies.{definition.Id}";
            foreach (var (index, step) in definition.Procedure.Steps.Select(static (step, index) => (index, step)))
            {
                var path = $"{basePath}.procedure.steps[{index}]";
                if (!TryItemKind(shift.Resources, step.Item, out var consumable)) context.Error("TLAW-CFG-303", source, Child(path, "item"), "Procedure item must be declared in resources.");
                else if (step.Consumes && !consumable) context.Error("TLAW-CFG-304", source, path, "Consuming procedure steps require consumable items.");
                else if (!step.Consumes && consumable) context.Error("TLAW-CFG-305", source, path, "Reusable procedure steps require reusable items.");
            }

            foreach (var (index, tool) in definition.ConfirmTest.Tools.Select(static (tool, index) => (index, tool)))
            {
                var path = $"{basePath}.confirm_test.tools[{index}]";
                if (!TryItemKind(shift.Resources, tool, out var consumable)) context.Error("TLAW-CFG-303", source, path, "Confirm tool must be declared in resources.");
                else if (consumable) context.Error("TLAW-CFG-306", source, path, "Confirm tools must be reusable.");
            }

            foreach (var action in definition.WrongActions)
            {
                var path = $"{basePath}.wrong_actions.{action.Key}";
                if (!TryItemKind(shift.Resources, action.Key, out var consumable)) context.Error("TLAW-CFG-303", source, path, "Wrong-action item must be declared in resources.");
                else if (action.Value.Consumes && !consumable) context.Error("TLAW-CFG-307", source, path, "Consuming wrong actions require consumable items.");
            }

            if (definition.ConfirmTest.RequiredLineNoise is not null)
            {
                if (definition.ConfirmTest.DurationSeconds != shift.LineNoise.PenitentConfirmRequiresContinuousQuietSeconds) context.Error("TLAW-CFG-308", source, $"{basePath}.confirm_test.duration_seconds", "Noise-gated confirm duration must equal the configured quiet duration.");
                if (definition.ConfirmTest.ResetWhenConditionLost != shift.LineNoise.ResetTestProgressWhenLoud) context.Error("TLAW-CFG-309", source, $"{basePath}.confirm_test.reset_when_condition_lost", "Noise-gated reset behavior must match line-noise configuration.");
            }
        }
    }

    private static bool TryItemKind(ResourcesConfiguration resources, ItemId item, out bool consumable)
    {
        if (resources.Consumable.ContainsKey(item)) { consumable = true; return true; }
        if (resources.Reusable.Contains(item)) { consumable = false; return true; }
        consumable = false;
        return false;
    }

    private static LogState? ParseTerminal(string? value, string path, ConfigurationDocument source, ValidationContext context, bool allowUnchanged, out bool unchanged)
    {
        unchanged = allowUnchanged && string.Equals(value, "unchanged", StringComparison.Ordinal);
        if (unchanged) return null;
        if (Enum.TryParse<LogState>(value, false, out var state) && state is LogState.PROCESSED or LogState.HELD_WRITTEN_OFF) return state;
        context.Error(allowUnchanged ? "TLAW-CFG-235" : "TLAW-CFG-218", source, path, "Outcome state must be terminal.");
        return null;
    }

    private static NodeId ToNodeId(string value) => value switch
    {
        "feed_gate" => NodeId.FEED_GATE,
        "intake" => NodeId.INTAKE,
        "procedure" => NodeId.PROCEDURE,
        "saw_queue" => NodeId.SAW_QUEUE,
        "saw" => NodeId.SAW,
        "containment" => NodeId.CONTAINMENT,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static bool Has(YamlMappingNode map, string key) => Find(map, key) is not null;
    private static YamlNode? Find(YamlMappingNode map, string key) => map.Children.FirstOrDefault(pair => pair.Key is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.Ordinal)).Value;
    private static string? TextValue(YamlNode? node) => node is YamlScalarNode scalar ? scalar.Value : null;
    private static string Child(string path, string key) => string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
    private static IEnumerable<(string Key, YamlNode Value)> Entries(YamlMappingNode map) => map.Children.Where(static pair => pair.Key is YamlScalarNode).Select(static pair => (((YamlScalarNode)pair.Key).Value ?? string.Empty, pair.Value));

    private static void Known(YamlMappingNode map, ConfigurationDocument source, ValidationContext context, string path, params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var (key, _) in Entries(map)) if (!set.Contains(key)) context.Error("TLAW-CFG-006", source, Child(path, key), "Unknown YAML property.");
    }

    private static YamlNode? Required(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context)
    {
        var node = Find(map, key);
        if (node is null) context.Error("TLAW-CFG-007", source, path, "Required YAML property is missing.");
        return node;
    }

    private static YamlMappingNode? RequiredMap(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context) => Map(Required(map, key, path, source, context), path, source, context);
    private static YamlMappingNode? OptionalMap(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context) => Has(map, key) ? Map(Find(map, key), path, source, context) : null;
    private static YamlSequenceNode? RequiredSequence(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context) => Sequence(Required(map, key, path, source, context), path, source, context);
    private static string? RequiredText(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context) => Scalar(Required(map, key, path, source, context), path, source, context);
    private static int? RequiredInt(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context) => Int(Required(map, key, path, source, context), path, source, context);
    private static bool? RequiredBool(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context) => Bool(Required(map, key, path, source, context), path, source, context);
    private static string? RequiredId(YamlMappingNode map, string key, string path, ConfigurationDocument source, ValidationContext context, string code) => ValidateId(RequiredText(map, key, path, source, context), path, source, context, code);

    private static YamlMappingNode? Map(YamlNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return null;
        if (node is YamlMappingNode map) return map;
        context.Error("TLAW-CFG-008", source, path, "Expected a mapping.");
        return null;
    }

    private static YamlSequenceNode? Sequence(YamlNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return null;
        if (node is YamlSequenceNode sequence) return sequence;
        context.Error("TLAW-CFG-008", source, path, "Expected a sequence.");
        return null;
    }

    private static string? Scalar(YamlNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        if (node is null) return null;
        if (node is YamlScalarNode scalar) return scalar.Value;
        context.Error("TLAW-CFG-008", source, path, "Expected a scalar.");
        return null;
    }

    private static int? Int(YamlNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        var scalar = Scalar(node, path, source, context);
        if (scalar is null) return null;
        if (int.TryParse(scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;
        context.Error("TLAW-CFG-008", source, path, "Expected an integer scalar.");
        return null;
    }

    private static bool? Bool(YamlNode? node, string path, ConfigurationDocument source, ValidationContext context)
    {
        var scalar = Scalar(node, path, source, context);
        if (scalar is null) return null;
        if (bool.TryParse(scalar, out var value)) return value;
        context.Error("TLAW-CFG-008", source, path, "Expected a boolean scalar.");
        return null;
    }

    private static string? ValidateId(string? value, string path, ConfigurationDocument source, ValidationContext context, string semanticCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            context.Error("TLAW-CFG-012", source, path, "Identifier cannot be blank.");
            if (!string.Equals(semanticCode, "TLAW-CFG-012", StringComparison.Ordinal)) context.Error(semanticCode, source, path, "Identifier is invalid.");
            return null;
        }

        if (HasSurroundingWhitespace(value))
        {
            context.Error("TLAW-CFG-013", source, path, "Identifier cannot have surrounding whitespace.");
            if (!string.Equals(semanticCode, "TLAW-CFG-013", StringComparison.Ordinal)) context.Error(semanticCode, source, path, "Identifier is invalid.");
            return null;
        }

        return value;
    }

    private static bool HasSurroundingWhitespace(string value) => value.Length != value.Trim().Length;

    private sealed class ValidationContext(List<ConfigurationDiagnostic> diagnostics)
    {
        internal int ErrorCount => diagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        internal void Error(string code, ConfigurationDocument document, string path, string message) => diagnostics.Add(new ConfigurationDiagnostic(code, DiagnosticSeverity.Error, document, path, message));
        internal void Warning(string code, ConfigurationDocument document, string path, string message) => diagnostics.Add(new ConfigurationDiagnostic(code, DiagnosticSeverity.Warning, document, path, message));
    }
}
