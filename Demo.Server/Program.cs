﻿using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using DarkLink.Util.JsonLd;
using DarkLink.Util.JsonLd.Types;
using DarkLink.Web.ActivityPub.Serialization;
using DarkLink.Web.ActivityPub.Types;
using DarkLink.Web.ActivityPub.Types.Extended;
using DarkLink.Web.WebFinger.Server;
using Microsoft.AspNetCore.Http.Extensions;
using ASLink = DarkLink.Web.ActivityPub.Types.Link;
using Object = DarkLink.Web.ActivityPub.Types.Object;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddWebFinger<ResourceDescriptorProvider>();

var app = builder.Build();
app.UseWebFinger();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var commonContext = DataList.FromItems(new LinkOr<ContextEntry>[]
{
    new Uri("https://www.w3.org/ns/activitystreams"),
    new ContextEntry
    {
        {new("inbox", UriKind.RelativeOrAbsolute), new Uri("ldp:inbox", UriKind.RelativeOrAbsolute)},
        {new("outbox", UriKind.RelativeOrAbsolute), new Uri("as:outbox", UriKind.RelativeOrAbsolute)},
        {new("url", UriKind.RelativeOrAbsolute), new Uri("as:url", UriKind.RelativeOrAbsolute)},
        {new("actor", UriKind.RelativeOrAbsolute), new Uri("as:actor", UriKind.RelativeOrAbsolute)},
        {new("published", UriKind.RelativeOrAbsolute), new Uri("as:published", UriKind.RelativeOrAbsolute)},
        {new("to", UriKind.RelativeOrAbsolute), new Uri("as:to", UriKind.RelativeOrAbsolute)},
        {new("attributedTo", UriKind.RelativeOrAbsolute), new Uri("as:attributedTo", UriKind.RelativeOrAbsolute)},
        {new("totalItems", UriKind.RelativeOrAbsolute), new Uri("as:totalItems", UriKind.RelativeOrAbsolute)},
    }!,
});

var jsonOptions = new JsonSerializerOptions
{
    Converters =
    {
        LinkToConverter.Instance,
    },
};

app.MapGet("/profiles/{username}", (string username) => $"Welcome to the profile of [{username}].");

app.MapGet("/profile.png", async ctx => { await ctx.Response.SendFileAsync("./profile.png", ctx.RequestAborted); });

app.MapGet("/profiles/{username}.json", async ctx =>
{
    if (!ctx.Request.RouteValues.TryGetValue("username", out var usernameRaw)
        || usernameRaw is not string username)
    {
        ctx.Response.StatusCode = (int) HttpStatusCode.BadRequest;
        return;
    }

    if (!Directory.Exists($"./data/{username}"))
    {
        ctx.Response.StatusCode = (int) HttpStatusCode.NotFound;
        return;
    }

    ctx.Response.Headers.CacheControl = "max-age=0, private, must-revalidate";
    ctx.Response.Headers.ContentType = "application/activity+json; charset=utf-8";

    var person = new Person(new Uri($"https://devtunnel.dark-link.info/profiles/{username}/inbox"), new Uri($"https://devtunnel.dark-link.info/profiles/{username}/outbox"))
    {
        Id = new Uri($"https://devtunnel.dark-link.info/profiles/{username}.json"),
        PreferredUsername = username,
        Name = $"Waldemar Tomme [{username}]",
        Summary = "Just testing around 🧪",
        Url = DataList.From<LinkOr<ASLink>>(new Link<ASLink>(new Uri($"https://devtunnel.dark-link.info/profiles/{username}"))),
        Icon = DataList.From<LinkOr<Image>>(new Object<Image>(new Image
        {
            MediaType = "image/png",
            Url = DataList.From<LinkOr<ASLink>>(new Link<ASLink>(new Uri("https://devtunnel.dark-link.info/profile.png"))),
        })),
    };

    var node = new JsonLdSerializer().Serialize(person, commonContext, jsonOptions);

    await ctx.Response.WriteAsync(node?.ToString() ?? string.Empty, ctx.RequestAborted);
});

app.MapGet("/profiles/{username}/outbox", async ctx =>
{
    if (!CheckRequest(ctx, out var username)) return;

    var activities = await new DirectoryInfo($"./data/{username}")
        .EnumerateFiles("*.txt")
        .OrderBy(f => f.CreationTime)
        .Select(f => GetNoteActivityAsync(username, f.Name, ctx.RequestAborted))
        .WhenAll();

    var outboxCollection = new OrderedCollection
    {
        TotalItems = activities.Length,
        OrderedItems = DataList.FromItems<LinkOr<Object>>(activities.Select(a => new Object<Object>(a))),
    };

    var node = new JsonLdSerializer().Serialize(outboxCollection, commonContext, jsonOptions);

    ctx.Response.Headers.ContentType = "application/activity+json; charset=utf-8";
    await ctx.Response.WriteAsync(node?.ToString() ?? string.Empty, ctx.RequestAborted);
});

app.MapMethods(
    "/{*path}",
    new[] {HttpMethods.Get, HttpMethods.Post},
    async ctx =>
    {
        var headers = string.Join('\n', ctx.Request.Headers.Select(h => $"{h.Key}: {h.Value}"));
        var query = string.Join('\n', ctx.Request.Query.Select(q => $"{q.Key}={q.Value}"));
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();
        logger.LogDebug($"{ctx.Request.Method} {ctx.Request.GetDisplayUrl()}\n{query}\n{headers}\n{body}");
        ctx.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
        await ctx.Response.CompleteAsync();
    });

app.Run();

bool CheckRequest(HttpContext ctx, [NotNullWhen(true)] out string? username)
{
    username = default;
    if (!ctx.Request.RouteValues.TryGetValue("username", out var usernameRaw)
        || usernameRaw is not string usernameLocal)
    {
        ctx.Response.StatusCode = (int) HttpStatusCode.BadRequest;
        return false;
    }

    if (!Directory.Exists($"./data/{usernameLocal}"))
    {
        ctx.Response.StatusCode = (int) HttpStatusCode.NotFound;
        return false;
    }

    username = usernameLocal;
    return true;
}

async Task<Note> GetNoteAsync(string username, string filename, CancellationToken cancellationToken = default)
{
    var fileInfo = new FileInfo($"./data/{username}/{filename}");
    return new Note
    {
        Id = new Uri($"https://devtunnel.dark-link.info/notes/{username}/{fileInfo.Name}"),
        AttributedTo = DataList.From<LinkOr<Object>>(new Link<Object>(new Uri($"https://devtunnel.dark-link.info/notes/{username}.json"))),
        Published = fileInfo.CreationTime,
        To = DataList.From<LinkOr<Object>>(new Link<Object>(new Uri("https://www.w3.org/ns/activitystreams#Public"))),
        Content = await File.ReadAllTextAsync(fileInfo.FullName, cancellationToken),
    };
}

async Task<Create> GetNoteActivityAsync(string username, string filename, CancellationToken cancellationToken = default)
{
    var note = await GetNoteAsync(username, filename, cancellationToken);
    return new Create
    {
        Id = new Uri($"{note.Id}/activity"),
        Published = note.Published,
        To = note.To,
        Actor = DataList.From<LinkTo<Actor>>(new Uri($"https://devtunnel.dark-link.info/profiles/{username}")),
        Object = DataList.From<LinkTo<Object>>(note),
    };
}

internal static class Helper
{
    public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> tasks, CancellationToken cancellationToken = default)
        => Task.WhenAll(tasks);
}
