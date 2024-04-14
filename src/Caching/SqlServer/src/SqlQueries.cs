// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.Extensions.Caching.SqlServer;

internal sealed class SqlQueries
{
    private const string TableInfoFormat = """
        SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = '{0}'
        AND TABLE_NAME = '{1}'
        """;

    // optimize for no-sliding/sliding-doesn't-impact-expiry:
    // avoid taking any locks if we don't need an update; to avoid geting competing "read/read/write/write" deadlocks,
    // we use NOLOCK on the initial select, combined with optimistic concurrency on the update; we don't need any
    // atomic row operations, so NOLOCK is not a problem here
    private const string GetValueWithSlideFormat = """
        declare @value varbinary(max), @slideSeconds bigint, @oldExpire datetimeoffset
        select @value = Value, @slideSeconds = SlidingExpirationInSeconds, @oldExpire = ExpiresAtTime
        from {0} (nolock)
        where Id = @id and @UtcNow <= ExpiresAtTime;

        if @slideSeconds > 0
        begin
        	declare @newExpire datetimeoffset = DATEADD(SECOND, @slideSeconds, @UtcNow);
        	if @newExpire > @oldExpire
        	begin
        		update {0}
        		set ExpiresAtTime = @newExpire
        		where Id = @id and ExpiresAtTime = @oldExpire
        	end
        end
        select @value
        """;

    // like GetValueWithSlideFormat, but doesn't touch the data field
    private const string RefreshFormat = """
        declare @slideSeconds bigint, @oldExpire datetimeoffset
        select @slideSeconds = SlidingExpirationInSeconds, @oldExpire = ExpiresAtTime
        from {0} (nolock)
        where Id = @id and @UtcNow <= ExpiresAtTime;

        if @slideSeconds > 0
        begin
        	declare @newExpire datetimeoffset = DATEADD(SECOND, @slideSeconds, @UtcNow);
        	if @newExpire > @oldExpire
        	begin
        		update {0}
        		set ExpiresAtTime = @newExpire
        		where Id = @id and ExpiresAtTime = @oldExpire
        	end
        end
        """;

    private const string SetCacheItemFormat =
        "DECLARE @ExpiresAtTime DATETIMEOFFSET; " +
        "SET @ExpiresAtTime = " +
        "(CASE " +
                "WHEN (@SlidingExpirationInSeconds IS NUll) " +
                "THEN @AbsoluteExpiration " +
                "ELSE " +
                "DATEADD(SECOND, Convert(bigint, @SlidingExpirationInSeconds), @UtcNow) " +
        "END);" +
        "UPDATE {0} SET Value = @Value, ExpiresAtTime = @ExpiresAtTime," +
        "SlidingExpirationInSeconds = @SlidingExpirationInSeconds, AbsoluteExpiration = @AbsoluteExpiration " +
        "WHERE Id = @Id " +
        "IF (@@ROWCOUNT = 0) " +
        "BEGIN " +
            "INSERT INTO {0} " +
            "(Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration) " +
            "VALUES (@Id, @Value, @ExpiresAtTime, @SlidingExpirationInSeconds, @AbsoluteExpiration); " +
        "END ";

    private const string DeleteCacheItemFormat = "DELETE FROM {0} WHERE Id = @Id";

    public const string DeleteExpiredCacheItemsFormat = "DELETE FROM {0} WHERE @UtcNow > ExpiresAtTime";

    public SqlQueries(string schemaName, string tableName)
    {
        var tableNameWithSchema = string.Format(
            CultureInfo.InvariantCulture,
            "{0}.{1}", DelimitIdentifier(schemaName), DelimitIdentifier(tableName));

        GetCacheItem = string.Format(CultureInfo.InvariantCulture, GetValueWithSlideFormat, tableNameWithSchema);
        GetCacheItemWithoutValue = string.Format(CultureInfo.InvariantCulture, RefreshFormat, tableNameWithSchema);
        DeleteCacheItem = string.Format(CultureInfo.InvariantCulture, DeleteCacheItemFormat, tableNameWithSchema);
        DeleteExpiredCacheItems = string.Format(CultureInfo.InvariantCulture, DeleteExpiredCacheItemsFormat, tableNameWithSchema);
        SetCacheItem = string.Format(CultureInfo.InvariantCulture, SetCacheItemFormat, tableNameWithSchema);
        TableInfo = string.Format(CultureInfo.InvariantCulture, TableInfoFormat, EscapeLiteral(schemaName), EscapeLiteral(tableName));
    }

    public string TableInfo { get; }

    public string GetCacheItem { get; }

    public string GetCacheItemWithoutValue { get; }

    public string SetCacheItem { get; }

    public string DeleteCacheItem { get; }

    public string DeleteExpiredCacheItems { get; }

    // From EF's SqlServerQuerySqlGenerator
    private static string DelimitIdentifier(string identifier)
    {
        return "[" + identifier.Replace("]", "]]") + "]";
    }

    private static string EscapeLiteral(string literal)
    {
        return literal.Replace("'", "''");
    }
}
