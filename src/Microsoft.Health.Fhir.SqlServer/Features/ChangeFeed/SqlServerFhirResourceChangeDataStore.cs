﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Abstractions.Data;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.SqlServer;

namespace Microsoft.Health.Fhir.SqlServer.Features.ChangeFeed
{
    /// <summary>
    /// A data store that provides functionalities to get resource changes and resource types for change feed sources.
    /// </summary>
    public class SqlServerFhirResourceChangeDataStore : IChangeFeedSource<IResourceChangeData>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ILogger<SqlServerFhirResourceChangeDataStore> _logger;

        // dbnetlib error value for timeout expired
        private const short TIMEOUTEXPIRED = -2;

        /// <summary>
        /// Creates a new instance of the <see cref="SqlServerFhirResourceChangeDataStore"/> class.
        /// </summary>
        /// <param name="sqlConnectionFactory">The SQL Connection factory.</param>
        /// <param name="logger">The logger.</param>
        public SqlServerFhirResourceChangeDataStore(ISqlConnectionFactory sqlConnectionFactory, ILogger<SqlServerFhirResourceChangeDataStore> logger)
        {
            EnsureArg.IsNotNull(sqlConnectionFactory, nameof(sqlConnectionFactory));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _sqlConnectionFactory = sqlConnectionFactory;
            _logger = logger;
        }

        /// <summary>
        ///  Returns the number of resource change records from startId.
        /// </summary>
        /// <param name="startId">The start id of resource change records to fetch. The start id is inclusive.</param>
        /// <param name="pageSize">The page size for fetching resource change records.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Resource change data rows.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if startId or pageSize is less than zero.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when a method call is invalid for the object's current state.</exception>
        /// <exception cref="Microsoft.Data.SqlClient.SqlException">Thrown when SQL Server returns a warning or error.</exception>
        /// <exception cref="System.TimeoutException">Thrown when the time allotted for a process or operation has expired.</exception>
        /// <exception cref="System.Exception">Thrown when errors occur during execution.</exception>
        public async Task<IReadOnlyCollection<IResourceChangeData>> GetRecordsAsync(long startId, int pageSize, CancellationToken cancellationToken)
        {
            EnsureArg.IsGte(startId, 0, nameof(startId));
            EnsureArg.IsGte(pageSize, 1, nameof(pageSize));

            var listResourceChangeData = new List<ResourceChangeData>();
            try
            {
                using (SqlConnection sqlConnection = await _sqlConnectionFactory.GetSqlConnectionAsync(cancellationToken: cancellationToken))
                {
                    await sqlConnection.OpenAsync(cancellationToken);
                    using (SqlCommand sqlCommand = new SqlCommand("dbo.FetchResourceChanges", sqlConnection))
                    {
                        sqlCommand.CommandType = CommandType.StoredProcedure;
                        sqlCommand.Parameters.AddWithValue("@startId", SqlDbType.BigInt).Value = startId;
                        sqlCommand.Parameters.AddWithValue("@pageSize", SqlDbType.SmallInt).Value = pageSize;
                        using (SqlDataReader sqlDataReader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
                        {
                            while (await sqlDataReader.ReadAsync(cancellationToken))
                            {
                                listResourceChangeData.Add(new ResourceChangeData(
                                    id: (long)sqlDataReader["Id"],
                                    timestamp: DateTime.SpecifyKind((DateTime)sqlDataReader["Timestamp"], DateTimeKind.Utc),
                                    resourceId: (string)sqlDataReader["ResourceId"],
                                    resourceTypeId: (short)sqlDataReader["ResourceTypeId"],
                                    resourceVersion: (int)sqlDataReader["ResourceVersion"],
                                    resourceChangeTypeId: (byte)sqlDataReader["ResourceChangeTypeId"]));
                            }
                        }

                        return listResourceChangeData;
                    }
                }
            }
            catch (SqlException ex)
            {
                switch (ex.Number)
                {
                    case TIMEOUTEXPIRED:
                        throw new TimeoutException(ex.Message, ex);
                    default:
                        _logger.LogError(ex, string.Format(Resources.SqlExceptionOccurredWhenFetchingResourceChanges, ex.Number));
                        throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Resources.ExceptionOccurredWhenFetchingResourceChanges);
                throw;
            }
        }

        /// <summary>
        /// Get resource types as a dictionary.
        /// </summary>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Returns a resource type map.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when a method call is invalid for the object's current state.</exception>
        /// <exception cref="Microsoft.Data.SqlClient.SqlException">Thrown when SQL Server returns a warning or error.</exception>
        /// <exception cref="System.TimeoutException">Thrown when the time allotted for a process or operation has expired.</exception>
        /// <exception cref="System.Exception">Thrown when errors occur during execution.</exception>
        public async Task<IDictionary<short, string>> GetResourceTypeMapAsync(CancellationToken cancellationToken)
        {
            var resourceTypeIdToNameMapping = new Dictionary<short, string>();
            try
            {
                using (SqlConnection sqlConnection = await _sqlConnectionFactory.GetSqlConnectionAsync(cancellationToken: cancellationToken))
                {
                    await sqlConnection.OpenAsync(cancellationToken);
                    using (SqlCommand sqlCommand = new SqlCommand("SELECT ResourceTypeId, Name FROM dbo.ResourceType", sqlConnection))
                    {
                        sqlCommand.CommandType = CommandType.Text;
                        using (SqlDataReader sqlDataReader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
                        {
                            while (await sqlDataReader.ReadAsync(cancellationToken))
                            {
                                resourceTypeIdToNameMapping.Add((short)sqlDataReader["ResourceTypeId"], (string)sqlDataReader["Name"]);
                            }
                        }

                        return resourceTypeIdToNameMapping;
                    }
                }
            }
            catch (SqlException ex)
            {
                switch (ex.Number)
                {
                    case TIMEOUTEXPIRED:
                        throw new TimeoutException(ex.Message, ex);
                    default:
                        _logger.LogError(ex, string.Format(Resources.SqlExceptionOccurredWhenGettingResourceTypes, ex.Number));
                        throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Resources.ExceptionOccurredWhenGettingResourceTypes);
                throw;
            }
        }
    }
}
