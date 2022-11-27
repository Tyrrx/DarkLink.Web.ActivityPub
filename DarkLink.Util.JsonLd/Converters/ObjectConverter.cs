﻿using System.Reflection;
using DarkLink.Util.JsonLd.Attributes;
using DarkLink.Util.JsonLd.Types;

namespace DarkLink.Util.JsonLd.Converters;

internal class ObjectConverter : ILinkedDataConverter
{
    public bool CanConvert(Type typeToConvert) => !typeToConvert.IsPrimitive && typeToConvert != typeof(string);

    public object? Convert(DataList<LinkedData> dataList, Type typeToConvert, LinkedDataSerializationOptions options)
    {
        var data = dataList.Value;
        if (data is null) return null;

        var obj = Activator.CreateInstance(typeToConvert)!;
        foreach (var property in typeToConvert.GetProperties())
        {
            if (property.Name.Equals("id", StringComparison.InvariantCultureIgnoreCase))
            {
                property.SetValue(obj, data.Id);
                continue;
            }

            if (property.Name.Equals("type", StringComparison.CurrentCultureIgnoreCase))
            {
                property.SetValue(obj, data.Type);
                continue;
            }

            var linkedDataProperty = property.GetCustomAttribute<LinkedDataPropertyAttribute>() ?? throw new InvalidOperationException();
            var propertyData = data[linkedDataProperty.Iri];
            var convertedValue = LinkedDataSerializer.DeserializeFromLinkedData(DataList.FromItems(propertyData), property.PropertyType, options);
            property.SetValue(obj, convertedValue);
        }

        return obj;
    }

    public DataList<LinkedData> ConvertBack(object? value, Type typeToConvert, LinkedDataSerializationOptions options)
    {
        var valueType = value?.GetType() ?? typeToConvert;
        if (value is null)
            return default;

        var data = new LinkedData
        {
            Type = DataList.FromItems(valueType.GetCustomAttributes<LinkedDataTypeAttribute>()
                .Select(attr => attr.Type)),
        };
        var properties = new Dictionary<Uri, DataList<LinkedData>>(UriEqualityComparer.Default);
        foreach (var property in valueType.GetProperties())
        {
            if (property.Name.Equals("id", StringComparison.InvariantCultureIgnoreCase))
            {
                data = data with
                {
                    Id = (Uri?) property.GetValue(value),
                };
                continue;
            }

            if (property.Name.Equals("type", StringComparison.CurrentCultureIgnoreCase))
            {
                data = data with
                {
                    Type = (DataList<Uri>) property.GetValue(value)!,
                };
                continue;
            }

            var linkedDataProperty = property.GetCustomAttribute<LinkedDataPropertyAttribute>() ?? throw new InvalidOperationException();
            var propertyValue = property.GetValue(value);
            var propertyData = LinkedDataSerializer.SerializeToLinkedData(propertyValue, property.PropertyType, options);
            properties.Add(linkedDataProperty.Iri, propertyData);
        }

        return data with
        {
            Properties = properties,
        };
    }
}
