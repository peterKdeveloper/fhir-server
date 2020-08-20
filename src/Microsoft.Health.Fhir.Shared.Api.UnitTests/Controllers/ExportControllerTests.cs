﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Api.Controllers;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Routing;
using NSubstitute;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Api.UnitTests.Controllers
{
    public class ExportControllerTests
    {
        private ExportController _exportEnabledController;
        private IMediator _mediator = Substitute.For<IMediator>();
        private IFhirRequestContextAccessor _fhirRequestContextAccessor = Substitute.For<IFhirRequestContextAccessor>();
        private IUrlResolver _urlResolver = Substitute.For<IUrlResolver>();

        public ExportControllerTests()
        {
            _exportEnabledController = GetController(new ExportJobConfiguration() { Enabled = true });
        }

        [Fact]
        public async Task GivenAnExportRequest_WhenDisabled_ThenRequestNotValidExceptionShouldBeThrown()
        {
            var exportController = GetController(new ExportJobConfiguration() { Enabled = false });

            await Assert.ThrowsAsync<RequestNotValidException>(() => exportController.Export(outputFormat: null, since: null, resourceType: null, containerName: null));
        }

        [Fact]
        public async Task GivenAnExportByResourceTypeRequest_WhenDisabled_ThenRequestNotValidExceptionShouldBeThrown()
        {
            var exportController = GetController(new ExportJobConfiguration() { Enabled = false });

            await Assert.ThrowsAsync<RequestNotValidException>(() => exportController.ExportResourceType(
                outputFormat: null,
                since: null,
                resourceType: null,
                containerName: null,
                typeParameter: ResourceType.Patient.ToString()));
        }

        [Fact]
        public async Task GivenAnExportByIdRequest_WhenDisabled_ThenRequestNotValidExceptionShouldBeThrown()
        {
            var exportController = GetController(new ExportJobConfiguration() { Enabled = false });

            await Assert.ThrowsAsync<RequestNotValidException>(() => exportController.ExportResourceTypeById(
                outputFormat: null,
                since: null,
                resourceType: null,
                containerName: null,
                typeParameter: ResourceType.Group.ToString(),
                idParameter: "id"));
        }

        [Fact]
        public async Task GivenAnExportRequest_WhenOutputFormatIsNotNdjson_ThenRequestNotValidExceptionShouldBeThrown()
        {
            await Assert.ThrowsAsync<RequestNotValidException>(() => _exportEnabledController.Export("invalid", null, null, null));
        }

        [Fact]
        public async Task GivenAnExportByResourceTypeRequest_WhenOutputFormatIsNotNdjson_ThenRequestNotValidExceptionShouldBeThrown()
        {
            await Assert.ThrowsAsync<RequestNotValidException>(() => _exportEnabledController.ExportResourceType("invalid", null, null, null, ResourceType.Patient.ToString()));
        }

        [Fact]
        public async Task GivenAnExportByIdRequest_WhenOutputFormatIsNotNdjson_ThenRequestNotValidExceptionShouldBeThrown()
        {
            await Assert.ThrowsAsync<RequestNotValidException>(() => _exportEnabledController.ExportResourceTypeById("invalid", null, null, null, ResourceType.Group.ToString(), "id"));
        }

        [Fact]
        public async Task GivenAnExportByResourceTypeRequest_WhenResourceTypeIsNotPatient_ThenRequestNotValidExceptionShouldBeThrown()
        {
            await Assert.ThrowsAsync<RequestNotValidException>(() => _exportEnabledController.ExportResourceType(null, null, null, null, ResourceType.Observation.ToString()));
        }

        [Fact]
        public async Task GivenAnExportResourceTypeIdRequest_WhenResourceTypeIsNotGroup_ThenRequestNotValidExceptionShouldBeThrown()
        {
            await Assert.ThrowsAsync<RequestNotValidException>(() => _exportEnabledController.ExportResourceTypeById(null, null, null, null, ResourceType.Patient.ToString(), "id"));
        }

        private ExportController GetController(ExportJobConfiguration exportConfig)
        {
            var operationConfig = new OperationsConfiguration()
            {
                Export = exportConfig,
            };

            IOptions<OperationsConfiguration> optionsOperationConfiguration = Substitute.For<IOptions<OperationsConfiguration>>();
            optionsOperationConfiguration.Value.Returns(operationConfig);

            return new ExportController(
                _mediator,
                _fhirRequestContextAccessor,
                _urlResolver,
                optionsOperationConfiguration,
                NullLogger<ExportController>.Instance);
        }
    }
}
