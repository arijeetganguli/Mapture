namespace Mapture.Tests;

public class NestedAndCollectionTests
{
    [Fact]
    public void Map_NestedObject()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Address, AddressDto>();
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var user = new User
        {
            Id = 1,
            Name = "Alice",
            Address = new Address { Street = "123 Main", City = "Springfield", Zip = "62701" }
        };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.NotNull(dto.Address);
        Assert.Equal("123 Main", dto.Address!.Street);
        Assert.Equal("Springfield", dto.Address.City);
    }

    [Fact]
    public void Map_NullNestedObject_StaysNull()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Address, AddressDto>();
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Name = "Alice", Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Null(dto.Address);
    }

    [Fact]
    public void Map_StringCollection()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<User, UserDto>();
        });

        var mapper = config.CreateMapper();
        var user = new User { Id = 1, Tags = new List<string> { "admin", "active" } };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(2, dto.Tags.Count);
        Assert.Contains("admin", dto.Tags);
        Assert.Contains("active", dto.Tags);
    }

    [Fact]
    public void Map_NestedCollection()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<OrderItem, OrderItemDto>();
            cfg.CreateMap<Order, OrderDto>();
        });

        var mapper = config.CreateMapper();
        var order = new Order
        {
            OrderId = 100,
            Total = 59.99m,
            Items = new List<OrderItem>
            {
                new() { ProductName = "Widget", Quantity = 2 },
                new() { ProductName = "Gadget", Quantity = 1 }
            }
        };

        var dto = mapper.Map<Order, OrderDto>(order);

        Assert.Equal(100, dto.OrderId);
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal("Widget", dto.Items[0].ProductName);
        Assert.Equal(1, dto.Items[1].Quantity);
    }
}
