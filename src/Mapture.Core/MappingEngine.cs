using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Mapture
{
    internal static class TypePairCache<TSource, TDestination>
    {
        public static readonly TypePair Value = new(typeof(TSource), typeof(TDestination));
        // Per-engine fast delegate cache — set on first use, avoids dictionary lookup
        [ThreadStatic] public static Func<TSource, TDestination>? LastFunc;
        [ThreadStatic] public static MappingEngine? LastEngine;
    }

    internal sealed class MappingEngine
    {
        private readonly ConcurrentDictionary<TypePair, Func<object, object>> _compiledMapFuncs = new();
        private readonly ConcurrentDictionary<TypePair, Delegate> _compiledUpdateFuncs = new();
        // Typed delegates for Map<TSource, TDestination> — avoids boxing
        private readonly ConcurrentDictionary<TypePair, Delegate> _typedMapFuncs = new();
        // Fast-path delegates (no cycle/depth checks) for type pairs that cannot have cycles
        private readonly ConcurrentDictionary<TypePair, Func<object, object>> _fastMapFuncs = new();
        private readonly Dictionary<TypePair, TypeMap> _typeMaps;
        private readonly MaptureOptions _options;
        // Pre-computed set of type pairs that can have cycles (self-referencing graphs)
        private readonly HashSet<TypePair> _cyclicPairs;
        private readonly bool _hasCyclicPairs;

        [ThreadStatic] private static HashSet<object>? t_visited;
        [ThreadStatic] private static int t_depth;

        public MappingEngine(Dictionary<TypePair, TypeMap> typeMaps, MaptureOptions options)
        {
            _typeMaps = typeMaps;
            _options = options;
            _cyclicPairs = DetectCyclicPairs();
            _hasCyclicPairs = _cyclicPairs.Count > 0;
        }

        // ────────────────────────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────────────────────────

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            if (source == null)
                return default!;

            // Ultra-fast path: use thread-static cache keyed by engine identity
            var cache = TypePairCache<TSource, TDestination>.LastEngine;
            Func<TSource, TDestination>? fn;
            if (cache == this)
            {
                fn = TypePairCache<TSource, TDestination>.LastFunc!;
            }
            else
            {
                var pair = TypePairCache<TSource, TDestination>.Value;
                if (!_typedMapFuncs.TryGetValue(pair, out var cached))
                    cached = CompileAndCacheTyped<TSource, TDestination>(pair);
                fn = (Func<TSource, TDestination>)cached;
                TypePairCache<TSource, TDestination>.LastFunc = fn;
                TypePairCache<TSource, TDestination>.LastEngine = this;
            }

            return fn(source);
        }

        private Delegate CompileAndCacheTyped<TSource, TDestination>(TypePair pair)
        {
            Func<TSource, TDestination> fn;
            bool isCyclic = _hasCyclicPairs && _cyclicPairs.Contains(pair);

            if (!isCyclic)
            {
                // Compile a direct typed delegate — no overhead
                fn = CompileTypedMapFunc<TSource, TDestination>(pair);
            }
            else
            {
                // Wrap with cycle detection + depth tracking
                fn = (TSource src) =>
                {
                    bool isRoot = t_depth == 0;
                    try
                    {
                        if (_options.EnableCycleDetection && isRoot)
                        {
                            var set = t_visited;
                            if (set == null)
                            {
                                set = new HashSet<object>(ReferenceEqualityComparer.Instance);
                                t_visited = set;
                            }
                            else
                            {
                                set.Clear();
                            }
                        }
                        return (TDestination)MapTracked(src!, pair);
                    }
                    finally
                    {
                        if (isRoot)
                        {
                            t_depth = 0;
                            t_visited?.Clear();
                        }
                    }
                };
            }

            _typedMapFuncs.TryAdd(pair, fn);
            return fn;
        }

        private Func<TSource, TDestination> CompileTypedMapFunc<TSource, TDestination>(TypePair pair)
        {
            if (!_typeMaps.TryGetValue(pair, out var typeMap))
                throw new MaptureException($"No mapping configured for {typeof(TSource).Name} -> {typeof(TDestination).Name}");

            if (typeMap.CustomConverter != null)
                return (Func<TSource, TDestination>)typeMap.CustomConverter;

            var srcParam = Expression.Parameter(typeof(TSource), "src");
            var destVar = Expression.Variable(typeof(TDestination), "dest");
            var stmts = new List<Expression>();

            if (typeMap.CustomConstructor != null)
            {
                stmts.Add(Expression.Assign(destVar,
                    Expression.Invoke(Expression.Constant((Func<TSource, TDestination>)typeMap.CustomConstructor), srcParam)));
            }
            else
            {
                stmts.Add(Expression.Assign(destVar, Expression.New(typeof(TDestination))));
            }

            if (typeMap.BeforeMapAction is Action<TSource, TDestination> before)
                stmts.Add(Expression.Invoke(Expression.Constant(before), srcParam, destVar));

            EmitPropertyAssignments(typeof(TSource), typeof(TDestination), typeMap, srcParam, destVar, stmts, false);

            if (typeMap.AfterMapAction is Action<TSource, TDestination> after)
                stmts.Add(Expression.Invoke(Expression.Constant(after), srcParam, destVar));

            stmts.Add(destVar);

            var body = Expression.Block(new[] { destVar }, stmts);
            return Expression.Lambda<Func<TSource, TDestination>>(body, srcParam).Compile();
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null)
                return destination;

            var pair = TypePairCache<TSource, TDestination>.Value;
            if (!_compiledUpdateFuncs.TryGetValue(pair, out var cached))
            {
                if (!_typeMaps.TryGetValue(pair, out var typeMap))
                    throw new MaptureException($"No mapping configured for {typeof(TSource).Name} -> {typeof(TDestination).Name}");
                cached = CompileUpdateAction<TSource, TDestination>(typeMap);
                _compiledUpdateFuncs.TryAdd(pair, cached);
            }
            ((Action<TSource, TDestination>)cached)(source, destination);
            return destination;
        }

        public object Map(object source, Type sourceType, Type destinationType)
        {
            if (source == null)
                return default!;

            var pair = new TypePair(sourceType, destinationType);

            if (!_cyclicPairs.Contains(pair))
            {
                var fn = GetOrCompileFast(pair);
                return fn(source);
            }

            bool isRoot = t_depth == 0;
            try
            {
                if (_options.EnableCycleDetection && isRoot)
                {
                    var set = t_visited;
                    if (set == null)
                    {
                        set = new HashSet<object>(ReferenceEqualityComparer.Instance);
                        t_visited = set;
                    }
                    else
                    {
                        set.Clear();
                    }
                }

                return MapTracked(source, pair);
            }
            finally
            {
                if (isRoot)
                {
                    t_depth = 0;
                    t_visited?.Clear();
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Fast path (no cycle/depth tracking)
        // ────────────────────────────────────────────────────────────

        private Func<object, object> GetOrCompileFast(TypePair pair)
        {
            if (_fastMapFuncs.TryGetValue(pair, out var fn))
                return fn;

            fn = CompileUntypedFunc(pair);
            _fastMapFuncs.TryAdd(pair, fn);
            return fn;
        }

        // ────────────────────────────────────────────────────────────
        //  Tracked path (cycle detection + depth)
        // ────────────────────────────────────────────────────────────

        internal object MapTracked(object source, TypePair pair)
        {
            if (source == null)
                return default!;

            if (t_depth > _options.MaxDepth)
                return default!;

            if (_options.EnableCycleDetection && t_visited != null && !pair.SourceType.IsValueType)
            {
                if (!t_visited.Add(source))
                    return default!;
            }

            if (!_compiledMapFuncs.TryGetValue(pair, out var fn))
            {
                fn = CompileUntypedFunc(pair);
                _compiledMapFuncs.TryAdd(pair, fn);
            }

            t_depth++;
            try
            {
                return fn(source);
            }
            finally
            {
                t_depth--;
            }
        }

        // Called from compiled expression trees for nested properties in cyclic graphs
        internal object MapNestedTracked(object sourceValue, Type sourceType, Type destType)
        {
            return MapTracked(sourceValue, new TypePair(sourceType, destType));
        }

        // Called from compiled expression trees for nested properties in acyclic graphs
        internal object MapNestedFast(object sourceValue, Type sourceType, Type destType)
        {
            var pair = new TypePair(sourceType, destType);
            var fn = GetOrCompileFast(pair);
            return fn(sourceValue);
        }

        // ────────────────────────────────────────────────────────────
        //  Property value mapping (for collections/enums)
        // ────────────────────────────────────────────────────────────

        internal object? MapPropertyValue(object sourceValue, Type sourceType, Type destType)
        {
            if (destType.IsAssignableFrom(sourceType))
                return sourceValue;

            var underlyingDest = Nullable.GetUnderlyingType(destType);
            if (underlyingDest != null && underlyingDest.IsAssignableFrom(sourceType))
                return sourceValue;

            if (destType.IsEnum && sourceType.IsEnum)
                return Enum.ToObject(destType, sourceValue);
            if (destType.IsEnum && sourceType == typeof(string))
                return Enum.Parse(destType, (string)sourceValue, ignoreCase: true);
            if (destType.IsEnum && IsNumericType(sourceType))
                return Enum.ToObject(destType, sourceValue);

            if (IsCollectionType(sourceType) && IsCollectionType(destType))
                return MapCollectionUntyped(sourceValue, sourceType, destType);

            var pair = new TypePair(sourceType, destType);
            if (_typeMaps.ContainsKey(pair))
            {
                if (_cyclicPairs.Contains(pair))
                    return MapTracked(sourceValue, pair);
                return GetOrCompileFast(pair)(sourceValue);
            }

            try
            {
                return Convert.ChangeType(sourceValue, underlyingDest ?? destType);
            }
            catch
            {
                return destType.IsValueType ? Activator.CreateInstance(destType) : null;
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Cycle analysis — run once at configuration time
        // ────────────────────────────────────────────────────────────

        private HashSet<TypePair> DetectCyclicPairs()
        {
            var cyclic = new HashSet<TypePair>();
            if (!_options.EnableCycleDetection)
                return cyclic;

            foreach (var pair in _typeMaps.Keys)
            {
                if (CanReachSelf(pair.SourceType, new HashSet<Type>()))
                    cyclic.Add(pair);
            }
            return cyclic;
        }

        private bool CanReachSelf(Type rootType, HashSet<Type> visited)
        {
            if (!visited.Add(rootType))
                return true; // cycle detected!

            var props = rootType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                var propType = prop.PropertyType;
                if (propType.IsValueType || propType == typeof(string))
                    continue;

                // Check element type for collections
                if (IsCollectionType(propType))
                {
                    propType = GetElementType(propType) ?? propType;
                    if (propType.IsValueType || propType == typeof(string))
                        continue;
                }

                if (CanReachSelf(propType, new HashSet<Type>(visited)))
                    return true;
            }

            return false;
        }

        // ────────────────────────────────────────────────────────────
        //  Compile Func<object, object> for a type pair
        // ────────────────────────────────────────────────────────────

        private Func<object, object> CompileUntypedFunc(TypePair pair)
        {
            if (_typeMaps.TryGetValue(pair, out var typeMap))
                return CompileUntypedMapFunc(typeMap);

            if (pair.DestinationType.IsAssignableFrom(pair.SourceType))
                return static (object s) => s;

            throw new MaptureException($"No mapping configured for {pair.SourceType.Name} -> {pair.DestinationType.Name}");
        }

        private Func<object, object> CompileUntypedMapFunc(TypeMap typeMap)
        {
            if (typeMap.CustomConverter != null)
            {
                var converter = typeMap.CustomConverter;
                return (object src) => converter.DynamicInvoke(src)!;
            }

            var srcType = typeMap.SourceType;
            var destType = typeMap.DestinationType;
            var isCyclic = _cyclicPairs.Contains(new TypePair(srcType, destType));

            var objParam = Expression.Parameter(typeof(object), "obj");
            var srcVar = Expression.Variable(srcType, "src");
            var destVar = Expression.Variable(destType, "dest");
            var stmts = new List<Expression>();

            stmts.Add(Expression.Assign(srcVar, Expression.Convert(objParam, srcType)));

            if (typeMap.CustomConstructor != null)
            {
                stmts.Add(Expression.Assign(destVar,
                    Expression.Convert(
                        Expression.Invoke(Expression.Constant(typeMap.CustomConstructor), srcVar),
                        destType)));
            }
            else
            {
                stmts.Add(Expression.Assign(destVar, Expression.New(destType)));
            }

            if (typeMap.BeforeMapAction != null)
                stmts.Add(Expression.Invoke(Expression.Constant(typeMap.BeforeMapAction), srcVar, destVar));

            EmitPropertyAssignments(srcType, destType, typeMap, srcVar, destVar, stmts, isCyclic);

            if (typeMap.AfterMapAction != null)
                stmts.Add(Expression.Invoke(Expression.Constant(typeMap.AfterMapAction), srcVar, destVar));

            stmts.Add(Expression.Convert(destVar, typeof(object)));

            var body = Expression.Block(new[] { srcVar, destVar }, stmts);
            return Expression.Lambda<Func<object, object>>(body, objParam).Compile();
        }

        // ────────────────────────────────────────────────────────────
        //  Emit property assignment expressions
        // ────────────────────────────────────────────────────────────

        private void EmitPropertyAssignments(
            Type srcType, Type destType,
            TypeMap typeMap,
            Expression srcExpr, Expression destExpr,
            List<Expression> stmts,
            bool isCyclic)
        {
            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var srcPropDict = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in srcProps)
                srcPropDict[p.Name] = p;

            foreach (var destProp in destProps)
            {
                if (!destProp.CanWrite) continue;

                // Explicit member mapping
                if (typeMap.MemberMappings.TryGetValue(destProp.Name, out var mm))
                {
                    if (mm.IsIgnored) continue;

                    Expression? valueExpr = null;

                    if (mm.HasConstantValue)
                    {
                        valueExpr = Expression.Constant(mm.ConstantValue, destProp.PropertyType);
                    }
                    else if (mm.CustomResolver != null)
                    {
                        valueExpr = Expression.Convert(
                            Expression.Invoke(Expression.Constant(mm.CustomResolver), srcExpr),
                            destProp.PropertyType);
                    }
                    else if (mm.SourceExpression is LambdaExpression lambda)
                    {
                        var invokeExpr = Expression.Invoke(Expression.Constant(lambda.Compile()), srcExpr);
                        valueExpr = Expression.Convert(invokeExpr, destProp.PropertyType);
                    }

                    if (valueExpr != null)
                    {
                        Expression assignStmt = Expression.Assign(
                            Expression.Property(destExpr, destProp), valueExpr);

                        if (mm.Condition != null)
                        {
                            var condExpr = Expression.Invoke(Expression.Constant(mm.Condition), srcExpr);
                            assignStmt = Expression.IfThen(condExpr, assignStmt);
                        }

                        stmts.Add(assignStmt);
                        continue;
                    }
                }

                // Auto-map by name
                if (!srcPropDict.TryGetValue(destProp.Name, out var srcProp) || !srcProp.CanRead)
                    continue;

                var srcAccess = Expression.Property(srcExpr, srcProp);
                var destAccess = Expression.Property(destExpr, destProp);

                if (destProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                {
                    stmts.Add(Expression.Assign(destAccess, srcAccess));
                }
                else if (_typeMaps.ContainsKey(new TypePair(srcProp.PropertyType, destProp.PropertyType)))
                {
                    EmitNestedObjectMapping(srcAccess, destAccess, srcProp, destProp, stmts, isCyclic);
                }
                else if (IsCollectionType(srcProp.PropertyType) && IsCollectionType(destProp.PropertyType))
                {
                    EmitCollectionMapping(srcAccess, destAccess, srcProp, destProp, stmts);
                }
                else
                {
                    EmitFallbackMapping(srcAccess, destAccess, srcProp, destProp, stmts);
                }
            }
        }

        private void EmitNestedObjectMapping(
            Expression srcAccess, Expression destAccess,
            PropertyInfo srcProp, PropertyInfo destProp,
            List<Expression> stmts, bool isCyclic)
        {
            var engineConst = Expression.Constant(this);

            if (!isCyclic)
            {
                // Fast path: pre-compile the nested delegate and embed as constant
                // This avoids TypePair allocation + dictionary lookup per call
                var nestedPair = new TypePair(srcProp.PropertyType, destProp.PropertyType);
                var nestedFn = GetOrCompileFast(nestedPair);
                var fnConst = Expression.Constant(nestedFn);

                Expression srcAsObj = Expression.Convert(srcAccess, typeof(object));
                var callMap = Expression.Convert(
                    Expression.Invoke(fnConst, srcAsObj),
                    destProp.PropertyType);

                if (!srcProp.PropertyType.IsValueType)
                {
                    stmts.Add(Expression.Assign(destAccess,
                        Expression.Condition(
                            Expression.Equal(srcAccess, Expression.Constant(null, srcProp.PropertyType)),
                            Expression.Default(destProp.PropertyType),
                            callMap)));
                }
                else
                {
                    stmts.Add(Expression.Assign(destAccess, callMap));
                }
            }
            else
            {
                // Cyclic path: must go through MapNestedTracked for depth/cycle checks
                var helperMethod = typeof(MappingEngine).GetMethod(nameof(MapNestedTracked), BindingFlags.NonPublic | BindingFlags.Instance)!;

                Expression srcAsObj = Expression.Convert(srcAccess, typeof(object));
                var callMap = Expression.Convert(
                    Expression.Call(engineConst, helperMethod,
                        srcAsObj,
                        Expression.Constant(srcProp.PropertyType),
                        Expression.Constant(destProp.PropertyType)),
                    destProp.PropertyType);

                if (!srcProp.PropertyType.IsValueType)
                {
                    stmts.Add(Expression.Assign(destAccess,
                        Expression.Condition(
                            Expression.Equal(srcAccess, Expression.Constant(null, srcProp.PropertyType)),
                            Expression.Default(destProp.PropertyType),
                            callMap)));
                }
                else
                {
                    stmts.Add(Expression.Assign(destAccess, callMap));
                }
            }
        }

        private void EmitCollectionMapping(
            Expression srcAccess, Expression destAccess,
            PropertyInfo srcProp, PropertyInfo destProp,
            List<Expression> stmts)
        {
            var engineConst = Expression.Constant(this);
            var helperMethod = typeof(MappingEngine)
                .GetMethod(nameof(MapPropertyValue), BindingFlags.NonPublic | BindingFlags.Instance)!;

            Expression srcAsObj = Expression.Convert(srcAccess, typeof(object));
            var callMap = Expression.Convert(
                Expression.Call(engineConst, helperMethod,
                    srcAsObj,
                    Expression.Constant(srcProp.PropertyType),
                    Expression.Constant(destProp.PropertyType)),
                destProp.PropertyType);

            if (!srcProp.PropertyType.IsValueType)
            {
                stmts.Add(Expression.Assign(destAccess,
                    Expression.Condition(
                        Expression.Equal(srcAccess, Expression.Constant(null, srcProp.PropertyType)),
                        Expression.Default(destProp.PropertyType),
                        callMap)));
            }
            else
            {
                stmts.Add(Expression.Assign(destAccess, callMap));
            }
        }

        private void EmitFallbackMapping(
            Expression srcAccess, Expression destAccess,
            PropertyInfo srcProp, PropertyInfo destProp,
            List<Expression> stmts)
        {
            var engineConst = Expression.Constant(this);
            var helperMethod = typeof(MappingEngine)
                .GetMethod(nameof(MapPropertyValue), BindingFlags.NonPublic | BindingFlags.Instance)!;

            Expression srcAsObj = Expression.Convert(srcAccess, typeof(object));
            stmts.Add(Expression.Assign(destAccess,
                Expression.Convert(
                    Expression.Call(engineConst, helperMethod,
                        srcAsObj,
                        Expression.Constant(srcProp.PropertyType),
                        Expression.Constant(destProp.PropertyType)),
                    destProp.PropertyType)));
        }

        // ────────────────────────────────────────────────────────────
        //  Compile update Action<TSrc, TDest>
        // ────────────────────────────────────────────────────────────

        private Action<TSource, TDestination> CompileUpdateAction<TSource, TDestination>(TypeMap typeMap)
        {
            var srcParam = Expression.Parameter(typeof(TSource), "src");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");
            var stmts = new List<Expression>();

            var srcType = typeof(TSource);
            var destType = typeof(TDestination);
            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var srcPropDict = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in srcProps)
                srcPropDict[p.Name] = p;

            foreach (var destProp in destProps)
            {
                if (!destProp.CanWrite) continue;

                if (typeMap.MemberMappings.TryGetValue(destProp.Name, out var mm))
                {
                    if (mm.IsIgnored) continue;

                    if (mm.HasConstantValue)
                    {
                        stmts.Add(Expression.Assign(
                            Expression.Property(destParam, destProp),
                            Expression.Constant(mm.ConstantValue, destProp.PropertyType)));
                        continue;
                    }

                    if (mm.CustomResolver != null)
                    {
                        stmts.Add(Expression.Assign(
                            Expression.Property(destParam, destProp),
                            Expression.Convert(
                                Expression.Invoke(Expression.Constant(mm.CustomResolver), srcParam),
                                destProp.PropertyType)));
                        continue;
                    }
                }

                if (!srcPropDict.TryGetValue(destProp.Name, out var srcProp) || !srcProp.CanRead)
                    continue;

                var srcAccess = Expression.Property(srcParam, srcProp);
                var destAccess = Expression.Property(destParam, destProp);

                if (destProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                {
                    stmts.Add(Expression.Assign(destAccess, srcAccess));
                }
                else
                {
                    var engineConst = Expression.Constant(this);
                    var helperMethod = typeof(MappingEngine)
                        .GetMethod(nameof(MapPropertyValue), BindingFlags.NonPublic | BindingFlags.Instance)!;

                    Expression srcAsObj = Expression.Convert(srcAccess, typeof(object));
                    stmts.Add(Expression.Assign(destAccess,
                        Expression.Convert(
                            Expression.Call(engineConst, helperMethod,
                                srcAsObj,
                                Expression.Constant(srcProp.PropertyType),
                                Expression.Constant(destProp.PropertyType)),
                            destProp.PropertyType)));
                }
            }

            if (stmts.Count == 0)
                stmts.Add(Expression.Empty());

            var body = Expression.Block(stmts);
            return Expression.Lambda<Action<TSource, TDestination>>(body, srcParam, destParam).Compile();
        }

        // ────────────────────────────────────────────────────────────
        //  Collection mapping
        // ────────────────────────────────────────────────────────────

        private object MapCollectionUntyped(object source, Type sourceType, Type destType)
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

                list.Add(MapPropertyValue(item, sourceElementType, destElementType));
            }

            if (destType.IsArray)
            {
                var array = Array.CreateInstance(destElementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        // ────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────

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

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }

            return typeof(object);
        }

        internal static bool IsCollectionType(Type type)
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
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
