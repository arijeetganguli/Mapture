using System;
using System.Collections.Generic;
using System.Reflection;

namespace Mapture.Compatibility
{
    /// <summary>
    /// Compatibility extensions that interpret common AutoMapper usage patterns.
    /// Provides behavior parity for nested objects, collections, and null handling.
    /// </summary>
    public static class CompatibilityExtensions
    {
        /// <summary>
        /// Creates a MapperConfiguration with compatibility mode enabled,
        /// scanning the given assembly for Profile classes.
        /// </summary>
        public static MapperConfiguration CreateCompatibleConfiguration(Assembly assembly, Action<MaptureOptions>? configureOptions = null)
        {
            var options = new MaptureOptions { CompatibilityMode = true };
            configureOptions?.Invoke(options);

            return new MapperConfiguration(cfg =>
            {
                cfg.AddProfiles(assembly);
            }, options);
        }

        /// <summary>
        /// Creates a MapperConfiguration with compatibility mode enabled,
        /// using inline configuration.
        /// </summary>
        public static MapperConfiguration CreateCompatibleConfiguration(Action<IMapperConfigurationExpression> configure, Action<MaptureOptions>? configureOptions = null)
        {
            var options = new MaptureOptions { CompatibilityMode = true };
            configureOptions?.Invoke(options);

            return new MapperConfiguration(configure, options);
        }
    }
}
