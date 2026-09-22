using System.Net;
using System.Text;

namespace ClearTextAI.Core.Tests.Fakes;

/// <summary>Returns a canned response, or throws, so provider code can be exercised without a network.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    public static StubHttpMessageHandler Json(HttpStatusCode statusCode, string json) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    public static StubHttpMessageHandler Throws(Exception exception) => new(_ => throw exception);

    /// <summary>Replays the given responses in order, repeating the last one once exhausted.</summary>
    public static StubHttpMessageHandler Sequence(params (HttpStatusCode Status, string Json)[] responses)
    {
        var index = 0;
        return new(_ =>
        {
            var current = responses[Math.Min(index++, responses.Length - 1)];
            return new HttpResponseMessage(current.Status)
            {
                Content = new StringContent(current.Json, Encoding.UTF8, "application/json"),
            };
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        return _responder(request);
    }
}
