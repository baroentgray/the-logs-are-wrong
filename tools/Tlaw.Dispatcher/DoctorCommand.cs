using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tlaw.Dispatcher;

/// <summary>Readiness reporting only. This command does not launch an agent task.</summary>
public static class DoctorCommand
{
    public static int Run(string[] args, TextWriter standardOutput, TextWriter standardError) => Run(args, standardOutput, standardError, new SystemDoctorProbe());
    internal static int RunForTesting(string[] args, TextWriter standardOutput, TextWriter standardError, IDoctorProbe probe) => Run(args, standardOutput, standardError, probe);

    private static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, IDoctorProbe probe)
    {
        try
        {
            var options = DoctorOptions.Parse(args);
            var config = DoctorConfig.Parse(TaskPacketCommand.ReadUtf8(options.ConfigPath));
            var reports = config.Agents.Select(agent => DoctorReport.Create(agent, options.Live, probe)).ToList();
            var doctor = DoctorJson.Write(reports);
            var snapshot = DoctorJson.WriteSnapshot(reports);
            _ = AgentSnapshotDocument.Parse(snapshot);
            TaskPacketCommand.WriteAtomically(options.OutputPath, doctor);
            TaskPacketCommand.WriteAtomically(options.AgentsOutputPath, snapshot);
            standardOutput.WriteLine("DOCTOR: readiness snapshot published");
            return 0;
        }
        catch (DoctorCommandException exception) { standardError.WriteLine($"FAIL: {exception.Message}"); return 1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or DecoderFallbackException or InvalidDataException or JsonException or HttpRequestException or TaskCanceledException) { standardError.WriteLine($"FAIL: {exception.Message}"); return 1; }
        catch (Exception) { standardError.WriteLine("FAIL: doctor operation failed unexpectedly."); return 1; }
    }
}

public sealed class DoctorCommandException(string message) : Exception(message);
internal sealed record DoctorOptions(string ConfigPath, string OutputPath, string AgentsOutputPath, bool Live)
{
    internal static DoctorOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count is not (7 or 8) || args[0] != "doctor") throw new DoctorCommandException("Doctor requires --config, --output, and --agents-output; --live is optional.");
        var live = args.Count == 8;
        if (live && args[7] != "--live") throw new DoctorCommandException("Doctor received an unknown option.");
        var values = new Dictionary<string,string>(StringComparer.Ordinal);
        for (var index=1; index<7; index+=2) if (string.IsNullOrWhiteSpace(args[index+1]) || !values.TryAdd(args[index],args[index+1])) throw new DoctorCommandException("Doctor received a duplicate or empty option.");
        if (values.Count != 3 || !values.ContainsKey("--config") || !values.ContainsKey("--output") || !values.ContainsKey("--agents-output")) throw new DoctorCommandException("Doctor requires only --config, --output, and --agents-output.");
        return new(Path.GetFullPath(values["--config"]),Path.GetFullPath(values["--output"]),Path.GetFullPath(values["--agents-output"]),live);
    }
}

internal sealed record DoctorAdapter(string Agent, string Mode, bool Enabled, string? Executable, Uri? Endpoint);
internal sealed record DoctorConfig(IReadOnlyList<DoctorAdapter> Agents)
{
    internal static DoctorConfig Parse(string json)
    {
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        if(root.ValueKind!=JsonValueKind.Object || root.EnumerateObject().Any(p=>p.Name is not ("schema" or "agents")) || !root.TryGetProperty("schema",out var schema) || schema.GetString()!="tlaw.dispatcher-adapters/v1" || !root.TryGetProperty("agents",out var entries)||entries.ValueKind!=JsonValueKind.Array) throw new DoctorCommandException("Doctor adapter config must be closed tlaw.dispatcher-adapters/v1 JSON.");
        var names=new HashSet<string>(StringComparer.Ordinal);var result=new List<DoctorAdapter>();
        foreach(var entry in entries.EnumerateArray())
        {
            if(entry.ValueKind!=JsonValueKind.Object)throw new DoctorCommandException("Doctor adapter entry must be an object.");
            var allowed=new HashSet<string>(["agent","mode","enabled","executable","endpoint"],StringComparer.Ordinal);var actual=entry.EnumerateObject().ToList();if(actual.Any(p=>!allowed.Contains(p.Name))||actual.GroupBy(p=>p.Name).Any(g=>g.Count()>1))throw new DoctorCommandException("Doctor adapter entry contains an unknown or duplicate property.");
            var agent=S(entry,"agent");if(agent is not("codex" or "claude" or "grok" or "local")||!names.Add(agent))throw new DoctorCommandException("Doctor config must use each closed agent name exactly once.");
            var mode=S(entry,"mode");if(mode is not("cli" or "api" or "local_api" or "manual_packet"))throw new DoctorCommandException("Doctor adapter mode is not supported.");
            if(!entry.TryGetProperty("enabled",out var enabled)||enabled.ValueKind is not(JsonValueKind.True or JsonValueKind.False))throw new DoctorCommandException("Doctor adapter enabled must be boolean.");
            var executable=entry.TryGetProperty("executable",out var e)&&e.ValueKind==JsonValueKind.String?e.GetString():null;var endpoint=entry.TryGetProperty("endpoint",out var p)&&p.ValueKind==JsonValueKind.String?ParseEndpoint(p.GetString()!):null;
            if(mode=="cli"&&string.IsNullOrWhiteSpace(executable)||mode=="local_api"&&endpoint is null||mode is "api" or "manual_packet" && (executable is not null||endpoint is not null))throw new DoctorCommandException("Doctor adapter fields do not match its closed mode.");
            if(executable is not null&&!Regex.IsMatch(executable,"^[A-Za-z0-9._-]+(?:\\.exe)?$",RegexOptions.CultureInvariant))throw new DoctorCommandException("Doctor executable must be a bare executable name; shell syntax is forbidden.");
            result.Add(new DoctorAdapter(agent,mode,enabled.GetBoolean(),executable,endpoint));
        }
        if(names.Count!=4)throw new DoctorCommandException("Doctor config must contain exactly codex, claude, grok, and local.");return new(result);
    }
    private static string S(JsonElement e,string n)=>e.TryGetProperty(n,out var p)&&p.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(p.GetString())?p.GetString()!:throw new DoctorCommandException($"Doctor adapter property '{n}' must be a non-empty string.");
    private static Uri ParseEndpoint(string value)
    {
        try { return LocalLmStudioEndpoint.Parse(value).BaseUri; }
        catch (LocalWorkerCommandException exception) { throw new DoctorCommandException(exception.Message); }
    }
}

internal interface IDoctorProbe { bool ExecutableExists(string executable); DoctorProbeResult ProbeGrok(string executable); DoctorProbeResult ProbeLocal(Uri endpoint); }
internal sealed record DoctorProbeResult(string Stage,string Diagnostic);
internal sealed class SystemDoctorProbe : IDoctorProbe
{
    private readonly HttpMessageHandler? _handler;
    internal SystemDoctorProbe(HttpMessageHandler handler) => _handler = handler;
    public SystemDoctorProbe() { }
    public bool ExecutableExists(string executable) => Resolve(executable) is not null;
    public DoctorProbeResult ProbeGrok(string executable)
    {
        var temp=Path.Combine(Path.GetTempPath(),"tlaw-doctor-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try { using var process=Process.Start(new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=temp,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"-p","Reply only with one JSON object: {\\\"ready\\\":true}.","--output-format","streaming-json"}});if(process is null)return new("DEGRADED","process did not start");if(!process.WaitForExit(5000)){process.Kill(true);return new("DEGRADED","bounded timeout");}var text=process.StandardOutput.ReadToEnd();using var _=JsonDocument.Parse(text);return process.ExitCode==0?new("NON_INTERACTIVE_READY","structured probe accepted"):new("AUTH_REQUIRED","provider command rejected readiness probe"); }
        catch(JsonException){return new("DEGRADED","malformed structured provider response");}catch(Exception){return new("DEGRADED","provider probe failed");}finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
    }
    public DoctorProbeResult ProbeLocal(Uri endpoint)
    {
        try
        {
            using var owned = _handler is null ? new SocketsHttpHandler { UseProxy = false } : null;
            using var client = new HttpClient(_handler ?? owned!, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(5) };
            using var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "/api/v1/models"))).GetAwaiter().GetResult();
            var bytes = LocalLmStudioResponseBody.Read(response.Content, LocalLmStudioResponseBody.MaximumModelListBytes);
            if (!response.IsSuccessStatusCode) return response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ? new("AUTH_REQUIRED", "model-list authentication required") : new("DEGRADED", $"model-list status {(int)response.StatusCode}");
            return LocalLmStudioModelList.ContainsUsableLlm(bytes) ? new("LOCAL_LLM_AVAILABLE", "usable LLM listed") : new("DEGRADED", "no usable LLM listed or malformed model list");
        }
        catch (LocalLmStudioResponseBodyLimitException) { return new("DEGRADED", "model-list response exceeds closed byte limit"); }
        catch (TaskCanceledException) { return new("DEGRADED", "bounded model-list timeout"); }
        catch (HttpRequestException) { return new("DEGRADED", "model-list request failed"); }
    }
    private static string? Resolve(string executable){var path=Environment.GetEnvironmentVariable("PATH")??string.Empty;return path.Split(Path.PathSeparator).Select(x=>Path.Combine(x,executable)).FirstOrDefault(File.Exists);}
}

internal sealed record DoctorReport(string Agent,string Mode,bool Enabled,string Identity,string Stage,string Availability,string Diagnostic)
{
    internal static DoctorReport Create(DoctorAdapter adapter,bool live,IDoctorProbe probe)
    {
        if(!adapter.Enabled)return new(adapter.Agent,adapter.Mode,false,adapter.Executable??adapter.Endpoint!.ToString(),"OFFLINE","OFFLINE","disabled");
        if(adapter.Mode is "api" or "manual_packet")return new(adapter.Agent,adapter.Mode,true,"configured","UNKNOWN","UNKNOWN","configuration only; no task launch");
        if(adapter.Mode=="local_api")
        {
            var localResult = live ? probe.ProbeLocal(adapter.Endpoint!) : new DoctorProbeResult("CLI_AVAILABLE", "loopback-only endpoint");
            var availability = localResult.Stage == "LOCAL_LLM_AVAILABLE" ? "AVAILABLE" : localResult.Stage == "CLI_AVAILABLE" ? "UNKNOWN" : "DEGRADED";
            return new(adapter.Agent,adapter.Mode,true,adapter.Endpoint!.GetLeftPart(UriPartial.Authority),localResult.Stage,availability,localResult.Diagnostic);
        }
        if(!probe.ExecutableExists(adapter.Executable!))return new(adapter.Agent,adapter.Mode,true,adapter.Executable!,"CLI_MISSING","OFFLINE","executable not found");
        var result=live&&adapter.Agent=="grok"?probe.ProbeGrok(adapter.Executable!):new DoctorProbeResult("CLI_AVAILABLE","no live provider request");
        return new(adapter.Agent,adapter.Mode,true,adapter.Executable!,result.Stage,result.Stage=="NON_INTERACTIVE_READY"?"AVAILABLE":result.Stage=="CLI_AVAILABLE"?"UNKNOWN":"DEGRADED",result.Diagnostic);
    }
}
internal static class DoctorJson
{
    internal static string Write(IReadOnlyList<DoctorReport> reports)=>JsonSerializer.Serialize(new{schema="tlaw.dispatcher-doctor/v1",agents=reports.Select(r=>new{agent=r.Agent,mode=r.Mode,enabled=r.Enabled,identity=r.Identity,probe_stage=r.Stage,readiness=r.Stage,effective_availability=r.Availability,diagnostic=r.Diagnostic})},new JsonSerializerOptions{WriteIndented=true}).Replace("\r\n","\n",StringComparison.Ordinal)+"\n";
    internal static string WriteSnapshot(IReadOnlyList<DoctorReport> reports)=>JsonSerializer.Serialize(new{schema="tlaw.dispatcher-agent-snapshot/v1",agents=reports.Select(r=>new{agent=r.Agent,capabilities=new[]{"repository_read"},availability=r.Availability})},new JsonSerializerOptions{WriteIndented=true}).Replace("\r\n","\n",StringComparison.Ordinal)+"\n";
}
