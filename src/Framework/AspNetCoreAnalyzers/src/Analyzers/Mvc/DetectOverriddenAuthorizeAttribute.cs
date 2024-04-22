// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers.Mvc;

using WellKnownType = WellKnownTypeData.WellKnownType;

public partial class MvcAnalyzer
{
    private static void DetectOverriddenAuthorizeAttribute(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes, INamedTypeSymbol controllerSymbol, IMethodSymbol actionSymbol)
    {
        var authAttributeData = actionSymbol.GetAttributes(wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAuthorizeData)).FirstOrDefault();

        var authAttributeLocation = authAttributeData?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
        if (authAttributeLocation is null)
        {
            return;
        }

        var anonymousControllerType = GetNearestTypeWithInheritedAttribute(controllerSymbol, wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAllowAnonymous));
        if (anonymousControllerType is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AuthorizeAttributeOverridden,
            authAttributeLocation,
            anonymousControllerType.Name));
    }

    private static ITypeSymbol? GetNearestTypeWithInheritedAttribute(ITypeSymbol? typeSymbol, ITypeSymbol attribute)
    {
        while (typeSymbol is not null && !typeSymbol.GetAttributes(attribute).Any())
        {
            typeSymbol = typeSymbol.BaseType;
        }

        // TODO: https://stackoverflow.com/questions/55523130/roslyn-is-isymbol-getattributes-returns-inherited-attributes

        return typeSymbol;
    }
}
