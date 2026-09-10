using System.Linq;
using System.Text.Json;
using AniVault.Metadata.Providers;

namespace AniVault.Tests;

public class JsonPathTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Selects_Nested_Property()
    {
        var root = Parse("""{ "data": { "title": "Frieren" } }""");
        Assert.Equal("Frieren", JsonPath.SelectString(root, "data.title"));
    }

    [Fact]
    public void Selects_Array_Index_And_Deeper()
    {
        var root = Parse("""{ "results": [ { "name": "a" }, { "name": "b" } ] }""");
        Assert.Equal("b", JsonPath.SelectString(root, "results[1].name"));
    }

    [Fact]
    public void Empty_Path_Returns_The_Element_Itself()
    {
        var root = Parse("\"hello\"");
        Assert.Equal("hello", JsonPath.SelectString(root, ""));
        Assert.Equal("hello", JsonPath.SelectString(root, null));
    }

    [Fact]
    public void Missing_Path_Returns_Null_Not_Throw()
    {
        var root = Parse("""{ "a": 1 }""");
        Assert.Null(JsonPath.SelectString(root, "a.b.c"));
        Assert.Null(JsonPath.SelectInt(root, "missing"));
        Assert.Empty(JsonPath.SelectArray(root, "not.an.array"));
    }

    [Fact]
    public void Reads_Numbers_From_Strings_And_Numbers()
    {
        var root = Parse("""{ "n": 7, "s": "42", "score": "82.5" }""");
        Assert.Equal(7, JsonPath.SelectInt(root, "n"));
        Assert.Equal(42, JsonPath.SelectInt(root, "s"));
        Assert.Equal(82.5, JsonPath.SelectDouble(root, "score"));
    }

    [Fact]
    public void SelectArray_Enumerates_Items()
    {
        var root = Parse("""{ "genres": ["Action", "Drama"] }""");
        var names = JsonPath.SelectArray(root, "genres").Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "Action", "Drama" }, names);
    }

    [Theory]
    [InlineData("2023-09-29", 2023)]
    [InlineData("Fall 2016", 2016)]
    [InlineData("no year here", null)]
    public void YearFrom_Extracts_A_Plausible_Year(string input, int? expected)
    {
        Assert.Equal(expected, JsonPath.YearFrom(input));
    }
}
