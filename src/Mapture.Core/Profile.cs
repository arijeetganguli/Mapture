using System.Collections.Generic;

namespace Mapture
{
    public abstract class Profile
    {
        internal List<TypeMap> TypeMaps { get; } = new();

        protected IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var typeMap = new TypeMap(typeof(TSource), typeof(TDestination));
            TypeMaps.Add(typeMap);
            return new MappingExpression<TSource, TDestination>(typeMap, reverseMap => TypeMaps.Add(reverseMap));
        }
    }
}
