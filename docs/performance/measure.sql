/*
  Performance measurement scenarios – the main API queries on 100,000 requests.

  Each statement is written exactly as EF Core 10 sends it (sp_executesql with typed parameters;
  compare with results/ef-sql.log, captured from the running API). Parameterized, so the optimizer
  sees the same thing it sees in production.

  Run in SSMS: connect to localhost,14333 (sa), database RequestsManagement, turn on
  "Include Actual Execution Plan" (Ctrl+M), run the whole file or one scenario, and look at the
  Messages tab (logical reads, CPU) and the Execution plan tab.

  scripts/perf/measure.py runs the same scenarios automatically, with and without each index,
  and writes docs/performance/results/ and docs/performance/plans/.
*/
SET NOCOUNT ON;
SET STATISTICS IO, TIME ON;
GO

-- @scenario list-default: List, no filter, newest first, page 1 (20 rows) – the screen every user opens
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY', N'@p int,@p1 int', @p = 0, @p1 = 20;
GO

-- @scenario list-status: One status (Waiting), newest first, page 1
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Status] = @values1', N'@values1 tinyint', @values1 = 2;
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Status] = @values1
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@values1 tinyint,@p int,@p0 int', @values1 = 2, @p = 0, @p0 = 20;
GO

-- @scenario list-status-priority-date: Two statuses + priority High + created since 2025-06-01, newest first
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Status] IN (@values2, @values3) AND [r].[Priority] = @values11 AND [r].[CreatedAt] >= @fromUtc', N'@values2 tinyint,@values3 tinyint,@values11 tinyint,@fromUtc datetime2(3)', @values2 = 1, @values3 = 2, @values11 = 2, @fromUtc = '2025-06-01T00:00:00';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Status] IN (@values2, @values3) AND [r].[Priority] = @values11 AND [r].[CreatedAt] >= @fromUtc
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@values2 tinyint,@values3 tinyint,@values11 tinyint,@fromUtc datetime2(3),@p int,@p0 int', @values2 = 1, @values3 = 2, @values11 = 2, @fromUtc = '2025-06-01T00:00:00', @p = 0, @p0 = 20;
GO

-- @scenario list-handler: One handler's requests, newest first
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[AssignedTo] = @handler', N'@handler nvarchar(100)', @handler = N'דוד פרידמן';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[AssignedTo] = @handler
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@handler nvarchar(100),@p int,@p0 int', @handler = N'דוד פרידמן', @p = 0, @p0 = 20;
GO

-- @scenario list-organization: Organization name starts with a prefix, newest first
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[OrganizationName] LIKE @organization_startswith ESCAPE N''\''', N'@organization_startswith nvarchar(200)', @organization_startswith = N'כרמל%';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[OrganizationName] LIKE @organization_startswith ESCAPE N''\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@organization_startswith nvarchar(200),@p int,@p0 int', @organization_startswith = N'כרמל%', @p = 0, @p0 = 20;
GO

-- @scenario list-search: Free-text search ("contains") in title or organization, newest first – through the SearchText column
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[SearchText] LIKE @pattern_contains ESCAPE N''\''', N'@pattern_contains nvarchar(4000)', @pattern_contains = N'%היתר%';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[SearchText] LIKE @pattern_contains ESCAPE N''\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@pattern_contains nvarchar(4000),@p int,@p0 int', @pattern_contains = N'%היתר%', @p = 0, @p0 = 20;
GO

-- @scenario list-search-rare: Free-text search for a rare term (an organization name, ~200 matches), newest first
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[SearchText] LIKE @pattern_contains ESCAPE N''\''', N'@pattern_contains nvarchar(4000)', @pattern_contains = N'%רימון לוגיסטיקה%';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[SearchText] LIKE @pattern_contains ESCAPE N''\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@pattern_contains nvarchar(4000),@p int,@p0 int', @pattern_contains = N'%רימון לוגיסטיקה%', @p = 0, @p0 = 20;
GO

-- @scenario list-sort-updated: No filter, sorted by last update (newest first), page 1 – a sort without a dedicated index
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
ORDER BY [r].[UpdatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY', N'@p int,@p1 int', @p = 0, @p1 = 20;
GO

-- @scenario list-sort-oldest: No filter, oldest first (the default sort reversed), page 1
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
ORDER BY [r].[CreatedAt], [r].[Id]
OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY', N'@p int,@p1 int', @p = 0, @p1 = 20;
GO

-- @scenario list-deep-page: Sorted by title, page 500 of 100 rows (OFFSET 49,900)
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
ORDER BY [r].[Title], [r].[Id]
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@p int,@p0 int', @p = 49900, @p0 = 100;
GO

-- @scenario summary-all: Aggregations of the unfiltered view (count by status, count by priority)
EXEC sp_executesql N'SELECT [r].[Status], COUNT(*)
FROM [Requests] AS [r]
GROUP BY [r].[Status]';
EXEC sp_executesql N'SELECT [r].[Priority], COUNT(*)
FROM [Requests] AS [r]
GROUP BY [r].[Priority]';
GO

-- @scenario summary-filtered: Aggregations for priority = High
EXEC sp_executesql N'SELECT [r].[Status], COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Priority] = @values1
GROUP BY [r].[Status]', N'@values1 tinyint', @values1 = 2;
EXEC sp_executesql N'SELECT [r].[Priority], COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Priority] = @values1
GROUP BY [r].[Priority]', N'@values1 tinyint', @values1 = 2;
GO

-- @scenario history: Status history of one request, newest first
EXEC sp_executesql N'SELECT [r].[Id], [r].[PreviousStatus], [r].[NewStatus], [r].[ChangedAt], [r].[ChangedBy]
FROM [RequestStatusHistory] AS [r]
WHERE [r].[RequestId] = @id
ORDER BY [r].[ChangedAt] DESC, [r].[Id] DESC', N'@id int', @id = 50000;
GO

-- @scenario lookups: Distinct handlers and organizations for the filter drop-downs
EXEC sp_executesql N'SELECT [r0].[AssignedTo]
FROM (
    SELECT DISTINCT [r].[AssignedTo]
    FROM [Requests] AS [r]
    WHERE [r].[AssignedTo] IS NOT NULL
) AS [r0]
ORDER BY [r0].[AssignedTo]';
EXEC sp_executesql N'SELECT [r0].[OrganizationName]
FROM (
    SELECT DISTINCT [r].[OrganizationName]
    FROM [Requests] AS [r]
) AS [r0]
ORDER BY [r0].[OrganizationName]';
GO

SET STATISTICS IO, TIME OFF;
GO
