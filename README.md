# Mapture

**The fastest object mapper for .NET — with safety features no other mapper has.**

[![Build](https://github.com/arijeetganguli/Mapture/actions/workflows/ci.yml/badge.svg)](https://github.com/arijeetganguli/Mapture/actions)
[![NuGet](https://img.shields.io/nuget/v/Mapture.svg)](https://www.nuget.org/packages/Mapture/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> **Disclaimer:** This project is an independent implementation and is not affiliated with AutoMapper, Mapster, PanoramicData.Mapper, or any other mapping library.

---

## The Story

You've been here before. Your API maps `User` to `UserDto` ten thousand times per second, and one day someone adds a `Parent` property that points back to itself. The stack overflows. Production goes down. Nobody knows why.

Or maybe it's subtler — a mapping library that phones home with telemetry you never asked for. Or one that only supports .NET 8+, leaving your legacy services stranded.

**Mapture was built for teams who are tired of compromise.** It's the only .NET mapper that is simultaneously:

- **Fastest** — benchmarked faster than Mapster, AutoMapper, and PanoramicData.Mapper
- **Safest** — cycle detection and max-depth enforcement catch infinite recursion before it happens
- **Broadest** — runs on .NET Framework 4.8, .NET Standard 2.0, .NET 8, and .NET 10
- **Cleanest** — zero telemetry, zero surprises, familiar API, 30-minute migration from AutoMapper

---

## Benchmark Results

Measured with BenchmarkDotNet on .NET 10.0, mapping a simple object with one nested child.
All libraries configured with equivalent mappings, run on the same hardware:

| Rank | Method | Mean | vs Manual | Allocated |
|:---:|---|---:|---:|---:|
| 🥇 | **Manual Mapping** | **~17 ns** | baseline | 96 B |
| 🥈 | **Mapture** | **~25 ns** | **1.5x** | **96 B** |
| 🥉 | Mapster | ~27 ns | 1.6x | 96 B |
| 4 | AutoMapper | ~68 ns | 4.0x | 96 B |
| 5 | PanoramicData.Mapper | ~283 ns | 16.9x | 272 B |

> **How?** Mapture compiles expression trees at configuration time and caches the compiled
> delegates per type pair. Acyclic type graphs get a zero-overhead fast path that skips all
> cycle/depth tracking. The result: your mapping function runs almost as fast as code you'd
> write by hand.

---

## Quick Start

### 1. Install

```bash
dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

### 2. Define a Profile

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

### 3. Register with DI

```csharp
using Mapture.Extensions.DependencyInjection;

builder.Services.AddMapture(typeof(Program).Assembly);
```

### 4. Map Objects

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

That's it. Convention-based matching handles properties with the same name. Custom mappings,
ignores, and reverse maps are all one fluent call away.

---

## Feature Comparison

| Feature | Mapture | AutoMapper | Mapster | PanoramicData.Mapper |
|---|:---:|:---:|:---:|:---:|
| **Performance rank** | **🥈** | 4th | 🥉 | 5th |
| Convention-based mapping | ✅ | ✅ | ✅ | ✅ |
| Profile system | ✅ | ✅ | ❌ | ✅ |
| ForMember / Ignore | ✅ | ✅ | ✅ | ✅ |
| ReverseMap | ✅ | ✅ | ✅ | ✅ |
| Nested object mapping | ✅ | ✅ | ✅ | ✅ |
| Collection mapping | ✅ | ✅ | ✅ | ✅ |
| **Cycle detection** | **✅** | ❌ | ❌ | ❌ |
| **Max depth enforcement** | **✅** | ❌ | ❌ | ✅ |
| ConvertUsing | ✅ | ✅ | ✅ | ✅ |
| BeforeMap / AfterMap | ✅ | ✅ | ✅ | ✅ |
| ConstructUsing | ✅ | ✅ | ✅ | ✅ |
| Condition | ✅ | ✅ | ✅ | ✅ |
| Configuration validation | ✅ | ✅ | ❌ | ✅ |
| DI integration | ✅ | ✅ | ✅ | ✅ |
| No forced telemetry | ✅ | ❌ | ✅ | ✅ |
| .NET Framework 4.8 | ✅ | ✅ | ✅ | ❌ |
| .NET Standard 2.0 | ✅ | ❌ | ✅ | ❌ |
| .NET 8 / .NET 10 | ✅ | ✅ | ✅ | ⚠️ .NET 10 only |

---

## Safety Features

### Cycle Detection

Objects that reference themselves (directly or indirectly) are the #1 cause of
`StackOverflowException` in mapping libraries. Mapture detects cycles at runtime
and breaks them safely:

```csharp
var node = new Node { Id = 1 };
node.Parent = node; // Circular reference!

var dto = mapper.Map<Node, NodeDto>(node);
// dto.Parent is null — cycle safely broken, no stack overflow
```

### Max Depth Enforcement

Even without cycles, deeply nested object graphs can exhaust the stack.
Mapture caps recursion at a configurable depth:

```csharp
services.AddMapture(typeof(Program).Assembly, options =>
{
    options.MaxDepth = 5; // Stop mapping beyond 5 levels deep
});
```

### Smart Cycle Analysis

Mapture analyzes your type graph at configuration time. Types that **cannot** have
cycles (no self-referencing paths) get a zero-overhead fast path — no `HashSet`, no
depth counter, no per-call allocation. You get safety where you need it and raw speed
everywhere else.

### Configuration Validation

Catch unmapped properties at startup — not at 3am in production:

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<User, UserDto>();
});

config.AssertConfigurationIsValid();
// Throws MaptureException if any destination property has no source and isn't ignored
```

---

## Fluent API

### ForMember — Custom Property Mapping

```csharp
CreateMap<User, UserDto>()
    .ForMember(d => d.FullName,
        opt => opt.MapFrom((Func<User, string>)(s => $"{s.First} {s.Last}")));
```

### Ignore — Skip a Property

```csharp
CreateMap<User, UserDto>()
    .Ignore(d => d.InternalSecret);
```

### ReverseMap — Bidirectional Mapping

```csharp
CreateMap<User, UserDto>().ReverseMap();
// Now both User→UserDto and UserDto→User work
```

### ConvertUsing — Full Custom Conversion

```csharp
CreateMap<User, UserDto>()
    .ConvertUsing(src => new UserDto
    {
        Id = src.Id,
        Name = src.Name.ToUpperInvariant()
    });
```

### BeforeMap / AfterMap — Pre/Post Processing

```csharp
CreateMap<User, UserDto>()
    .AfterMap((src, dest) => dest.Name = dest.Name.Trim());
```

### ConstructUsing — Custom Instantiation

```csharp
CreateMap<User, UserDto>()
    .ConstructUsing(src => new UserDto { Id = src.Id * 10 });
```

### Condition — Conditional Mapping

```csharp
CreateMap<User, UserDto>()
    .ForMember(d => d.Age, opt =>
    {
        opt.MapFrom((Func<User, int>)(s => s.Age));
        opt.Condition(s => s.Age > 0);
    });
```

### UseValue — Constant Value

```csharp
CreateMap<User, UserDto>()
    .ForMember(d => d.Source, opt => opt.UseValue("API"));
```

---

## Migration from AutoMapper

Mapture uses the same API patterns. Most migrations take under 30 minutes:

### Step 1 — Replace NuGet Packages

```bash
dotnet remove package AutoMapper
dotnet remove package AutoMapper.Extensions.Microsoft.DependencyInjection
dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

### Step 2 — Find & Replace Namespaces

| Find | Replace |
|---|---|
| `using AutoMapper;` | `using Mapture;` |
| `using AutoMapper.Extensions.Microsoft.DependencyInjection;` | `using Mapture.Extensions.DependencyInjection;` |

### Step 3 — Replace DI Registration

```csharp
// Before
services.AddAutoMapper(typeof(Startup));

// After
services.AddMapture(typeof(Startup));
```

### Step 4 — Run Tests

Most mappings work identically. The same `Profile`, `CreateMap`, `ForMember`, `Ignore`,
and `ReverseMap` patterns are supported.

---

## Configuration Options

```csharp
services.AddMapture(typeof(Program).Assembly, options =>
{
    options.CompatibilityMode = true;    // Extra AutoMapper compat behaviors
    options.MaxDepth = 10;               // Prevent infinite recursion (default: 10)
    options.EnableCycleDetection = true;  // Detect circular references (default: true)
    options.EnableDebugTracing = false;   // Debug logging (default: false)
});
```

---

## Architecture

```
Mapture.slnx
├── src/
│   ├── Mapture.Core                              # Core mapping engine
│   ├── Mapture.Compatibility                     # AutoMapper compatibility layer
│   ├── Mapture.Extensions.DependencyInjection    # DI registration extensions
│   └── Mapture.Benchmarks                        # BenchmarkDotNet performance tests
├── tests/
│   ├── Mapture.Tests                             # 24 unit tests × 3 frameworks = 72
│   └── Mapture.CompatibilityTests                # 3 compat tests × 3 frameworks = 9
├── enterprise/
│   └── Mapture.Enterprise                        # Metrics & audit (optional)
├── samples/
│   └── Mapture.Sample.Api                        # Sample ASP.NET Core Web API
└── docs/
    └── index.html                                # Full documentation site
```

### How the Engine Works

1. **Configuration time** — You define mappings via Profiles. Mapture builds a type map dictionary.
2. **First map call** — Mapture compiles an expression tree for the type pair into a native delegate.
   For acyclic types, this delegate is pure property assignment — no reflection, no dictionary lookups,
   no allocations beyond the destination object.
3. **Subsequent calls** — The compiled delegate is cached in a thread-static slot. The hot path is:
   null check → read cached delegate → call. That's it.

For cyclic types (detected at configuration time), Mapture wraps the delegate with cycle detection
and depth tracking. You never need to think about it — the engine picks the right path automatically.

---

## Enterprise Extension

Optional enterprise features available in `Mapture.Enterprise`:

- **Mapping metrics** — execution counts and latency tracking
- **Audit trail** — field-level lineage tracking
- **No forced telemetry** — opt-in only, you control what's collected

---

## License

MIT License. See [LICENSE](LICENSE) for details.

> **Disclaimer:** This project is an independent implementation and is not affiliated with AutoMapper, Mapster, or any other object mapping library. Common API patterns (IMapper, Profile, CreateMap) are standard industry conventions used for migration compatibility.
