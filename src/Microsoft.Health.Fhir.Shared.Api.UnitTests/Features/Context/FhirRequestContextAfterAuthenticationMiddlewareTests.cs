﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Health.Fhir.Api.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Context;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Context
{
    public class FhirRequestContextAfterAuthenticationMiddlewareTests
    {
        private readonly FhirRequestContextAfterAuthenticationMiddleware _fhirRequestContextAfterAuthenticationMiddleware;

        public FhirRequestContextAfterAuthenticationMiddlewareTests()
        {
            _fhirRequestContextAfterAuthenticationMiddleware = new FhirRequestContextAfterAuthenticationMiddleware(
                httpContext => Task.CompletedTask);
        }

        [Fact]
        public async Task GivenUserNotNull_WhenInvoked_ThenPrincipalShouldBeSet()
        {
            IFhirRequestContextAccessor fhirRequestContextAccessor = Substitute.For<IFhirRequestContextAccessor>();

            var fhirRequestContext = new DefaultFhirRequestContext();

            fhirRequestContextAccessor.FhirRequestContext.Returns(fhirRequestContext);

            HttpContext httpContext = new DefaultHttpContext();

            var expectedPrincipal = new System.Security.Claims.ClaimsPrincipal();

            httpContext.User = expectedPrincipal;

            await _fhirRequestContextAfterAuthenticationMiddleware.Invoke(httpContext, fhirRequestContextAccessor);

            Assert.Same(expectedPrincipal, fhirRequestContext.Principal);
        }
    }
}
