// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonSchemaMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Supports managing elements that belong in the "components" section of
/// an OpenAPI document. In particular, this is the API that is used to
/// interact with the JSON schemas that are managed by a given OpenAPI document.
/// </summary>
internal sealed class OpenApiComponentService(IOptions<JsonOptions> jsonOptions)
{
    private readonly ConcurrentDictionary<Type, JsonObject> _schemas = new()
    {
        // Pre-populate OpenAPI schemas for well-defined types in ASP.NET Core.
        [typeof(IFormFile)] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
        [typeof(IFormFileCollection)] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string", ["format"] = "binary" }
        },
        [typeof(Stream)] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
        [typeof(PipeReader)] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
    };

    private readonly JsonSerializerOptions _jsonSerializerOptions = jsonOptions.Value.SerializerOptions;
    private readonly JsonSchemaMapperConfiguration _configuration = new()
    {
        OnSchemaGenerated = (context, schema) =>
        {
            schema.ApplyPrimitiveTypesAndFormats(context.TypeInfo.Type);
            if (context.GetCustomAttributes(typeof(ValidationAttribute)) is { } validationAttributes)
            {
                schema.ApplyValidationAttributes(validationAttributes);
            }
        }
    };

    internal OpenApiSchema GetOrCreateSchema(Type type, ApiParameterDescription? parameterDescription = null)
    {
        var schemaAsJsonObject = _schemas.GetOrAdd(type, CreateSchema);
        if (parameterDescription is not null)
        {
            schemaAsJsonObject.ApplyParameterInfo(parameterDescription);
        }
        return JsonSerializer.Deserialize<OpenApiJsonSchema>(schemaAsJsonObject)?.Schema ?? new OpenApiSchema();
    }

    private JsonObject CreateSchema(Type type)
        => JsonSchemaMapper.JsonSchemaMapper.GetJsonSchema(_jsonSerializerOptions, type, _configuration);
}
