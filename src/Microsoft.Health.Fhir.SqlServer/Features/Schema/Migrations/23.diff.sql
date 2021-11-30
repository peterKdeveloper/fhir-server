﻿/*************************************************************
    This migration removes the existing primary key clustered index and adds a clustered index on Id column in the ResourceChangeData table.
    The migration is "online" meaning the server is fully available during the upgrade, but it can be very time-consuming.
    For reference, a resource change data table with 10 million records took around 25 minutes to complete 
    on the Azure SQL database (SQL elastic pools - GeneralPurpose: Gen5, 2 vCores).
**************************************************************/

EXEC dbo.LogSchemaMigrationProgress 'Beginning migration to version 23.';
GO

/*************************************************************
 Insert singleValue values into the low and high values for
 dbo.QuantitySearchParam table
**************************************************************/
EXEC dbo.LogSchemaMigrationProgress 'Populating LowValue and HighValue in QuantitySearchParam if null'
UPDATE dbo.QuantitySearchParam
SET LowValue = SingleValue, 
    HighValue = SingleValue
WHERE LowValue IS NULL
    AND HighValue IS NULL
    AND SingleValue IS NOT NULL;
GO

--
-- STORED PROCEDURE
--     FetchResourceChanges_3
--
-- DESCRIPTION
--     Returns the number of resource change records from startId. The start id is inclusive.
--
-- PARAMETERS
--     @startId
--         * The start id of resource change records to fetch.
--     @lastProcessedDateTime
--         * The last checkpoint datetime.
--     @pageSize
--         * The page size for fetching resource change records.
--
-- RETURN VALUE
--     Resource change data rows.
--
CREATE OR ALTER PROCEDURE dbo.FetchResourceChanges_3
    @startId bigint,
    @lastProcessedDateTime datetime2(7),
    @pageSize smallint
AS
BEGIN
    -- Drop indexes that uses LowValue and HighValue columns
	IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue')
	BEGIN
	    EXEC dbo.LogSchemaMigrationProgress 'Dropping IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue'
		DROP INDEX IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue
		ON dbo.QuantitySearchParam
	END;

	IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue')
	BEGIN
	    EXEC dbo.LogSchemaMigrationProgress 'Dropping IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue'
		DROP INDEX IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue
		ON dbo.QuantitySearchParam
	END;

    DECLARE @partitions TABLE (partitionBoundary datetime2(7));
    DECLARE @precedingPartitionBoundary datetime2(7) = (SELECT TOP(1) CAST(prv.value as datetime2(7)) AS value FROM sys.partition_range_values AS prv
                                                           INNER JOIN sys.partition_functions AS pf ON pf.function_id = prv.function_id
                                                       WHERE pf.name = N'PartitionFunction_ResourceChangeData_Timestamp'
                                                           AND CAST(prv.value AS datetime2(7)) < DATEADD(HOUR, DATEDIFF(HOUR, 0, @lastProcessedDateTime), 0)
                                                       ORDER BY prv.boundary_id DESC);

    IF (@precedingPartitionBoundary IS NULL) BEGIN
        SET @precedingPartitionBoundary = CONVERT(datetime2(7), N'1970-01-01T00:00:00.0000000');
    END;

    INSERT INTO @partitions 
        SELECT CAST(prv.value AS datetime2(7)) FROM sys.partition_range_values AS prv
            INNER JOIN sys.partition_functions AS pf ON pf.function_id = prv.function_id
        WHERE pf.name = N'PartitionFunction_ResourceChangeData_Timestamp'
              AND CAST(prv.value AS datetime2(7)) >= DATEADD(HOUR, DATEDIFF(HOUR, 0, @precedingPartitionBoundary), 0)
              AND CAST(prv.value AS datetime2(7)) < DATEADD(HOUR, 1, SYSUTCDATETIME());

-- Recreate dropped indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue')
BEGIN
	EXEC dbo.LogSchemaMigrationProgress 'Creating IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue'

    CREATE NONCLUSTERED INDEX IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue
	ON dbo.QuantitySearchParam
	(
      ResourceTypeId,
      ResourceVersion,
      ResourceChangeTypeId
    FROM @partitions AS p CROSS APPLY (
        SELECT TOP(@pageSize) Id,
          Timestamp,
          ResourceId,
          ResourceTypeId,
          ResourceVersion,
          ResourceChangeTypeId
        FROM ResourceChangeData WITH (REPEATABLEREAD)
            WHERE Id >= @startId AND $PARTITION.PartitionFunction_ResourceChangeData_Timestamp(Timestamp) = $PARTITION.PartitionFunction_ResourceChangeData_Timestamp(p.partitionBoundary)
        ORDER BY Id ASC
        ) AS cd
    ORDER BY cd.Id ASC;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue')
BEGIN
    EXEC dbo.LogSchemaMigrationProgress 'Creating IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue'
	
    CREATE NONCLUSTERED INDEX IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue
    ON dbo.QuantitySearchParam
    (
	    ResourceTypeId,
	    SearchParamId,
	    QuantityCodeId,
	    HighValue,
	    LowValue,
	    ResourceSurrogateId
    )
    INCLUDE
    (
	    SystemId
    )
    WHERE IsHistory = 0
    WITH (ONLINE=ON) 
    ON PartitionScheme_ResourceTypeId(ResourceTypeId);
END;
GO

IF NOT EXISTS (
    SELECT * 
	FROM sys.key_constraints 
	WHERE name='PK_QuantitySearchParam' AND type='PK')
BEGIN
    EXEC dbo.LogSchemaMigrationProgress 'Adding PK_QuantitySearchParam'
	ALTER TABLE dbo.QuantitySearchParam 
	ADD CONSTRAINT PK_QuantitySearchParam PRIMARY KEY NONCLUSTERED(ResourceTypeId, SearchParamId, HighValue, LowValue, ResourceSurrogateId)
	WITH (DATA_COMPRESSION = PAGE, ONLINE=ON) 
	ON PartitionScheme_ResourceTypeId(ResourceTypeId)
END;
GO

/*************************************************************
 Insert singleValue values into the low and high values for
 dbo.NumberSearchParam table
**************************************************************/
EXEC dbo.LogSchemaMigrationProgress 'Populating LowValue and HighValue in NumberSearchParam if null'
UPDATE dbo.NumberSearchParam
SET LowValue = SingleValue, 
    HighValue = SingleValue
WHERE LowValue IS NULL
    AND HighValue IS NULL
    AND SingleValue IS NOT NULL;
GO

/*************************************************************
Table - dbo.NumberSearchParam
**************************************************************/
-- Update LowValue and HighValue column as NOT NULL
IF (((SELECT COLUMNPROPERTY(OBJECT_ID('dbo.NumberSearchParam', 'U'), 'LowValue', 'AllowsNull')) = 1)
    OR (SELECT COLUMNPROPERTY(OBJECT_ID('dbo.NumberSearchParam', 'U'), 'HighValue', 'AllowsNull')) = 1)
BEGIN
 -- Drop indexes that uses lowValue and HighValue column
	IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NumberSearchParam_SearchParamId_LowValue_HighValue')
	BEGIN
	    EXEC dbo.LogSchemaMigrationProgress 'Dropping IX_NumberSearchParam_SearchParamId_LowValue_HighValue'
		DROP INDEX IX_NumberSearchParam_SearchParamId_LowValue_HighValue
		ON dbo.NumberSearchParam
	END;

	IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NumberSearchParam_SearchParamId_HighValue_LowValue')
BEGIN
	    EXEC dbo.LogSchemaMigrationProgress 'Dropping IX_NumberSearchParam_SearchParamId_HighValue_LowValue'
		DROP INDEX IX_NumberSearchParam_SearchParamId_HighValue_LowValue
		ON dbo.NumberSearchParam
END;

	-- Update datatype and non-nullable LowValue and HighValue columns
	EXEC dbo.LogSchemaMigrationProgress 'Updating NumberSearchParam LowValue as NOT NULL'
	ALTER TABLE dbo.NumberSearchParam
	ALTER COLUMN LowValue decimal(18,6) NOT NULL;

	EXEC dbo.LogSchemaMigrationProgress 'Updating NumberSearchParam HighValue as NOT NULL'
	ALTER TABLE dbo.NumberSearchParam
	ALTER COLUMN HighValue decimal(18,6) NOT NULL;
END;
GO

-- Recreate dropped indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NumberSearchParam_SearchParamId_HighValue_LowValue')
BEGIN
    EXEC dbo.LogSchemaMigrationProgress 'Creating IX_NumberSearchParam_SearchParamId_HighValue_LowValue'
    CREATE NONCLUSTERED INDEX IX_NumberSearchParam_SearchParamId_HighValue_LowValue
    ON dbo.NumberSearchParam
    (
	    ResourceTypeId,
	    SearchParamId,
	    HighValue,
	    LowValue,
	    ResourceSurrogateId
    )
    WHERE IsHistory = 0
    WITH (ONLINE=ON)
    ON PartitionScheme_ResourceTypeId(ResourceTypeId)
END;
GO

EXEC dbo.LogSchemaMigrationProgress 'Creating IXC_ResourceChangeDataStaging index on ResourceChangeDataStaging table.';
GO

IF NOT EXISTS (
    SELECT * 
	FROM sys.key_constraints 
	WHERE name='PK_NumberSearchParam' AND type='PK')
BEGIN
    EXEC dbo.LogSchemaMigrationProgress 'Adding PK_NumberSearchParam'
	ALTER TABLE dbo.NumberSearchParam 
	ADD CONSTRAINT PK_NumberSearchParam PRIMARY KEY NONCLUSTERED(ResourceTypeId, SearchParamId, LowValue, HighValue, ResourceSurrogateId)
    WITH (DATA_COMPRESSION = PAGE, ONLINE=ON)
    ON PartitionScheme_ResourceTypeId(ResourceTypeId)
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NumberSearchParam_SearchParamId_LowValue_HighValue')
BEGIN
    EXEC dbo.LogSchemaMigrationProgress 'Creating IX_NumberSearchParam_SearchParamId_LowValue_HighValue'
    CREATE NONCLUSTERED INDEX IX_NumberSearchParam_SearchParamId_LowValue_HighValue
    ON dbo.NumberSearchParam
    (
        ResourceTypeId,
        SearchParamId,
        LowValue,
        HighValue,
        ResourceSurrogateId
    )
    WHERE IsHistory = 0
    WITH (ONLINE=ON)
    ON PartitionScheme_ResourceTypeId(ResourceTypeId)
END;
GO
