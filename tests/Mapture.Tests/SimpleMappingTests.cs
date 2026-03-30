namespace Mapture.Tests;

public class SimpleMappingTests
{
    [Fact]
    public void Map_SimpleProperties_ByConvention()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Name = "Alice", Email = "alice@test.com", Age = 30 };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(1, dto.Id);
        Assert.Equal("Alice", dto.Name);
        Assert.Equal("alice@test.com", dto.Email);
        Assert.Equal(30, dto.Age);
    }

    [Fact]
    public void Map_NullSource_ReturnsDefault()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var result = mapper.Map<User, UserDto>(null!);

        Assert.Null(result);
    }

    [Fact]
    public void Map_UsingObjectOverload()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 5, Name = "Bob" };

        var dto = mapper.Map<UserDto>((object)user);

        Assert.Equal(5, dto.Id);
        Assert.Equal("Bob", dto.Name);
    }

    [Fact]
    public void Map_UsingTypeOverload()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 7, Name = "Charlie" };

        var result = mapper.Map(user, typeof(User), typeof(UserDto));

        Assert.IsType<UserDto>(result);
        Assert.Equal(7, ((UserDto)result).Id);
    }

    [Fact]
    public void Map_NoConfiguredMapping_ThrowsException()
    {
        var config = new MapperConfiguration(cfg => { });
        var mapper = config.CreateMapper();

        Assert.Throws<MaptureException>(() => mapper.Map<User, UserDto>(new User()));
    }
}
