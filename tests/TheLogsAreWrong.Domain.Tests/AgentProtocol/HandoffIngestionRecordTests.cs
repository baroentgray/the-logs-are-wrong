using Tlaw.Dispatcher;

namespace TheLogsAreWrong.Domain.Tests.AgentProtocol;

public sealed class HandoffIngestionRecordTests
{
    [Fact] public void Valid_record_is_accepted() => HandoffIngestionJson.Validate(Valid);
    [Theory]
    [InlineData("\"handoff_sha256\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"", "\"handoff_sha256\": \"ABC\"")]
    [InlineData("\"decision\": \"reassign\"", "\"decision\": \"human\"")]
    [InlineData("\"lease_status\": \"active\"", "\"lease_status\": \"expired\"")]
    [InlineData("\"next_state\": \"todo\",", "\"unknown\": \"x\",\"next_state\": \"todo\",")]
    [InlineData("\"branch\": \"task/x\",", "")]
    [InlineData("\"blocked\":false", "\"blocked\":\"false\"")]
    public void Invalid_record_contracts_fail_closed(string find, string replace) => Assert.Throws<HandoffIngestionException>(() => HandoffIngestionJson.Validate(Valid.Replace(find, replace, StringComparison.Ordinal)));
    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{} {}")]
    public void Malformed_or_nonobject_records_fail_closed(string value) => Assert.ThrowsAny<Exception>(() => HandoffIngestionJson.Validate(value));
    [Theory]
    [InlineData("timeout")]
    [InlineData("quota_exhaustion")]
    [InlineData("manual_cancel")]
    public void Closed_reason_options_accept_only_exact_values(string reason)
    {
        var args = new[] { "ingest-handoff", "--task", "a", "--handoff", "b", "--lease-store", Path.GetFullPath("store"), "--reason", reason, "--output", "c" };
        Assert.Equal(reason, HandoffIngestionOptions.Parse(args).Reason);
    }
    [Theory]
    [InlineData("completion")]
    [InlineData("TIMEOUT")]
    [InlineData("unknown")]
    public void Unsupported_reason_fails(string reason) => Assert.Throws<HandoffIngestionException>(() => HandoffIngestionOptions.Parse(["ingest-handoff", "--task", "a", "--handoff", "b", "--lease-store", Path.GetFullPath("store"), "--reason", reason, "--output", "c"]));
    private const string Valid = "{\n  \"schema\": \"tlaw.dispatcher-handoff-ingestion/v1\",\n  \"task_id\": \"BAR-39\",\n  \"source_id\": \"BAR-26\",\n  \"claimed_by\": \"codex\",\n  \"claim_id\": \"0123456789abcdef0123456789abcdef\",\n  \"handoff_sha256\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\n  \"handoff_status\": \"ready\",\n  \"head_sha\": \"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\n  \"branch\": \"task/x\",\n  \"lease_status\": \"active\",\n  \"lease_action\": \"release_required\",\n  \"release_reason\": \"manual_cancel\",\n  \"decision\": \"reassign\",\n  \"next_state\": \"todo\",\n  \"blocked\":false\n}\n";
}
