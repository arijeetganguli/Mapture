namespace Mapture.Tests;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public Address? Address { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public AddressDto? Address { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class AddressDto
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class Order
{
    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderDto
{
    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
}

public class OrderItemDto
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
}

public class SelfReferencing
{
    public int Id { get; set; }
    public SelfReferencing? Parent { get; set; }
}

public class SelfReferencingDto
{
    public int Id { get; set; }
    public SelfReferencingDto? Parent { get; set; }
}
