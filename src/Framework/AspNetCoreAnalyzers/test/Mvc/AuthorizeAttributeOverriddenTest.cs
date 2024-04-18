// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Microsoft.AspNetCore.Analyzers.Verifiers.CSharpAnalyzerVerifier<Microsoft.AspNetCore.Analyzers.Mvc.MvcAnalyzer>;

namespace Microsoft.AspNetCore.Analyzers.Mvc;

public partial class AuthorizeAttributeOverriddenTest
{
    [Fact]
    public async Task AuthorizeOnAction_AllowAnonymousOnController_HasDiagnostics()
    {
        // Arrange
        var source = @"
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

WebApplication.Create().Run();

[AllowAnonymous]
public class WeatherForecastController
{
    [{|#0:Authorize|}]
    public object Get() => new object();
}
";

        var expectedDiagnostics = new[] {
            new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeOverridden).WithArguments("WeatherForecastController").WithLocation(0),
        };

        // Act & Assert
        await VerifyCS.VerifyAnalyzerAsync(source, expectedDiagnostics);
    }
}

