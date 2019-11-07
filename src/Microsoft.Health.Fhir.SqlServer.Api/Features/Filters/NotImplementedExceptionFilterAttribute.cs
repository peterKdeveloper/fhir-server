﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;

namespace Microsoft.Health.Fhir.SqlServer.Api.Features.Filters
{
    public class NotImplementedExceptionFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (!context.ExceptionHandled)
            {
                var resultJson = new JObject();
                if (context.Exception != null)
                {
                    resultJson = new JObject { ["error"] = context.Exception.Message };
                }

                switch (context.Exception)
                {
                    case NotImplementedException _:
                        context.Result = new JsonResult(resultJson) { StatusCode = (int)HttpStatusCode.NotImplemented };
                        context.ExceptionHandled = true;
                        break;

                    case FileNotFoundException _:
                        context.Result = new JsonResult(resultJson) { StatusCode = (int)HttpStatusCode.NotFound };
                        context.ExceptionHandled = true;
                        break;
                }
            }
        }
    }
}
