using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ShiftTrack.Api.Tests;

internal static class ResultTestHelpers
{
    internal sealed record ExecutedResult(int StatusCode, string Body, byte[] Bytes, IHeaderDictionary Headers, string? ContentType);

    internal static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var executed = await ExecuteWithHeadersAsync(result);
        return (executed.StatusCode, executed.Body);
    }

    internal static async Task<ExecutedResult> ExecuteWithHeadersAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var bytes = ((MemoryStream)context.Response.Body).ToArray();
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return new ExecutedResult(
            context.Response.StatusCode,
            await reader.ReadToEndAsync(),
            bytes,
            context.Response.Headers,
            context.Response.ContentType);
    }

    internal static T? ReadJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
