using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Mapture
{
    public sealed class MapperConfiguration
    {
        private readonly Dictionary<TypePair, TypeMap> _typeMaps = new();
        private readonly MaptureOptions _options;
        internal MappingEngine Engine { get; }

        public MapperConfiguration(Action<IMapperConfigurationExpression> configure)
            : this(configure, new MaptureOptions())
        {
        }

        public MapperConfiguration(Action<IMapperConfigurationExpression> configure, MaptureOptions options)
        {
            _options = options ?? new MaptureOptions();
            var expression = new MapperConfigurationExpression();
            configure(expression);

            foreach (var typeMap in expression.TypeMaps)
            {
                var pair = new TypePair(typeMap.SourceType, typeMap.DestinationType);
                _typeMaps[pair] = typeMap;
            }

            Engine = new MappingEngine(_typeMaps, _options);
        }

        public IMapper CreateMapper() => new Mapper(Engine);

        public void AssertConfigurationIsValid()
        {
            foreach (var kvp in _typeMaps)
            {
                var typeMap = kvp.Value;
                var destProps = typeMap.DestinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var sourceProps = typeMap.SourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var destProp in destProps)
                {
                    if (!destProp.CanWrite) continue;

                    if (typeMap.MemberMappings.TryGetValue(destProp.Name, out var mm) && (mm.IsIgnored || mm.CustomResolver != null || mm.SourceExpression != null || mm.HasConstantValue))
                        continue;

                    var hasSource = sourceProps.Any(sp => string.Equals(sp.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));
                    if (!hasSource)
                    {
                        throw new MaptureException(
                            $"Unmapped property '{destProp.Name}' on {typeMap.DestinationType.Name}. " +
                            $"Create a map from {typeMap.SourceType.Name} or call Ignore().");
                    }
                }
            }
        }
    }

    public interface IMapperConfigurationExpression
    {
        IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();
        void AddProfile<TProfile>() where TProfile : Profile, new();
        void AddProfile(Profile profile);
        void AddProfiles(Assembly assembly);
    }

    internal sealed class MapperConfigurationExpression : IMapperConfigurationExpression
    {
        internal List<TypeMap> TypeMaps { get; } = new();

        public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var typeMap = new TypeMap(typeof(TSource), typeof(TDestination));
            TypeMaps.Add(typeMap);
            return new MappingExpression<TSource, TDestination>(typeMap, reverseMap => TypeMaps.Add(reverseMap));
        }

        public void AddProfile<TProfile>() where TProfile : Profile, new()
        {
            AddProfile(new TProfile());
        }

        public void AddProfile(Profile profile)
        {
            TypeMaps.AddRange(profile.TypeMaps);
        }

        public void AddProfiles(Assembly assembly)
        {
            var profileTypes = assembly.GetTypes()
                .Where(t => typeof(Profile).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);

            foreach (var type in profileTypes)
            {
                var profile = (Profile)Activator.CreateInstance(type)!;
                AddProfile(profile);
            }
        }
    }
}
