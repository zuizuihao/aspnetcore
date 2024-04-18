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
        var authorizeLocation = GetAttributeLocation(context, wellKnownTypes, actionSymbol, WellKnownType.Microsoft_AspNetCore_Authorization_IAuthorizeData);
        if (authorizeLocation is null)
        {
            return;
        }

        var allowAnonymousLocation = GetAttributeLocation(context, wellKnownTypes, controllerSymbol, WellKnownType.Microsoft_AspNetCore_Authorization_IAllowAnonymous);
        if (allowAnonymousLocation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AuthorizeAttributeOverridden,
            authorizeLocation,
            controllerSymbol.Name));
    }

    private static Location? GetAttributeLocation(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes, ISymbol symbol, WellKnownType attributeType)
    {
        var attributeData = symbol.GetAttributes(wellKnownTypes.Get(attributeType)).LastOrDefault();
        return attributeData?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
    }
}
