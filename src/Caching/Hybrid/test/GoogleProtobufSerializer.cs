// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Hybrid.Tests;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides HybridCache configuration extension methods for Google.Protobuf types
    /// </summary>
    public static class GoogleProtobufHybridCacheExtensions
    {
        public static IHybridCacheBuilder AddGoogleProtobuf<T>(this IHybridCacheBuilder builder)
            where T : IMessage<T> => builder.WithSerializer<T, GoogleProtobufSerializer<T>>();

        public static IHybridCacheBuilder AddGoogleProtobuf(this IHybridCacheBuilder builder)
            => builder.WithSerializerFactory<GoogleProtobufSerializerFactory>();
    }
}

namespace Microsoft.Extensions.Caching.Hybrid.Tests
{
    /// <summary>
    /// HybridCache serialization implementation for a single Google.Protobuf message type
    /// </summary>
    /// <typeparam name="T">The type of message to be handled</typeparam>
    public class GoogleProtobufSerializer<T> : IHybridCacheSerializer<T> where T : IMessage<T>
    {
        // serialization: via IMessage<T> instance methods
        // deserialization: via the parser API on the static .Parser property
        private static readonly MessageParser<T> _parser = typeof(T)
            .GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                as MessageParser<T> ?? throw new InvalidOperationException(
                    "Message parser not found; type may not be Google.Protobuf");

        T IHybridCacheSerializer<T>.Deserialize(ReadOnlySequence<byte> source)
            => _parser.ParseFrom(source);

        void IHybridCacheSerializer<T>.Serialize(T value, IBufferWriter<byte> target)
            => value.WriteTo(target);
    }

    /// <summary>
    /// HybridCache serialization factory implementation for Google.Protobuf message types
    /// </summary>
    public class GoogleProtobufSerializerFactory : IHybridCacheSerializerFactory
    {
#pragma warning disable CS0436 // ambiguous [NotNullWhen(...)] because of proj refs
        public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
#pragma warning restore CS0436 // ambiguous [NotNullWhen(...)] because of proj refs
        {
            // all Google.Protobuf types implement IMessage<T> : IMessage; check for IMessage first,
            // since we can do that without needing to use MakeGenericType for that; note that
            // IMessage<T> and GoogleProtobufSerializer<T> both have the T : IMessage<T> constraint,
            // which means we're going to need to use reflection here
            try
            {
                if (typeof(IMessage).IsAssignableFrom(typeof(T))
                    && typeof(IMessage<>).MakeGenericType(typeof(T)).IsAssignableFrom(typeof(T)))
                {
                    serializer = (IHybridCacheSerializer<T>)Activator.CreateInstance(
                        typeof(GoogleProtobufSerializer<>).MakeGenericType(typeof(T)))!;
                    return true;
                }
            }
            catch (Exception ex)
            {
                // unexpected; maybe manually implemented and missing .Parser property?
                // log it and ignore the type
                Debug.WriteLine(ex.Message);
            }
            // this does not appear to be a Google.Protobuf type
            serializer = null;
            return false;
        }
    }
}
