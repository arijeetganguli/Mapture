using Mapture.Compatibility;

namespace Mapture.CompatibilityTests;

public class Source
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public InnerSource? Inner { get; set; }
}

public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public InnerDest? Inner { get; set; }
}

public class InnerSource
{
    public string Value { get; set; } = "";
}

public class InnerDest
{
    public string Value { get; set; } = "";
}

public class CompatibilityProfile : Profile
{
    public CompatibilityProfile()
    {
        CreateMap<Source, Destination>();
        CreateMap<InnerSource, InnerDest>();
    }
}

public class CompatibilityModeTests
{
    [Fact]
    public void CompatibleConfiguration_MapsCorrectly()
    {
        var config = CompatibilityExtensions.CreateCompatibleConfiguration(typeof(CompatibilityProfile).Assembly);
        var mapper = config.CreateMapper();

        var source = new Source
        {
            Id = 1,
            Name = "Test",
            Description = "Desc",
            Tags = new List<string> { "a", "b" },
            Inner = new InnerSource { Value = "inner" }
        };

        var dest = mapper.Map<Source, Destination>(source);

        Assert.Equal(1, dest.Id);
        Assert.Equal("Test", dest.Name);
        Assert.Equal("Desc", dest.Description);
        Assert.Equal(2, dest.Tags.Count);
        Assert.NotNull(dest.Inner);
        Assert.Equal("inner", dest.Inner!.Value);
    }

    [Fact]
    public void CompatibleConfiguration_NullHandling()
    {
        var config = CompatibilityExtensions.CreateCompatibleConfiguration(cfg =>
        {
            cfg.CreateMap<Source, Destination>();
        });

        var mapper = config.CreateMapper();
        var source = new Source { Id = 1, Description = null, Inner = null };

        var dest = mapper.Map<Source, Destination>(source);

        Assert.Null(dest.Description);
        Assert.Null(dest.Inner);
    }

    [Fact]
    public void CompatibleConfiguration_EmptyCollection()
    {
        var config = CompatibilityExtensions.CreateCompatibleConfiguration(cfg =>
        {
            cfg.CreateMap<Source, Destination>();
        });

        var mapper = config.CreateMapper();
        var source = new Source { Id = 1, Tags = new List<string>() };

        var dest = mapper.Map<Source, Destination>(source);

        Assert.NotNull(dest.Tags);
        Assert.Empty(dest.Tags);
    }
}
