// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Analyzers.Infrastructure;
using Microsoft.AspNetCore.Analyzers.Infrastructure.RoutePattern;
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

    private static ITypeSymbol? GetNearestTypeWithInheritedAttribute(ITypeSymbol typeSymbol, ITypeSymbol attribute)
    {
        foreach (var type in GetTypeHierarchy(typeSymbol))
        {
            if (type.GetAttributes(attribute).Any())
            {
                return type;
            }
        }

        return null;
    }

    private static IEnumerable<ITypeSymbol> GetTypeHierarchy(ITypeSymbol? typeSymbol)
    {
        while (typeSymbol != null)
        {
            yield return typeSymbol;

            typeSymbol = typeSymbol.BaseType;
        }
    }
}
