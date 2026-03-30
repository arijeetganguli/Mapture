using System;

namespace Mapture
{
    /// <summary>
    /// Core mapping interface. Drop-in compatible with common mapper patterns.
    /// </summary>
    public interface IMapper
    {
        TDestination Map<TDestination>(object source);
        TDestination Map<TSource, TDestination>(TSource source);
        TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
        object Map(object source, Type sourceType, Type destinationType);
    }
}
