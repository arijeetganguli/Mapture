namespace Mapture.Tests;

public class ProfileTests
{
    [Fact]
    public void Profile_RegistersMappings()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TestProfile>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Name = "Alice", Email = "alice@test.com" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Alice", dto.Name);
    }

    [Fact]
    public void AddProfiles_ScansAssembly()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfiles(typeof(ProfileTests).Assembly);
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Name = "Test" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Test", dto.Name);
    }

    [Fact]
    public void Map_ExistingDestination_UpdatesProperties()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var source = new User { Id = 1, Name = "Updated", Email = "new@test.com" };
        var existing = new UserDto { Id = 99, Name = "Old", Email = "old@test.com" };

        var result = mapper.Map(source, existing);

        Assert.Same(existing, result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Updated", result.Name);
        Assert.Equal("new@test.com", result.Email);
    }
}

public class TestProfile : Profile
{
    public TestProfile()
    {
        CreateMap<User, UserDto>();
        CreateMap<Address, AddressDto>();
        CreateMap<Order, OrderDto>();
        CreateMap<OrderItem, OrderItemDto>();
    }
}
