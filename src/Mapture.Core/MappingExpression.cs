using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Mapture
{
    internal sealed class MemberMapping
    {
        public string DestinationMemberName { get; set; } = default!;
        public bool IsIgnored { get; set; }
        public Delegate? CustomResolver { get; set; }
        public LambdaExpression? SourceExpression { get; set; }
        public object? ConstantValue { get; set; }
        public bool HasConstantValue { get; set; }
        public Delegate? Condition { get; set; }
    }

    internal sealed class TypeMap
    {
        public Type SourceType { get; }
        public Type DestinationType { get; }
        public Dictionary<string, MemberMapping> MemberMappings { get; } = new();
        public Delegate? CustomConverter { get; set; }
        public Delegate? BeforeMapAction { get; set; }
        public Delegate? AfterMapAction { get; set; }
        public Delegate? CustomConstructor { get; set; }
        public bool HasReverseMap { get; set; }

        public TypeMap(Type sourceType, Type destinationType)
        {
            SourceType = sourceType;
            DestinationType = destinationType;
        }
    }

    public sealed class MappingExpression<TSource, TDestination> : IMappingExpression<TSource, TDestination>
    {
        internal TypeMap TypeMap { get; }
        private readonly Action<TypeMap>? _onReverseMap;

        internal MappingExpression(TypeMap typeMap, Action<TypeMap>? onReverseMap = null)
        {
            TypeMap = typeMap;
            _onReverseMap = onReverseMap;
        }

        public IMappingExpression<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destinationMember,
            Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions)
        {
            var memberName = GetMemberName(destinationMember);
            var config = new MemberConfigurationExpression<TSource, TDestination, TMember>(memberName);
            memberOptions(config);

            TypeMap.MemberMappings[memberName] = config.ToMemberMapping();
            return this;
        }

        public IMappingExpression<TSource, TDestination> Ignore<TMember>(
            Expression<Func<TDestination, TMember>> destinationMember)
        {
            var memberName = GetMemberName(destinationMember);
            TypeMap.MemberMappings[memberName] = new MemberMapping
            {
                DestinationMemberName = memberName,
                IsIgnored = true
            };
            return this;
        }

        public IMappingExpression<TSource, TDestination> ReverseMap()
        {
            TypeMap.HasReverseMap = true;
            var reverseTypeMap = new TypeMap(typeof(TDestination), typeof(TSource));
            _onReverseMap?.Invoke(reverseTypeMap);
            return this;
        }

        public IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination> converter)
        {
            TypeMap.CustomConverter = converter;
            return this;
        }

        public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> beforeFunction)
        {
            TypeMap.BeforeMapAction = beforeFunction;
            return this;
        }

        public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> afterFunction)
        {
            TypeMap.AfterMapAction = afterFunction;
            return this;
        }

        public IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> constructor)
        {
            TypeMap.CustomConstructor = constructor;
            return this;
        }

        private static string GetMemberName<TMember>(Expression<Func<TDestination, TMember>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            throw new ArgumentException("Expression must be a member access expression.", nameof(expression));
        }
    }

    internal sealed class MemberConfigurationExpression<TSource, TDestination, TMember>
        : IMemberConfigurationExpression<TSource, TDestination, TMember>
    {
        private readonly string _memberName;
        private LambdaExpression? _sourceExpression;
        private Delegate? _customResolver;
        private bool _isIgnored;
        private TMember? _constantValue;
        private bool _hasConstantValue;
        private Delegate? _condition;

        public MemberConfigurationExpression(string memberName)
        {
            _memberName = memberName;
        }

        public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        {
            _sourceExpression = sourceMember;
        }

        public void MapFrom(Func<TSource, TMember> resolver)
        {
            _customResolver = resolver;
        }

        public void Ignore()
        {
            _isIgnored = true;
        }

        public void UseValue(TMember value)
        {
            _constantValue = value;
            _hasConstantValue = true;
        }

        public void Condition(Func<TSource, bool> condition)
        {
            _condition = condition;
        }

        internal MemberMapping ToMemberMapping()
        {
            return new MemberMapping
            {
                DestinationMemberName = _memberName,
                IsIgnored = _isIgnored,
                CustomResolver = _customResolver,
                SourceExpression = _sourceExpression,
                ConstantValue = _constantValue,
                HasConstantValue = _hasConstantValue,
                Condition = _condition,
            };
        }
    }
}
