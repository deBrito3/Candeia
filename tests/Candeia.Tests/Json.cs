using System.Text.Json;
using Candeia.Execution;

namespace Candeia.Tests;

static class Json
{
    public static JsonElement Of(object? value) => JsonSerializer.SerializeToElement(value);

    // A test case from plain values: Test(true, 12, 22) expects true for the arguments 12 and 22.
    public static TestCase Test(object? expected, params object?[] args) => new(args.Select(Of).ToArray(), Of(expected));
}
