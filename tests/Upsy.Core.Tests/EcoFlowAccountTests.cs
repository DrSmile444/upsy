using System.Net;
using System.Text;
using System.Text.Json;
using Upsy.Core.Protocol.EcoFlow;

namespace Upsy.Core.Tests;

public sealed class EcoFlowAccountTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Success_returns_the_user_id_and_sends_the_password_base64()
    {
        var handler = new FakeHandler(_ => Json("""{ "code": "0", "message": "Success", "data": { "user": { "userId": "1234567890123456789" } } }"""));
        using var http = new HttpClient(handler);
        var result = await new EcoFlowAccount(http).SignInAsync("name@example.com", "secret", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("1234567890123456789", result.UserId);
        var (uri, body) = Assert.Single(handler.Requests);
        Assert.Equal(EcoFlowAccount.Hosts[0], uri);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("c2VjcmV0", sent.RootElement.GetProperty("password").GetString());
        Assert.Equal("name@example.com", sent.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Wrong_password_shows_ecoflows_message_and_does_not_try_another_region()
    {
        var handler = new FakeHandler(_ => Json("""{ "code": "2026", "message": "Incorrect password" }"""));
        using var http = new HttpClient(handler);
        var result = await new EcoFlowAccount(http).SignInAsync("name@example.com", "wrong", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Incorrect password", result.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task No_network_says_that_sign_in_needs_internet_once()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("no route"));
        using var http = new HttpClient(handler);
        var result = await new EcoFlowAccount(http).SignInAsync("name@example.com", "secret", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("needs internet once", result.Error, StringComparison.Ordinal);
        Assert.Equal(EcoFlowAccount.Hosts.Length, handler.Requests.Count);
    }

    [Fact]
    public async Task The_eu_host_is_tried_when_the_global_one_fails()
    {
        var handler = new FakeHandler(r => r.RequestUri == EcoFlowAccount.Hosts[0]
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : Json("""{ "code": "0", "data": { "user": { "userId": "42" } } }"""));
        using var http = new HttpClient(handler);
        var result = await new EcoFlowAccount(http).SignInAsync("name@example.com", "secret", TestContext.Current.CancellationToken);

        Assert.Equal("42", result.UserId);
        Assert.Equal(2, handler.Requests.Count);
    }
}
