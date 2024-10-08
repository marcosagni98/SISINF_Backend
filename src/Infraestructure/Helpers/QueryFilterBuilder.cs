﻿using Domain.Dtos.CommonDtos.Request;
using Domain.Entities.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace Infraestructure.Helpers;

public class QueryFilterBuilder<T>
{
    private IQueryable<T> _query;
    private Expression<Func<T, bool>> _whereClause;
    private Expression<Func<T, bool>> _whereActiveClause;
    private string _sortBy;
    private string _sortOrder;
    private int _pageSize;
    private int _pageNumber;

    public QueryFilterBuilder(IQueryable<T> query)
    {
        _query = query;
        _whereClause = null;
        _whereActiveClause = null;
        _sortBy = null;
        _sortOrder = null;
        _pageSize = 10;
        _pageNumber = 1;
    }

    /// <summary>
    /// Builds a where clause for the query based on the properties and value provided.
    /// </summary>
    /// <param name="filterParameterNames">A list of property names to filter on</param>
    /// <param name="value">The value to be filtered</param>
    /// <returns>The query filter builder instance</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    public QueryFilterBuilder<T> Where(List<string>? filterParameterNames, object? value)
    {
        ValidateParemeters(value);

        var parameterExpression = Expression.Parameter(typeof(T), "x");

        var propertiesToFilter = (filterParameterNames == null || filterParameterNames.Count == 0)
        ? typeof(T).GetProperties().Select(p => p.Name).ToList()
        : filterParameterNames;

        var filterExpressions = CreateFilterExpressions(parameterExpression, propertiesToFilter, value!);
        var combinedExpression = CombineExpression(filterExpressions);

        var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameterExpression);
        _query = _query.Where(lambda);
        _whereClause = lambda;

        return this;
    }

    #region Where private functions

    /// <summary>
    /// Validates the provided parameters for the Where method.
    /// </summary>
    /// <param name="filterParameterNames">A list of property names to filter on</param>
    /// <exception cref="ArgumentException">Thrown when parameter names are null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when filter value is null or empty.</exception>
    private void ValidateParemeters(object? value)
    {
        if (string.IsNullOrEmpty(value?.ToString()))
        {
            throw new ArgumentNullException(nameof(value), "Filter value cannot be null or empty.");
        }
    }

    /// <summary>
    /// Creates filter expressions for the specified property names and filter value.
    /// </summary>
    /// <param name="parameterExpression">The parameter expression representing the object being queried.</param>
    /// <param name="filterParameterNames">The property names to filter on.</param>
    /// <param name="value">The filter value.</param>
    /// <returns>A list of combined filter expressions.</returns>
    private List<Expression> CreateFilterExpressions(ParameterExpression parameterExpression, List<string> filterParameterNames, object value)
    {
        var orExpressions = new List<Expression>();
        var filterValue = value.ToString();
        var containsMethod = GetContainsMethod();

        foreach (var filterParameterName in filterParameterNames)
        {
            var propertyExpression = Expression.Property(parameterExpression, filterParameterName);
            var containsExpression = CreateContainsExpression(propertyExpression, containsMethod, filterValue);
            orExpressions.Add(containsExpression);
        }

        return orExpressions;
    }

    /// <summary>
    /// Retrieves the 'Contains' method info for string comparisons.
    /// </summary>
    /// <returns>The MethodInfo for the string 'Contains' method.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Contains method is not found.</exception>
    private Expression CombineExpression(List<Expression> orExpressions)
    {
        return orExpressions.Count == 1
            ? orExpressions[0]
            : orExpressions.Aggregate(Expression.Or);
    }

    /// <summary>
    /// Retrieves the 'Contains' method info for string comparisons.
    /// </summary>
    /// <returns>The MethodInfo for the string 'Contains' method.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Contains method is not found.</exception>
    private MethodInfo GetContainsMethod()
    {
        return typeof(string).GetMethod("Contains", new[] { typeof(string) })
            ?? throw new InvalidOperationException("Contains method not found on type 'string'.");
    }


    /// <summary>
    /// Creates a 'Contains' expression for a given property and filter value.
    /// </summary>
    /// <param name="propertyExpression">The property expression to apply the Contains method to.</param>
    /// <param name="containsMethod">The MethodInfo for the Contains method.</param>
    /// <param name="filterValue">The value to filter by.</param>
    /// <returns>A contains expression for the property.</returns>
    private Expression CreateContainsExpression(Expression propertyExpression, MethodInfo containsMethod, string filterValue)
    {
        return propertyExpression.Type == typeof(string)
            ? Expression.Call(propertyExpression, containsMethod, Expression.Constant(filterValue))
            : Expression.Call(Expression.Convert(propertyExpression, typeof(string)), containsMethod, Expression.Constant(filterValue));
    }
    #endregion


    /// <summary>
    /// Builds the where clause for the query based on the 'Active' property.
    /// </summary>
    /// <returns>The query filter builder instance</returns>
    public QueryFilterBuilder<T> WhereActive()
    {
        _query = _query.Where(x => (x as Entity).Active == true);
        _whereActiveClause = x => (x as Entity).Active == true;

        return this;
    }

    /// <summary>
    /// Sorts the query results based on the specified property and order.
    /// </summary>
    /// <param name="sortBy">The property name to sort by.</param>
    /// <param name="sortOrder">The order of sorting ("asc" for ascending or "desc" for descending).</param>
    /// <returns>The query filter builder instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the sortBy property is null or empty.</exception>
    public QueryFilterBuilder<T> SortBy(string sortBy, string sortOrder)
    {
        ValidateSortByParameter(sortBy);
        ValidateSortOrderParameter(sortOrder);

        var parameterExpression = Expression.Parameter(typeof(T), "x");
        var propertyExpression = CreatePropertyExpression(sortBy, parameterExpression);
        var sortingMethod = GetSortingMethod(sortOrder);

        var methodCallExpression = Expression.Call(
            typeof(Queryable),
            sortingMethod,
            new Type[] { typeof(T), propertyExpression.Type },
            _query.Expression,
            Expression.Quote(Expression.Lambda(propertyExpression, parameterExpression))
        );

        _query = _query.Provider.CreateQuery<T>(methodCallExpression);

        _sortBy = sortBy;
        _sortOrder = sortOrder;
        return this;
    }

    #region SortBy private functions

    /// <summary>
    /// Validates the sortOrder parameter to ensure it is either "asc" or "desc".
    /// </summary>
    /// <param name="sortOrder">The order of sorting.</param>
    /// <exception cref="ArgumentException">Thrown when sortOrder is invalid.</exception>
    private void ValidateSortOrderParameter(string sortOrder)
    {
        if (!string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("SortOrder must be either 'asc' or 'desc'.", nameof(sortOrder));
        }
    }

    /// <summary>
    /// Validates the sortBy parameter.
    /// </summary>
    /// <param name="sortBy">The property name to sort by.</param>
    /// <exception cref="ArgumentNullException">Thrown when sortBy is null or empty.</exception>
    private void ValidateSortByParameter(string sortBy)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            throw new ArgumentNullException(nameof(sortBy), "SortBy property cannot be null or empty.");
        }
    }

    /// <summary>
    /// Creates a property expression for the specified property name.
    /// </summary>
    /// <param name="sortBy">The property name to create the expression for.</param>
    /// <param name="parameterExpression">The parameter expression representing the object being queried.</param>
    /// <returns>The property expression.</returns>
    private Expression CreatePropertyExpression(string sortBy, ParameterExpression parameterExpression)
    {
        return Expression.Property(parameterExpression, sortBy);
    }

    /// <summary>
    /// Determines the appropriate sorting method based on the sort order.
    /// </summary>
    /// <param name="sortOrder">The order of sorting ("asc" or "desc").</param>
    /// <returns>The name of the sorting method.</returns>
    private string GetSortingMethod(string sortOrder)
    {
        return string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase)
            ? "OrderByDescending"
            : "OrderBy";
    }
    #endregion

    /// <summary>
    /// Skips a specified number of records in the query results.
    /// </summary>
    /// <param name="skip">The number of records to skip.</param>
    /// <returns>The current instance of the QueryFilterBuilder.</returns>
    private QueryFilterBuilder<T> Skip(int skip)
    {
        if (skip >= 0)
        {
            _query = _query.Skip(skip);
        }
        return this;
    }


    /// <summary>
    /// Takes a specified number of records from the query results.
    /// </summary>
    /// <param name="take">The number of records to take.</param>
    /// <returns>The current instance of the QueryFilterBuilder.</returns>
    private QueryFilterBuilder<T> Take(int take)
    {
        if (take > 0)
        {
            _query = _query.Take(take);
        }
        return this;
    }

    /// <summary>
    /// Applies pagination to the query results based on the specified page number and page size.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <returns>The current instance of the QueryFilterBuilder.</returns>
    private QueryFilterBuilder<T> Paginate(int pageNumber, int pageSize)
    {
        _pageNumber = pageNumber;
        _pageSize = pageSize;

        _query = _query.Skip((pageNumber - 1) * pageSize).Take(pageSize);

        return this;
    }

    /// <summary>
    /// Applies the specified query filter to the current query based on the provided filter parameters.
    /// </summary>
    /// <param name="queryFilter">An optional QueryFilterDto object containing filter criteria.</param>
    /// <param name="filterParameters">A list of property names to filter on.</param>
    /// <returns>The current instance of the QueryFilterBuilder.</returns>
    public QueryFilterBuilder<T> ApplyQueryFilter(QueryFilterDto? queryFilter, List<string>? filterParameters)
    {
        // If the query filter is null, return the current instance without making changes.
        if (queryFilter == null)
        {
            return this;
        }

        // If a search term is provided and there are filter parameters, apply the filtering.
        if (!string.IsNullOrWhiteSpace(queryFilter.Search))
        {
            Where(filterParameters, queryFilter.Search);
        }

        // If order by and order direction are provided, apply sorting.
        if (!string.IsNullOrWhiteSpace(queryFilter.OrderBy) && !string.IsNullOrWhiteSpace(queryFilter.OrderDirection))
        {
            SortBy(queryFilter.OrderBy, queryFilter.OrderDirection);
        }

        // If valid pagination parameters are provided, apply pagination.
        if (queryFilter.PageNumber > 0 && queryFilter.PageSize > 0)
        {
            Paginate(queryFilter.PageNumber, queryFilter.PageSize);
        }
        return this;
    }


    /// <summary>
    /// Applies the active filter along with the specified query filter.
    /// </summary>
    /// <param name="queryFilter">An optional QueryFilterDto object containing filter criteria.</param>
    /// <param name="filterParameters">A list of property names to filter on.</param>
    /// <returns>The current instance of the QueryFilterBuilder.</returns>
    public QueryFilterBuilder<T> ApplyQueryFilterAndActive(QueryFilterDto? queryFilter, List<string>? filterParameters)
    {
        WhereActive();
        ApplyQueryFilter(queryFilter, filterParameters);
        
        return this;
    }


    /// <summary>
    /// Builds and returns the final query as an IQueryable of type <see cref="T"/>.
    /// </summary>
    /// <returns>An IQueryable of the specified type <typeparamref name="T"/>.</returns>
    public IQueryable<T> Build()
    {
        return _query;
    }
}