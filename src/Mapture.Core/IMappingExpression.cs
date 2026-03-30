using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Mapture
{
    public interface IMappingExpression<TSource, TDestination>
    {
        IMappingExpression<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destinationMember,
            Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

        IMappingExpression<TSource, TDestination> Ignore<TMember>(
            Expression<Func<TDestination, TMember>> destinationMember);

        IMappingExpression<TSource, TDestination> ReverseMap();

        IMappingExpression<TSource, TDestination> ConvertUsing(
            Func<TSource, TDestination> converter);

        IMappingExpression<TSource, TDestination> BeforeMap(
            Action<TSource, TDestination> beforeFunction);

        IMappingExpression<TSource, TDestination> AfterMap(
            Action<TSource, TDestination> afterFunction);

        IMappingExpression<TSource, TDestination> ConstructUsing(
            Func<TSource, TDestination> constructor);
    }

    public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
    {
        void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);
        void MapFrom(Func<TSource, TMember> resolver);
        void Ignore();
        void UseValue(TMember value);
        void Condition(Func<TSource, bool> condition);
    }
}
