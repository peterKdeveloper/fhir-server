// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirPathPatch.Operations
{
    /// <summary>
    /// An enumeration of the supported types of FHIR Path Patch operations.
    /// </summary>
    public enum EOperationType
    {
        /// <summary>
        /// FHIR Patch Add Operation.
        /// </summary>
        ADD,

        /// <summary>
        /// FHIR Patch Insert Operation.
        /// </summary>
        INSERT,

        /// <summary>
        /// FHIR Patch Delete Operation.
        /// </summary>
        DELETE,

        /// <summary>
        /// FHIR Patch Replace Operation.
        /// </summary>
        REPLACE,

        /// <summary>
        /// FHIR Patch Move Operation.
        /// </summary>
        MOVE,
    }
}
