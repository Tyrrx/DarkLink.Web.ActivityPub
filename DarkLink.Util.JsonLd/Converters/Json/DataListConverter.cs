﻿using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DarkLink.Util.JsonLd.Types;

namespace DarkLink.Util.JsonLd.Converters.Json;

internal class DataListConverter : JsonConverterFactory
{
    private DataListConverter() { }

    public static DataListConverter Instance { get; } = new();

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(DataList<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GenericTypeArguments[0];
        var converterType = typeof(Conv<>).MakeGenericType(itemType);
        var converter = (JsonConverter) Activator.CreateInstance(converterType)!;
        return converter;
    }

    private class Conv<T> : JsonConverter<DataList<T>>
    {
        public override DataList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.StartArray
                ? ReadEnumerable(ref reader, options)
                : ReadSingle(ref reader, options);

        private DataList<T> ReadEnumerable(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var list = new List<T>();
            reader.Read();
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                var item = JsonSerializer.Deserialize<T>(ref reader, options)!; // null should not be in data list
                list.Add(item);
                reader.Read(); // TODO why? shouldn't deserialization read ahead?
            }

            return DataList.FromItems(list);
        }

        private DataList<T> ReadSingle(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            return DataList.From(value);
        }

        public override void Write(Utf8JsonWriter writer, DataList<T> value, JsonSerializerOptions options)
        {
            if (value.Count == 0)
                JsonSerializer.Serialize(writer, default(JsonNode), options);
            else if (value.Count == 1)
                JsonSerializer.Serialize(writer, value.Value, options);
            else
                JsonSerializer.Serialize(writer, value.ToArray(), options);
        }
    }
}

internal class LinkedDataListConverter : JsonConverterFactory
{
    private LinkedDataListConverter() { }

    public static LinkedDataListConverter Instance { get; } = new();

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(LinkedDataList<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GenericTypeArguments[0];
        var converterType = typeof(Conv<>).MakeGenericType(itemType);
        var converter = (JsonConverter) Activator.CreateInstance(converterType)!;
        return converter;
    }

    private class Conv<T> : JsonConverter<LinkedDataList<T>>
    {
        public override LinkedDataList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => JsonSerializer.Deserialize<DataList<LinkOr<T>>>(ref reader, options);

        public override void Write(Utf8JsonWriter writer, LinkedDataList<T> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, (DataList<LinkOr<T>>) value, options);
    }
}
