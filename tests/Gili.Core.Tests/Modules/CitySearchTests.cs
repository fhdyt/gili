using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

/// <summary>F-RIGHT-05: the Weather city search (design 4c).</summary>
public sealed class CitySearchTests
{
    [Fact]
    public void Reads_open_meteo_results_in_order()
    {
        const string json = """
            {"results":[
              {"id":1642911,"name":"Jakarta","latitude":-6.21462,"longitude":106.84513,"country":"Indonesia","admin1":"Jakarta"},
              {"id":4250542,"name":"Springfield","latitude":39.80172,"longitude":-89.64371,"country":"United States","admin1":"Illinois"},
              {"id":9,"name":"Nowhere"}
            ],"generationtime_ms":0.5}
            """;
        var results = CitySearch.Parse(json);
        Assert.Equal(["Jakarta, Indonesia", "Springfield, Illinois, United States"], results.Select(r => r.Display));
        Assert.Equal((-6.21462, 106.84513), (results[0].Latitude, results[0].Longitude));
    }

    [Theory]
    [InlineData("{\"generationtime_ms\":0.3}")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void No_results_for_an_empty_or_odd_answer(string json) => Assert.Empty(CitySearch.Parse(json));

    [Fact]
    public void Only_the_typed_name_is_sent()
    {
        Assert.Equal("https://geocoding-api.open-meteo.com/v1/search?name=S%C3%A3o%20Paulo&count=5&language=en&format=json", CitySearch.Url(" São Paulo "));
        Assert.False(CitySearch.IsQuery(" a "));
        Assert.True(CitySearch.IsQuery("Bo"));
    }
}
