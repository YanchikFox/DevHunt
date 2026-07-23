using DevHunt.CoreApi.Services.Moderation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public sealed class ProfanityFilterServiceTests
{
    private readonly ProfanityFilterService _service = new(NullLogger<ProfanityFilterService>.Instance);

    [Theory]
    [InlineData("Hello! How can I assist you today?")]
    [InlineData("assistant assistance assignment associate assessment")]
    [InlineData("class classic pass passion compass grass glass massive bass")]
    [InlineData("asset assemble assume assert password")]
    [InlineData("Scunthorpe Dickinson Cockburn pussywillow")]
    [InlineData("страхуй страхуется бляха бляшка хулиган")]
    public void CensorText_ShouldNotCensorProfanitySubstringInsideCleanWords(string text)
    {
        _service.CensorText(text).Should().Be(text);
        _service.CheckField("Text", text).Should().BeNull();
    }

    [Theory]
    [InlineData("ass")]
    [InlineData("asshole")]
    [InlineData("f.u.c.k")]
    [InlineData("motherfucker")]
    [InlineData("бля")]
    [InlineData("хуй")]
    public void CensorText_ShouldCensorStandaloneProfanityTokens(string text)
    {
        _service.CensorText(text).Should().Contain("******");
        _service.CheckField("Text", text).Should().NotBeNull();
    }
}
