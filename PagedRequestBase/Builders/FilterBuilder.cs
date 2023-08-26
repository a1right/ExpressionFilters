﻿using PagedRequestBuilder.Cache;
using PagedRequestBuilder.Common;
using PagedRequestBuilder.Common.ValueParser;
using PagedRequestBuilder.Common.ValueParser.Models;
using PagedRequestBuilder.Constant;
using PagedRequestBuilder.Models;
using PagedRequestBuilder.Models.Filter;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace PagedRequestBuilder.Builders;

public class FilterBuilder<T> : IFilterBuilder<T> where T : class
{
    private readonly IPagedRequestValueParser _valueParser;
    private readonly IPagedRequestPropertyMapper _propertyMapper;
    private readonly IQueryFilterCache<T> _queryFilterCache;
    private readonly IMethodCallExpressionBuilder _methodCallExpressionBuilder;

    public FilterBuilder(
        IPagedRequestValueParser valueParser,
        IPagedRequestPropertyMapper propertyMapper,
        IQueryFilterCache<T> queryFilterCache,
        IMethodCallExpressionBuilder methodCallExpressionBuilder)
    {
        _valueParser = valueParser;
        _propertyMapper = propertyMapper;
        _queryFilterCache = queryFilterCache;
        _methodCallExpressionBuilder = methodCallExpressionBuilder;
    }
    public IEnumerable<IQueryFilter<T>> BuildFilters(PagedRequestBase<T>? request)
    {
        if (request is null or { Filters: not { Count: > 0 } })
            yield break;

        foreach (var filter in request.Filters)
        {
            var cached = _queryFilterCache.Get(filter);
            if (cached is not null)
            {
                yield return cached;
                continue;
            }

            var predicate = GetPredicate(filter);

            if (predicate != null)
            {
                var queryFilter = new QueryFilter<T>(predicate);
                yield return queryFilter;
                _queryFilterCache.Set(filter, queryFilter);
            }
        }
    }

    private Expression<Func<T, bool>>? GetPredicate(FilterEntry entry)
    {
        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var typePropertyName = _propertyMapper.MapRequestNameToPropertyName<T>(entry.Property);
            var propertySelector = Expression.PropertyOrField(parameter, typePropertyName);
            var assignablePropertyType = typeof(T).GetProperty(typePropertyName).PropertyType;

            if (entry.Nested is not null)
            {
                foreach (var nested in entry.Nested)
                {
                    typePropertyName = _propertyMapper.MapNestedRequestNameToPropertyName<T>(nested, assignablePropertyType);
                    propertySelector = Expression.PropertyOrField(propertySelector, typePropertyName);
                    assignablePropertyType = assignablePropertyType.GetProperty(typePropertyName).PropertyType;
                }
            }

            var providedValue = _valueParser.GetValue(entry.Value, assignablePropertyType);
            var constant = Expression.Constant(providedValue, typeof(ValueParseResult));
            var constantClojure = Expression.Property(constant, nameof(ValueParseResult.Value));
            var converted = Expression.Convert(constantClojure, providedValue.ValueType);
            var newExpression = GetOperationExpression(propertySelector, converted, entry.Operation, assignablePropertyType);
            return Expression.Lambda<Func<T, bool>>(newExpression, parameter);
        }

        catch (Exception ex)
        {
            return null;
        }
    }

    private Expression GetOperationExpression(Expression left, Expression right, string operation, Type assignablePropertyType) => operation switch
    {
        Constants.RequestOperations.Equal => Expression.Equal(left, right),
        Constants.RequestOperations.GreaterThen => Expression.GreaterThan(left, right),
        Constants.RequestOperations.GreaterThenOrEquals => Expression.GreaterThanOrEqual(left, right),
        Constants.RequestOperations.LessThen => Expression.LessThan(left, right),
        Constants.RequestOperations.LessThenOrEqual => Expression.LessThanOrEqual(left, right),
        Constants.RequestOperations.NotEqual => Expression.NotEqual(left, right),
        Constants.RequestOperations.Contains => _methodCallExpressionBuilder.Build(Constants.MethodInfoNames.Contains, left, right, assignablePropertyType),
        Constants.RequestOperations.In => _methodCallExpressionBuilder.Build(Constants.MethodInfoNames.Contains, right, left, right.Type),

        _ => throw new NotImplementedException()
    };

}

public interface IFilterBuilder<T> where T : class
{
    IEnumerable<IQueryFilter<T>> BuildFilters(PagedRequestBase<T>? request);
}
