﻿using System.Text.Json.Serialization;
using DarkLink.Util.JsonLd;
using DarkLink.Util.JsonLd.Attributes;
using DarkLink.Util.JsonLd.Types;
using DarkLink.Web.ActivityPub.Types.Extended;
using LDConstants = DarkLink.Util.JsonLd.Constants;

namespace DarkLink.Web.ActivityPub.Types;

public static class Constants
{
    public const string NAMESPACE = "https://www.w3.org/ns/activitystreams#";

    public static readonly LinkedDataList<ContextEntry> Context = DataList.FromItems(new LinkOr<ContextEntry>[]
    {
        new Uri("https://www.w3.org/ns/activitystreams"),
        new ContextEntry
        {
            MapId("inbox", "ldp:inbox"),
            MapId("outbox", "as:outbox"),
            MapId("url", "as:url"),
            MapId("actor", "as:actor"),
            Map("published", "as:published", "xsd:dateTime"),
            MapId("to", "as:to"),
            MapId("attributedTo", "as:attributedTo"),
            {new("totalItems", UriKind.RelativeOrAbsolute), new Uri("as:totalItems", UriKind.RelativeOrAbsolute)},
        }!,
    });

    private static (Uri Id, TermMapping Mapping) Map(string property, string iri, string type)
        => (new Uri(property, UriKind.Relative),
            new TermMapping(new Uri(iri, UriKind.RelativeOrAbsolute))
            {
                Type = new Uri(type, UriKind.RelativeOrAbsolute),
            });

    private static (Uri Id, TermMapping Mapping) MapId(string property, string iri)
        => (new Uri(property, UriKind.Relative),
            new TermMapping(new Uri(iri, UriKind.RelativeOrAbsolute))
            {
                Type = LDConstants.Id,
            });
}

internal class ActivityStreamsContextProxyResolver : IContextProxyResolver
{
    public IEnumerable<Type> ResolveProxyTypes(Type proxiedType) => typeof(Entity).Assembly.GetExportedTypes()
        .Where(t => (t.Namespace?.StartsWith(typeof(Entity).Namespace!) ?? false)
                    && !t.IsAbstract);
}

[ContextProxy(ProxyTypeResolver = typeof(ActivityStreamsContextProxyResolver))]
public abstract record Entity
{
    public DataList<LinkOr<Object>> AttributedTo { get; init; }

    [LinkedData("@id")] public Uri? Id { get; init; }

    public string? MediaType { get; init; }
}

[LinkedDataType($"{Constants.NAMESPACE}Link")]
public record Link : Entity { }

[LinkedDataType($"{Constants.NAMESPACE}Object")]
public record Object : Entity
{
    [LinkedDataProperty($"{Constants.NAMESPACE}attachment")]
    public DataList<LinkOr<Object>> Attachment { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}content")]
    public string? Content { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}icon")]
    public DataList<LinkTo<Image>> Icon { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}name")]
    public string? Name { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}published")]
    public DateTimeOffset? Published { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}summary")]
    public string? Summary { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}to")]
    public DataList<LinkOr<Object>> To { get; init; }

    [LinkedDataProperty($"{Constants.NAMESPACE}url")]
    public DataList<LinkTo<Object>> Url { get; init; }
}

public abstract record BaseCollectionPage<TPage>
    where TPage : BaseCollectionPage<TPage>
{
    public LinkOr<TPage>? Next { get; init; }

    public LinkOr<Collection>? PartOf { get; init; }

    public LinkOr<TPage>? Prev { get; init; }
}

public abstract record BaseCollection<TPage> : Object
    where TPage : BaseCollectionPage<TPage>
{
    public LinkOr<TPage>? Current { get; init; }

    public LinkOr<TPage>? First { get; init; }

    public LinkOr<TPage>? Last { get; init; }

    public int TotalItems { get; init; }
}

[LinkedData(Constants.NAMESPACE)]
public record CollectionPage : BaseCollectionPage<CollectionPage>
{
    public DataList<LinkOr<Object>> Items { get; init; }
}

[LinkedData(Constants.NAMESPACE)]
public record Collection : BaseCollection<CollectionPage>
{
    public DataList<LinkOr<Object>> Items { get; init; }
}

[LinkedData(Constants.NAMESPACE)]
public record OrderedCollectionPage : BaseCollectionPage<OrderedCollectionPage>
{
    [JsonPropertyName($"{Constants.NAMESPACE}items")]
    public DataList<LinkOr<Object>> OrderedItems { get; init; }

    public int StartIndex { get; init; }
}

[LinkedData(Constants.NAMESPACE)]
public record OrderedCollection : BaseCollection<OrderedCollectionPage>
{
    [JsonPropertyName($"{Constants.NAMESPACE}items")]
    public DataList<LinkOr<Object>> OrderedItems { get; init; }
}

[LinkedData(Constants.NAMESPACE)]
public record Activity : Object
{
    public DataList<LinkTo<Actor>> Actor { get; init; }

    public DataList<LinkTo<Object>> Object { get; init; }
}
