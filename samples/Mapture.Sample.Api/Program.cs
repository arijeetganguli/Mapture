using Mapture;
using Mapture.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Register Mapture with DI — scans for Profile classes
builder.Services.AddMapture(typeof(Program).Assembly);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Sample entities
var users = new List<User>
{
    new() { Id = 1, Name = "Alice Johnson", Email = "alice@example.com", Age = 30 },
    new() { Id = 2, Name = "Bob Smith", Email = "bob@example.com", Age = 25 },
};

app.MapGet("/users", (IMapper mapper) =>
{
    return users.Select(u => mapper.Map<User, UserDto>(u));
});

app.MapGet("/users/{id}", (int id, IMapper mapper) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is null ? Results.NotFound() : Results.Ok(mapper.Map<User, UserDto>(user));
});

app.Run();

// Models
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

// Mapture Profile
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .Ignore(d => d.Email); // Demonstrates ignoring sensitive data
    }
}
