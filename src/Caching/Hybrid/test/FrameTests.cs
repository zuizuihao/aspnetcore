// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Caching.Hybrid.Internal;
using Xunit.Abstractions;

namespace Microsoft.Extensions.Caching.Hybrid.Tests;
public class FrameTests(ITestOutputHelper Log)
{
    [Fact]
    public void BasicFrameRoundTrip()
    {
        DefaultHybridCache.PayloadHeader header = new(42, 1000000, 99, 12345, "some key", []);

        using var writer = RecyclableArrayBufferWriter<byte>.Create(100);
        header.Write(writer);


        var original = writer.GetCommittedMemory().Span;
        var remaining = original;
        Assert.True(DefaultHybridCache.PayloadHeader.TryParse(ref remaining, out var parsed));
        
        Assert.Equal(42, parsed.Flags);
        Assert.Equal(1000000UL, parsed.EntropyAndCreationTime);
        Assert.Equal(99U, parsed.PayloadSize);
        Assert.Equal(12345UL, parsed.TTL);
        Assert.Equal("some key", parsed.Key);
        Assert.Empty(parsed.Tags);
        Assert.True(remaining.IsEmpty);

        var hex = BitConverter.ToString(writer.GetBuffer(out int length), 0, length);
        Log.WriteLine(hex);
        Assert.Equal("03-01-2A-00-40-42-0F-00-00-00-00-00-63-00-00-00-39-30-00-00-00-00-10-73-6F-6D-65-20-6B-65-79", hex);
        // 03-01                    sentinel+version
        // 2A-00                    flags
        // 40-42-0F-00-00-00-00-00  entropy+creationtime
        // 63-00-00-00              payload size
        // 39-30-00-00-00           ttl
        // 00                       tag count
        // 10                       key length + marker
        // 73-6F-6D-65-20-6B-65-79  key
    }

    [Fact]
    public void FrameRoundTripWithTags()
    {
        DefaultHybridCache.PayloadHeader header = new(42, 1000000, 99, 12345, "some key", ["abc", "def"]);

        using var writer = RecyclableArrayBufferWriter<byte>.Create(100);
        header.Write(writer);

        var original = writer.GetCommittedMemory().Span;
        var remaining = original;
        Assert.True(DefaultHybridCache.PayloadHeader.TryParse(ref remaining, out var parsed));

        Assert.Equal(42, parsed.Flags);
        Assert.Equal(1000000UL, parsed.EntropyAndCreationTime);
        Assert.Equal(99U, parsed.PayloadSize);
        Assert.Equal(12345UL, parsed.TTL);
        Assert.Equal("some key", parsed.Key);
        Assert.Equal(["abc", "def"], parsed.Tags);
        Assert.True(remaining.IsEmpty);

        var hex = BitConverter.ToString(writer.GetBuffer(out int length), 0, length);
        Log.WriteLine(hex);
        Assert.Equal("03-01-2A-00-40-42-0F-00-00-00-00-00-63-00-00-00-39-30-00-00-00-02-10-73-6F-6D-65-20-6B-65-79-06-61-62-63-06-64-65-66", hex);
        // 03-01                    sentinel+version
        // 2A-00                    flags
        // 40-42-0F-00-00-00-00-00  entropy+creationtime
        // 63-00-00-00              payload size
        // 39-30-00-00-00           ttl
        // 02                       tag count
        // 10                       key length + marker
        // 73-6F-6D-65-20-6B-65-79  key
        // 06-61-62-63              "abc"
        // 06-64-65-66              "def"
    }

    [Fact]
    public void FrameRoundTripWithLongKey()
    {
        const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";
        var key = ALPHABET + ALPHABET + ALPHABET + ALPHABET + ALPHABET + ALPHABET;
        Assert.True(key.Length > 130);

        DefaultHybridCache.PayloadHeader header = new(42, 1000000, 99, 12345, key, []);

        using var writer = RecyclableArrayBufferWriter<byte>.Create(1000);
        header.Write(writer);


        var original = writer.GetCommittedMemory().Span;
        var remaining = original;
        Assert.True(DefaultHybridCache.PayloadHeader.TryParse(ref remaining, out var parsed));

        Assert.Equal(42, parsed.Flags);
        Assert.Equal(1000000UL, parsed.EntropyAndCreationTime);
        Assert.Equal(99U, parsed.PayloadSize);
        Assert.Equal(12345UL, parsed.TTL);
        Assert.Equal(key, parsed.Key);
        Assert.Empty(parsed.Tags);
        Assert.True(remaining.IsEmpty);

        var hex = BitConverter.ToString(writer.GetBuffer(out int length), 0, length);
        Log.WriteLine(hex);
        Assert.Equal("03-01-2A-00-40-42-0F-00-00-00-00-00-63-00-00-00-39-30-00-00-00-00-39-01-61-62-63-64-65-66-67-68-69-6A-6B-6C-6D-6E-6F-70-71-72-73-74-75-76-77-78-79-7A-61-62-63-64-65-66-67-68-69-6A-6B-6C-6D-6E-6F-70-71-72-73-74-75-76-77-78-79-7A-61-62-63-64-65-66-67-68-69-6A-6B-6C-6D-6E-6F-70-71-72-73-74-75-76-77-78-79-7A-61-62-63-64-65-66-67-68-69-6A-6B-6C-6D-6E-6F-70-71-72-73-74-75-76-77-78-79-7A-61-62-63-64-65-66-67-68-69-6A-6B-6C-6D-6E-6F-70-71-72-73-74-75-76-77-78-79-7A-61-62-63-64-65-66-67-68-69-6A-6B-6C-6D-6E-6F-70-71-72-73-74-75-76-77-78-79-7A", hex);

    }
}
