using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Mapture.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Mapture services, scanning the given assembly for Profile classes.
        /// </summary>
        public static IServiceCollection AddMapture(this IServiceCollection services, Type markerType)
        {
            return AddMapture(services, markerType.Assembly, null);
        }

        /// <summary>
        /// Registers Mapture services, scanning the given assembly for Profile classes.
        /// </summary>
        public static IServiceCollection AddMapture(this IServiceCollection services, Assembly assembly, Action<MaptureOptions>? configureOptions = null)
        {
            var options = new MaptureOptions();
            configureOptions?.Invoke(options);

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfiles(assembly);
            }, options);

            services.AddSingleton(config);
            services.AddSingleton<IMapper>(config.CreateMapper());
            return services;
        }

        /// <summary>
        /// Registers Mapture services with inline configuration.
        /// </summary>
        public static IServiceCollection AddMapture(this IServiceCollection services, Action<IMapperConfigurationExpression> configure, Action<MaptureOptions>? configureOptions = null)
        {
            var options = new MaptureOptions();
            configureOptions?.Invoke(options);

            var config = new MapperConfiguration(configure, options);
            services.AddSingleton(config);
            services.AddSingleton<IMapper>(config.CreateMapper());
            return services;
        }

        /// <summary>
        /// Overload matching the pattern: services.AddMapture(options => { ... });
        /// </summary>
        public static IServiceCollection AddMapture(this IServiceCollection services, Action<MaptureOptions> configureOptions)
        {
            return AddMapture(services, Assembly.GetCallingAssembly(), configureOptions);
        }
    }
}
