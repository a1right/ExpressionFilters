﻿using PagedRequestBuilder.Cache;
using PagedRequestBuilder.Models;
using PagedRequestBuilder.Models.Filter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace PagedRequestBuilder.Builders
{
    public class FilterBuilder<T> : IFilterBuilder<T> where T : class
    {
        private readonly IPagedRequestValueParser _valueParser;
        private readonly IPagedRequestPropertyMapper _propertyMapper;
        private readonly IQueryFilterCache<T> _queryFilterCache;
        public FilterBuilder(IPagedRequestValueParser valueParser, IPagedRequestPropertyMapper propertyMapper, IQueryFilterCache<T> queryFilterCache)
        {
            _valueParser = valueParser;
            _propertyMapper = propertyMapper;
            _queryFilterCache = queryFilterCache;
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
                var constant = Expression.Constant(_valueParser.GetValue(entry.Value, assignablePropertyType));
                var newExpression = GetOperationExpression(propertySelector, constant, entry.Operation, assignablePropertyType);
                return Expression.Lambda<Func<T, bool>>(newExpression, parameter);
            }

            catch (Exception ex)
            {
                return null;
            }
        }

        private Expression GetOperationExpression(MemberExpression left, ConstantExpression right, string operation, Type assignablePropertyType)
        {
            return operation switch
            {
                "=" => Expression.Equal(left, right),
                ">" => Expression.GreaterThan(left, right),
                ">=" => Expression.GreaterThanOrEqual(left, right),
                "<" => Expression.LessThan(left, right),
                "<=" => Expression.LessThanOrEqual(left, right),
                "!=" => Expression.NotEqual(left, right),
                "contains" when assignablePropertyType == typeof(string) => Expression.Call(left, typeof(string).GetMethod("Contains", new[] { typeof(string) }), right),
                "contains" when assignablePropertyType.IsArray => Expression.Call(GetArrayLinqMethodInfo("Contains", assignablePropertyType), left, right),
                "contains" when typeof(IEnumerable).IsAssignableFrom(assignablePropertyType) => Expression.Call(GetLinqMethodInfo("Contains", assignablePropertyType), left, right),

                _ => throw new NotImplementedException()
            };
        }

        private MethodInfo GetLinqMethodInfo(string name, Type assignablePropertyType)
        {
            return typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(x => x.Name == name && x.GetParameters().Length == 2)
            .MakeGenericMethod(assignablePropertyType.GetGenericArguments().First());
        }

        private MethodInfo GetArrayLinqMethodInfo(string name, Type assignablePropertyType)
        {
            return typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(x => x.Name == name && x.GetParameters().Length == 2)
            .MakeGenericMethod(assignablePropertyType.GetElementType());
        }
    }

    public interface IFilterBuilder<T> where T : class
    {
        IEnumerable<IQueryFilter<T>> BuildFilters(PagedRequestBase<T>? request);
    }
}