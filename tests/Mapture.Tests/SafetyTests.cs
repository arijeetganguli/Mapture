namespace Mapture.Tests;

public class SafetyTests
{
    [Fact]
    public void CycleDetection_DoesNotStackOverflow()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SelfReferencing, SelfReferencingDto>();
        }, new MaptureOptions { EnableCycleDetection = true, MaxDepth = 5 });

        var mapper = config.CreateMapper();

        var entity = new SelfReferencing { Id = 1 };
        entity.Parent = entity; // Circular reference

        var dto = mapper.Map<SelfReferencing, SelfReferencingDto>(entity);

        Assert.Equal(1, dto.Id);
        Assert.Null(dto.Parent); // Cycle broken
    }

    [Fact]
    public void MaxDepth_EnforcesLimit()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SelfReferencing, SelfReferencingDto>();
        }, new MaptureOptions { MaxDepth = 2 });

        var mapper = config.CreateMapper();

        var deep = new SelfReferencing
        {
            Id = 1,
            Parent = new SelfReferencing
            {
                Id = 2,
                Parent = new SelfReferencing
                {
                    Id = 3,
                    Parent = new SelfReferencing { Id = 4 }
                }
            }
        };

        var dto = mapper.Map<SelfReferencing, SelfReferencingDto>(deep);

        Assert.Equal(1, dto.Id);
        Assert.NotNull(dto.Parent);
        Assert.Equal(2, dto.Parent!.Id);
        // Depth limit should cut off deeper nesting
    }

    [Fact]
    public void AssertConfigurationIsValid_ThrowsOnUnmappedProperty()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<PartialSource, FullDest>();
        });

        Assert.Throws<MaptureException>(() => config.AssertConfigurationIsValid());
    }

    [Fact]
    public void AssertConfigurationIsValid_PassesWhenIgnored()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<PartialSource, FullDest>()
                .Ignore(d => d.Extra);
        });

        config.AssertConfigurationIsValid(); // Should not throw
    }
}

public class PartialSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class FullDest
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Extra { get; set; } = "";
}
