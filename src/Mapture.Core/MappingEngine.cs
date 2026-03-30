using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Mapture
{
    internal sealed class MappingEngine
    {
        private readonly ConcurrentDictionary<TypePair, Delegate> _compiledMaps = new();
        private readonly Dictionary<TypePair, TypeMap> _typeMaps;
        private readonly MaptureOptions _options;

        public MappingEngine(Dictionary<TypePair, TypeMap> typeMaps, MaptureOptions options)
        {
            _typeMaps = typeMaps;
            _options = options;
        }

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            if (source == null)
                return default!;

            return Map<TSource, TDestination>(source, depth: 0, visited: _options.EnableCycleDetection ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null);
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null)
                return destination;

            var pair = new TypePair(typeof(TSource), typeof(TDestination));
            if (!_typeMaps.TryGetValue(pair, out var typeMap))
                throw new MaptureException($"No mapping configured for {typeof(TSource).Name} -> {typeof(TDestination).Name}");

            ApplyMapping(source, destination, typeMap);
            return destination;
        }

        public object Map(object source, Type sourceType, Type destinationType)
        {
            if (source == null)
                return default!;

            return MapObject(source, sourceType, destinationType, 0,
                _options.EnableCycleDetection ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null);
        }

        private TDestination Map<TSource, TDestination>(TSource source, int depth, HashSet<object>? visited)
        {
            if (source == null)
                return default!;

            if (depth > _options.MaxDepth)
                return default!;

            if (visited != null && !source.GetType().IsValueType)
            {
                if (!visited.Add(source))
                    return default!;
            }

            var pair = new TypePair(typeof(TSource), typeof(TDestination));

            if (_typeMaps.TryGetValue(pair, out var typeMap))
            {
                if (typeMap.CustomConverter != null)
                {
                    return ((Func<TSource, TDestination>)typeMap.CustomConverter)(source);
                }

                return ExecuteMap<TSource, TDestination>(source, typeMap, depth, visited);
            }

            // Try collection mapping
            if (TryMapCollection<TSource, TDestination>(source, depth, visited, out var collectionResult))
                return collectionResult;

            // Try assignable
            if (source is TDestination direct)
                return direct;

            throw new MaptureException($"No mapping configured for {typeof(TSource).Name} -> {typeof(TDestination).Name}");
        }

        private object MapObject(object source, Type sourceType, Type destinationType, int depth, HashSet<object>? visited)
        {
            if (source == null)
                return default!;

            if (depth > _options.MaxDepth)
                return default!;

            if (visited != null && !sourceType.IsValueType)
            {
                if (!visited.Add(source))
                    return default!;
            }

            var pair = new TypePair(sourceType, destinationType);

            if (_typeMaps.TryGetValue(pair, out var typeMap))
            {
                if (typeMap.CustomConverter != null)
                    return typeMap.CustomConverter.DynamicInvoke(source)!;

                return ExecuteMapUntyped(source, typeMap, depth, visited);
            }

            // Assignable
            if (destinationType.IsAssignableFrom(sourceType))
                return source;

            throw new MaptureException($"No mapping configured for {sourceType.Name} -> {destinationType.Name}");
        }

        private TDestination ExecuteMap<TSource, TDestination>(TSource source, TypeMap typeMap, int depth, HashSet<object>? visited)
        {
            var dest = typeMap.CustomConstructor != null
                ? ((Func<TSource, TDestination>)typeMap.CustomConstructor)(source)
                : Activator.CreateInstance<TDestination>();

            if (typeMap.BeforeMapAction is Action<TSource, TDestination> beforeAction)
                beforeAction(source, dest);

            ApplyMappingWithDepth(source, dest, typeMap, depth, visited);

            if (typeMap.AfterMapAction is Action<TSource, TDestination> afterAction)
                afterAction(source, dest);

            return dest;
        }

        private object ExecuteMapUntyped(object source, TypeMap typeMap, int depth, HashSet<object>? visited)
        {
            var dest = typeMap.CustomConstructor != null
                ? typeMap.CustomConstructor.DynamicInvoke(source)!
                : Activator.CreateInstance(typeMap.DestinationType)!;

            if (typeMap.BeforeMapAction != null)
                typeMap.BeforeMapAction.DynamicInvoke(source, dest);

            ApplyMappingUntyped(source, dest, typeMap, depth, visited);

            if (typeMap.AfterMapAction != null)
                typeMap.AfterMapAction.DynamicInvoke(source, dest);

            return dest;
        }

        private void ApplyMapping<TSource, TDestination>(TSource source, TDestination dest, TypeMap typeMap)
        {
            ApplyMappingWithDepth(source, dest, typeMap, 0, null);
        }

        private void ApplyMappingWithDepth<TSource, TDestination>(TSource source, TDestination dest, TypeMap typeMap, int depth, HashSet<object>? visited)
        {
            var sourceType = typeof(TSource);
            var destType = typeof(TDestination);
            var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var destProp in destProps)
            {
                if (!destProp.CanWrite) continue;

                // Check for explicit member mapping
                if (typeMap.MemberMappings.TryGetValue(destProp.Name, out var memberMapping))
                {
                    if (memberMapping.IsIgnored) continue;

                    if (memberMapping.Condition != null)
                    {
                        var conditionResult = (bool)memberMapping.Condition.DynamicInvoke(source)!;
                        if (!conditionResult) continue;
                    }

                    if (memberMapping.HasConstantValue)
                    {
                        destProp.SetValue(dest, memberMapping.ConstantValue);
                        continue;
                    }

                    if (memberMapping.CustomResolver != null)
                    {
                        var value = memberMapping.CustomResolver.DynamicInvoke(source);
                        destProp.SetValue(dest, ConvertValue(value, destProp.PropertyType));
                        continue;
                    }

                    if (memberMapping.SourceExpression is LambdaExpression lambda)
                    {
                        var compiled = lambda.Compile();
                        var value = compiled.DynamicInvoke(source);
                        destProp.SetValue(dest, ConvertValue(value, destProp.PropertyType));
                        continue;
                    }
                }

                // Auto-map by name
                var sourceProp = sourceProps.FirstOrDefault(p =>
                    string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));

                if (sourceProp == null || !sourceProp.CanRead) continue;

                var sourceValue = sourceProp.GetValue(source);
                if (sourceValue == null)
                {
                    if (!destProp.PropertyType.IsValueType)
                        destProp.SetValue(dest, null);
                    continue;
                }

                destProp.SetValue(dest, MapPropertyValue(sourceValue, sourceProp.PropertyType, destProp.PropertyType, depth + 1, visited));
            }
        }

        private void ApplyMappingUntyped(object source, object dest, TypeMap typeMap, int depth, HashSet<object>? visited)
        {
            var sourceType = source.GetType();
            var destType = dest.GetType();
            var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var destProp in destProps)
            {
                if (!destProp.CanWrite) continue;

                if (typeMap.MemberMappings.TryGetValue(destProp.Name, out var memberMapping))
                {
                    if (memberMapping.IsIgnored) continue;

                    if (memberMapping.HasConstantValue)
                    {
                        destProp.SetValue(dest, memberMapping.ConstantValue);
                        continue;
                    }

                    if (memberMapping.CustomResolver != null)
                    {
                        var value = memberMapping.CustomResolver.DynamicInvoke(source);
                        destProp.SetValue(dest, ConvertValue(value, destProp.PropertyType));
                        continue;
                    }
                }

                var sourceProp = sourceProps.FirstOrDefault(p =>
                    string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));

                if (sourceProp == null || !sourceProp.CanRead) continue;

                var sourceValue = sourceProp.GetValue(source);
                if (sourceValue == null)
                {
                    if (!destProp.PropertyType.IsValueType)
                        destProp.SetValue(dest, null);
                    continue;
                }

                destProp.SetValue(dest, MapPropertyValue(sourceValue, sourceProp.PropertyType, destProp.PropertyType, depth + 1, visited));
            }
        }

        private object? MapPropertyValue(object sourceValue, Type sourceType, Type destType, int depth, HashSet<object>? visited)
        {
            // Assignable directly (primitives, strings, same types)
            if (destType.IsAssignableFrom(sourceType))
                return sourceValue;

            // Nullable unwrap
            var underlyingDest = Nullable.GetUnderlyingType(destType);
            if (underlyingDest != null && underlyingDest.IsAssignableFrom(sourceType))
                return sourceValue;

            // Enum conversion
            if (destType.IsEnum && sourceType.IsEnum)
                return Enum.ToObject(destType, sourceValue);
            if (destType.IsEnum && sourceType == typeof(string))
                return Enum.Parse(destType, (string)sourceValue, ignoreCase: true);
            if (destType.IsEnum && IsNumericType(sourceType))
                return Enum.ToObject(destType, sourceValue);

            // Collection mapping
            if (IsCollectionType(sourceType) && IsCollectionType(destType))
                return MapCollection(sourceValue, sourceType, destType, depth, visited);

            // Nested object mapping
            var pair = new TypePair(sourceType, destType);
            if (_typeMaps.ContainsKey(pair))
                return MapObject(sourceValue, sourceType, destType, depth, visited);

            // Basic type conversion
            try
            {
                return Convert.ChangeType(sourceValue, underlyingDest ?? destType);
            }
            catch
            {
                return destType.IsValueType ? Activator.CreateInstance(destType) : null;
            }
        }

        private bool TryMapCollection<TSource, TDestination>(TSource source, int depth, HashSet<object>? visited, out TDestination result)
        {
            result = default!;
            var sourceType = typeof(TSource);
            var destType = typeof(TDestination);

            if (!IsCollectionType(sourceType) || !IsCollectionType(destType))
                return false;

            var mapped = MapCollection(source!, sourceType, destType, depth, visited);
            if (mapped is TDestination typedResult)
            {
                result = typedResult;
                return true;
            }

            return false;
        }

        private object MapCollection(object source, Type sourceType, Type destType, int depth, HashSet<object>? visited)
        {
            if (source is not IEnumerable sourceEnumerable)
                return default!;

            var destElementType = GetElementType(destType);
            var sourceElementType = GetElementType(sourceType);

            if (destElementType == null || sourceElementType == null)
                return default!;

            var listType = typeof(List<>).MakeGenericType(destElementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var item in sourceEnumerable)
            {
                if (item == null)
                {
                    list.Add(null);
                    continue;
                }

                list.Add(MapPropertyValue(item, sourceElementType, destElementType, depth, visited));
            }

            // Return as array if destination is array
            if (destType.IsArray)
            {
                var array = Array.CreateInstance(destElementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        private static Type? GetElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1)
                    return genericArgs[0];
            }

            // Check interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }

            return typeof(object);
        }

        private static bool IsCollectionType(Type type)
        {
            if (type == typeof(string)) return false;
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return Convert.ChangeType(value, underlying);
        }
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
