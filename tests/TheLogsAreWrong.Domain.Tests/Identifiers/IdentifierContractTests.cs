using TheLogsAreWrong.Domain.Identifiers;

namespace TheLogsAreWrong.Domain.Tests.Identifiers;

public sealed class IdentifierContractTests
{
    [Fact]
    public void LogId_Preserves_exact_value_and_uses_ordinal_case_sensitive_equality()
    {
        var lower = LogId.From("pine");
        var upper = LogId.From("PINE");

        Assert.Equal("pine", lower.Value);
        Assert.NotEqual(lower, upper);
        Assert.Equal(" x ", LogId.From(" x ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void String_ids_reject_blank_values(string value)
    {
        Assert.Throws<ArgumentException>(() => LogId.From(value));
        Assert.False(LogId.TryFrom(value, out _));
    }

    [Fact]
    public void String_ids_detect_default_and_never_convert_implicitly()
    {
        LogId id = default;

        Assert.True(id.IsDefault);
        Assert.Null(id.Value);
        Assert.Equal(string.Empty, id.ToString());
    }

    [Fact]
    public void String_ids_are_dictionary_safe_without_case_folding()
    {
        var values = new Dictionary<ItemId, int>
        {
            [ItemId.From("holy_water")] = 1,
            [ItemId.From("HOLY_WATER")] = 2,
        };

        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void Every_string_identifier_uses_the_uniform_non_blank_contract()
    {
        Assert.False(ShiftId.TryFrom(null, out _));
        Assert.False(LogId.TryFrom("", out _));
        Assert.False(SpeciesId.TryFrom(" ", out _));
        Assert.False(AnomalyId.TryFrom("\t", out _));
        Assert.False(FlagId.TryFrom("\r\n", out _));
        Assert.False(ItemId.TryFrom("", out _));
        Assert.False(ProfileId.TryFrom("", out _));
        Assert.False(EffectEventId.TryFrom("", out _));
        Assert.False(IntentId.TryFrom("", out _));
        Assert.False(ActorId.TryFrom("", out _));
        Assert.False(EventId.TryFrom("", out _));
        Assert.Equal(" exact ", ShiftId.From(" exact ").Value);
    }
}
