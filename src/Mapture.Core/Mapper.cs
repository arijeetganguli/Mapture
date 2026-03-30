using System;

namespace Mapture
{
    public sealed class Mapper : IMapper
    {
        private readonly MappingEngine _engine;

        internal Mapper(MappingEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public Mapper(MapperConfiguration configuration)
            : this(configuration.Engine)
        {
        }

        public TDestination Map<TDestination>(object source)
        {
            if (source == null) return default!;
            return (TDestination)_engine.Map(source, source.GetType(), typeof(TDestination));
        }

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            return _engine.Map<TSource, TDestination>(source);
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            return _engine.Map<TSource, TDestination>(source, destination);
        }

        public object Map(object source, Type sourceType, Type destinationType)
        {
            return _engine.Map(source, sourceType, destinationType);
        }
    }
}
