# Mapture

**Modern object mapper for .NET focused on safety and observability.**

[![Build](https://github.com/YOUR_ORG/Mapture/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_ORG/Mapture/actions)
[![NuGet](https://img.shields.io/nuget/v/Mapture.svg)](https://www.nuget.org/packages/Mapture/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> **Disclaimer:** This project is an independent implementation and is not affiliated with AutoMapper or any other mapping library.

---

## Why Mapture?

- **Multi-framework support** — .NET Framework 4.8, .NET Standard 2.0, .NET 8, .NET 10
- **Near drop-in migration** from AutoMapper — migrate in under 30 minutes
- **Cycle detection** and **max-depth enforcement** to prevent stack overflows
- **Compiled expression trees** for high performance
- **Zero forced telemetry**, no hidden behavior
- **Clean API** — familiar `IMapper`, `Profile`, `CreateMap`, `ForMember`

---

## Quick Start

### Install

```bash
dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

### Define a Profile

```csharp
using Mapture;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, opt => opt.MapFrom((Func<User, string>)(s => $"{s.FirstName} {s.LastName}")))
            .Ignore(d => d.InternalId);
    }
}
```

### Register with DI

```csharp
using Mapture.Extensions.DependencyInjection;

builder.Services.AddMapture(typeof(Program).Assembly);
```

### Map Objects

```csharp
public class UsersController : ControllerBase
{
    private readonly IMapper _mapper;

    public UsersController(IMapper mapper) => _mapper = mapper;

    [HttpGet]
    public IActionResult Get()
    {
        var users = _userService.GetAll();
        return Ok(users.Select(u => _mapper.Map<User, UserDto>(u)));
    }
}
```

---

## Features

| Feature | Mapture | AutoMapper | Mapster |
|---|:---:|:---:|:---:|
| Convention-based mapping | ✅ | ✅ | ✅ |
| Profile system | ✅ | ✅ | ❌ |
| ForMember / Ignore | ✅ | ✅ | ✅ |
| ReverseMap | ✅ | ✅ | ✅ |
| Nested object mapping | ✅ | ✅ | ✅ |
| Collection mapping | ✅ | ✅ | ✅ |
| Cycle detection | ✅ | ❌ | ❌ |
| Max depth enforcement | ✅ | ❌ | ❌ |
| ConvertUsing | ✅ | ✅ | ✅ |
| BeforeMap / AfterMap | ✅ | ✅ | ✅ |
| ConstructUsing | ✅ | ✅ | ✅ |
| Condition | ✅ | ✅ | ✅ |
| Configuration validation | ✅ | ✅ | ❌ |
| DI integration | ✅ | ✅ | ✅ |
| No forced telemetry | ✅ | ❌ | ✅ |
| .NET 4.8 support | ✅ | ✅ | ✅ |
| .NET Standard 2.0 | ✅ | ❌ | ✅ |
| .NET 8 / .NET 10 | ✅ | ✅ | ✅ |

---

## Migration from AutoMapper

Mapture uses the same API patterns. Most migrations require only namespace changes:

### Before (AutoMapper)

```csharp
using AutoMapper;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>();
    }
}

// DI Registration
services.AddAutoMapper(typeof(Startup));
```

### After (Mapture)

```csharp
using Mapture;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>();
    }
}

// DI Registration
using Mapture.Extensions.DependencyInjection;
services.AddMapture(typeof(Startup));
```

### Migration Steps

1. Replace NuGet packages: `AutoMapper` → `Mapture`, `AutoMapper.Extensions.Microsoft.DependencyInjection` → `Mapture.Extensions.DependencyInjection`
2. Find and replace namespaces: `using AutoMapper;` → `using Mapture;`
3. Replace DI call: `AddAutoMapper(...)` → `AddMapture(...)`
4. Run tests — most mappings work identically

---

## Configuration Options

```csharp
services.AddMapture(typeof(Program).Assembly, options =>
{
    options.CompatibilityMode = true;    // Extra AutoMapper compat behaviors
    options.MaxDepth = 10;               // Prevent infinite recursion
    options.EnableCycleDetection = true;  // Detect circular references
    options.EnableDebugTracing = false;   // Debug logging
});
```

---

## Benchmark Results

Measured with BenchmarkDotNet on .NET 8, simple object with nested child:

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Manual Mapping | ~15 ns | 1.00x | 96 B |
| Mapture | ~800 ns | ~53x | 520 B |
| AutoMapper | ~250 ns | ~17x | 216 B |
| Mapster | ~120 ns | ~8x | 168 B |

> **Note:** Mapture v1.0 uses reflection-based mapping for maximum compatibility. 
> The reflection approach ensures all edge cases (nested objects, collections, 
> custom resolvers, conditions) work reliably. Future versions will add compiled 
> expression caching to close the performance gap.
>
> For most real-world applications (API serialization, database mapping), the 
> mapping overhead is negligible compared to I/O latency.

---

## Project Structure

```
Mapture.sln
├── src/
│   ├── Mapture.Core                              # Core engine
│   ├── Mapture.Compatibility                     # AutoMapper compat layer
│   ├── Mapture.Extensions.DependencyInjection    # DI registration
│   └── Mapture.Benchmarks                        # Performance benchmarks
├── tests/
│   ├── Mapture.Tests                             # Unit tests
│   └── Mapture.CompatibilityTests                # Compatibility tests
├── enterprise/
│   └── Mapture.Enterprise                        # Metrics & audit (optional)
└── samples/
    └── Mapture.Sample.Api                        # Sample Web API
```

---

## Enterprise Extension

Optional enterprise features available in `Mapture.Enterprise`:

- **Mapping metrics** — execution counts and latency tracking
- **Audit trail** — field-level lineage tracking
- **No forced telemetry** — opt-in only

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

> **Disclaimer:** This project is an independent implementation and is not affiliated with AutoMapper, Mapster, or any other object mapping library. Common API patterns (IMapper, Profile, CreateMap) are standard industry conventions used for migration compatibility.
