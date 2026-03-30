namespace Mapture.Tests;

public class FluentApiTests
{
    [Fact]
    public void ForMember_MapFrom_CustomResolver()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .ForMember(d => d.Name, opt => opt.MapFrom((Func<User, string>)(s => s.Name.ToUpperInvariant())));
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Name = "alice" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("ALICE", dto.Name);
    }

    [Fact]
    public void Ignore_SkipsProperty()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .Ignore(d => d.Email);
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Name = "Alice", Email = "alice@test.com" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Alice", dto.Name);
        Assert.Equal("", dto.Email); // Default, not mapped
    }

    [Fact]
    public void ForMember_UseValue_SetsConstant()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .ForMember(d => d.Email, opt => opt.UseValue("default@test.com"));
        });

        var mapper = config.CreateMapper();
        var dto = mapper.Map<User, UserDto>(new User { Id = 1 });

        Assert.Equal("default@test.com", dto.Email);
    }

    [Fact]
    public void ForMember_Condition_OnlyMapsWhenTrue()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .ForMember(d => d.Age, opt =>
                {
                    opt.MapFrom((Func<User, int>)(s => s.Age));
                    opt.Condition(s => s.Age > 0);
                });
        });

        var mapper = config.CreateMapper();

        var dto1 = mapper.Map<User, UserDto>(new User { Id = 1, Age = 25 });
        Assert.Equal(25, dto1.Age);

        var dto2 = mapper.Map<User, UserDto>(new User { Id = 2, Age = 0 });
        Assert.Equal(0, dto2.Age); // Condition false, Age stays default (0)
    }

    [Fact]
    public void ConvertUsing_CustomConverter()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .ConvertUsing(src => new UserDto { Id = src.Id * 10, Name = $"Converted_{src.Name}" });
        });

        var mapper = config.CreateMapper();
        var dto = mapper.Map<User, UserDto>(new User { Id = 3, Name = "Test" });

        Assert.Equal(30, dto.Id);
        Assert.Equal("Converted_Test", dto.Name);
    }

    [Fact]
    public void AfterMap_ExecutesPostProcessing()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .AfterMap((src, dest) => dest.Name = dest.Name + " [mapped]");
        });

        var mapper = config.CreateMapper();
        var dto = mapper.Map<User, UserDto>(new User { Name = "Alice" });

        Assert.Equal("Alice [mapped]", dto.Name);
    }

    [Fact]
    public void ReverseMap_CreatesInverseMapping()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>().ReverseMap();
        });

        var mapper = config.CreateMapper();

        // Forward
        var dto = mapper.Map<User, UserDto>(new User { Id = 1, Name = "Alice" });
        Assert.Equal("Alice", dto.Name);

        // Reverse
        var user = mapper.Map<UserDto, User>(new UserDto { Id = 2, Name = "Bob" });
        Assert.Equal("Bob", user.Name);
    }

    [Fact]
    public void ConstructUsing_CustomConstruction()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>()
                .ConstructUsing(src => new UserDto { Id = src.Id + 100 })
                .Ignore(d => d.Id); // Prevent auto-map from overwriting
        });

        var mapper = config.CreateMapper();
        var dto = mapper.Map<User, UserDto>(new User { Id = 5, Name = "Test" });

        Assert.Equal(105, dto.Id);
        Assert.Equal("Test", dto.Name); // Name still auto-mapped
    }
}
