using RequestsManagement.Api.Http;

namespace RequestsManagement.UnitTests.Http;

public class EntityTagsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_header_is_reported_as_missing(string? header) =>
        Assert.Equal(IfMatchStatus.Missing, EntityTags.ParseIfMatch(header).Status);

    [Theory]
    [InlineData("*")]                                   // "overwrite anything" – exactly what we must prevent
    [InlineData("W/\"AAAAAAAAB9E=\"")]                  // weak tag
    [InlineData("\"AAAAAAAAB9E=\", \"AAAAAAAAB9I=\"")]  // more than one tag
    [InlineData("\"not base64\"")]
    [InlineData("\"AAAA\"")]                            // wrong length
    public void Unusable_header_is_invalid(string header) =>
        Assert.Equal(IfMatchStatus.Invalid, EntityTags.ParseIfMatch(header).Status);

    [Theory]
    [InlineData("\"AAAAAAAAB9E=\"")]
    [InlineData("AAAAAAAAB9E=")]
    public void Single_strong_tag_is_parsed(string header)
    {
        var result = EntityTags.ParseIfMatch(header);

        Assert.Equal(IfMatchStatus.Valid, result.Status);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 }, result.Version);
    }

    [Fact]
    public void Format_quotes_the_version() =>
        Assert.Equal("\"AAAAAAAAB9E=\"", EntityTags.Format("AAAAAAAAB9E="));
}

public class UserHeaderTests
{
    [Theory]
    [InlineData("%D7%93%D7%A0%D7%94", "דנה")]   // encodeURIComponent("דנה")
    [InlineData("Dana", "Dana")]
    [InlineData("  Dana  ", "Dana")]
    [InlineData(null, "")]
    [InlineData("100%", "100%")]               // not valid encoding: kept as is
    public void Decodes_url_encoded_names(string? header, string expected) =>
        Assert.Equal(expected, UserHeader.Decode(header));
}
