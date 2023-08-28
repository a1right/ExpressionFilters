﻿using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using PagedRequestBuilder.Builders;
using PagedRequestBuilder.Extensions;
using PagedRequestBuilder.Models;
using Example = PagedRequestBuilder.Models.Example;

namespace PagedRequestBuilder.Services;

public class ExampleService : IExampleService
{
    private readonly ExampleContext _context;
    private readonly ExampleMongoContext _exampleMongoContext;
    private readonly IPagedQueryBuilder<Example> _queryBuilder;
    private readonly IPagedQueryBuilder<ExampleDocument> _queryMongoBuilder;

    public ExampleService(ExampleContext context, ExampleMongoContext exampleMongoContext, IPagedQueryBuilder<Example> queryBuilder, IPagedQueryBuilder<ExampleDocument> queryMongoBuilder)
    {
        _context = context;
        _exampleMongoContext = exampleMongoContext;
        _queryBuilder = queryBuilder;
        _queryMongoBuilder = queryMongoBuilder;
    }

    public async Task<PagedResponse<ExampleDto>> GetPaged(GetPagedExampleRequest? request)
    {
        request ??= new();
        var query = _context.Set<Example>();
        var pagedQuery = _queryBuilder
            .BuildQuery(query, request, 1, 100)
            .Include(x => x.Inner)
            .ThenInclude(x => x!.Nested)
            .Select(x => x.Map<Example, ExampleDto>());

        Console.WriteLine(query.ToQueryString());

        var data = await pagedQuery.ToListAsync();
        var total = await pagedQuery.CountAsync();

        return data.ToPagedResponse(request.Page, request.Size, total);
    }

    public async Task<PagedResponse<ExampleDocument>> GetPaged(GetPagedExampleDocument? request)
    {
        request ??= new();

        var query = _exampleMongoContext.Examples;
        query = _queryMongoBuilder.BuildQuery(query, request);
        var data = await (await query.ToCursorAsync()).ToListAsync();
        var total = await query.CountAsync();
        return data.ToPagedResponse(request.Page, request.Size, total);
    }
}

public interface IExampleService
{
    Task<PagedResponse<ExampleDto>> GetPaged(GetPagedExampleRequest? request);
    Task<PagedResponse<ExampleDocument>> GetPaged(GetPagedExampleDocument? request);
}
