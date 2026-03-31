using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using Mapture;
using Mapster;

// AutoMapper uses its own namespace
using AutoMapperConfig = AutoMapper.MapperConfiguration;
using AutoMapperProfile = AutoMapper.Profile;
using IAutoMapper = AutoMapper.IMapper;

using PdMapperConfig = PanoramicData.Mapper.MapperConfiguration;
using IPdMapper = PanoramicData.Mapper.IMapper;

BenchmarkRunner.Run<MappingBenchmarks>(
    DefaultConfig.Instance
        .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Trend)));

[MemoryDiagnoser]
[RankColumn]
public class MappingBenchmarks
{
    private Mapture.IMapper _MaptureMapper = null!;
    private IAutoMapper _autoMapper = null!;
    private IPdMapper _pdMapper = null!;
    private BenchmarkUser _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Mapture setup
        var msConfig = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<BenchmarkUser, BenchmarkUserDto>();
            cfg.CreateMap<BenchmarkAddress, BenchmarkAddressDto>();
        });
        _MaptureMapper = msConfig.CreateMapper();

        // AutoMapper setup
        var amConfig = new AutoMapperConfig(cfg =>
        {
            cfg.CreateMap<BenchmarkUser, BenchmarkUserDto>();
            cfg.CreateMap<BenchmarkAddress, BenchmarkAddressDto>();
        });
        _autoMapper = amConfig.CreateMapper();

        // PanoramicData.Mapper setup
        var pdConfig = new PdMapperConfig(cfg =>
        {
            cfg.AddProfile(new PdBenchmarkProfile());
        });
        _pdMapper = pdConfig.CreateMapper();

        // Mapster setup (uses conventions by default)
        TypeAdapterConfig.GlobalSettings.NewConfig<BenchmarkUser, BenchmarkUserDto>();

        _source = new BenchmarkUser
        {
            Id = 1,
            Name = "Alice Johnson",
            Email = "alice@example.com",
            Age = 30,
            Address = new BenchmarkAddress
            {
                Street = "123 Main St",
                City = "Springfield",
                State = "IL",
                Zip = "62701"
            }
        };
    }

    [Benchmark(Baseline = true)]
    public BenchmarkUserDto ManualMapping()
    {
        return new BenchmarkUserDto
        {
            Id = _source.Id,
            Name = _source.Name,
            Email = _source.Email,
            Age = _source.Age,
            Address = _source.Address == null ? null : new BenchmarkAddressDto
            {
                Street = _source.Address.Street,
                City = _source.Address.City,
                State = _source.Address.State,
                Zip = _source.Address.Zip
            }
        };
    }

    [Benchmark]
    public BenchmarkUserDto Mapture_Map()
    {
        return _MaptureMapper.Map<BenchmarkUser, BenchmarkUserDto>(_source);
    }

    [Benchmark]
    public BenchmarkUserDto AutoMapper_Map()
    {
        return _autoMapper.Map<BenchmarkUserDto>(_source);
    }

    [Benchmark]
    public BenchmarkUserDto Mapster_Map()
    {
        return _source.Adapt<BenchmarkUserDto>();
    }

    [Benchmark]
    public BenchmarkUserDto PanoramicData_Map()
    {
        return _pdMapper.Map<BenchmarkUserDto>(_source);
    }
}

public class BenchmarkUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public BenchmarkAddress? Address { get; set; }
}

public class BenchmarkUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public BenchmarkAddressDto? Address { get; set; }
}

public class BenchmarkAddress
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class BenchmarkAddressDto
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class PdBenchmarkProfile : PanoramicData.Mapper.Profile
{
    public PdBenchmarkProfile()
    {
        CreateMap<BenchmarkUser, BenchmarkUserDto>();
        CreateMap<BenchmarkAddress, BenchmarkAddressDto>();
    }
}
