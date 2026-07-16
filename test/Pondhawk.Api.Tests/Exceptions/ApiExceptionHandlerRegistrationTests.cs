// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Pondhawk.Api;
using Pondhawk.Api.Exceptions;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Exceptions;

public class ApiExceptionHandlerRegistrationTests
{
    [Fact]
    public void Handler_Validates_UnderScopeValidation_WhenRegisteredNormally()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPondhawkApi();                            // scoped IRequestContext + IHttpContextAccessor
        services.AddExceptionHandler<ApiExceptionHandler>();  // Microsoft: registers the handler as a singleton

        // A real host builds with scope validation on. A singleton that constructor-injects a scoped
        // dependency (a captive dependency) fails right here — this is the guard for that class of bug,
        // which per-instance unit construction can never surface.
        Should.NotThrow(() => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        }));
    }
}
