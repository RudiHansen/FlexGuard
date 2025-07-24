using System.Text.Json;

namespace FlexGuard.Core.Util;
public static class JsonSettings
{
    public static readonly JsonSerializerOptions Indented = new JsonSerializerOptions
    {
        WriteIndented = true
    };
    public static readonly JsonSerializerOptions DeserializeIgnoreCase = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}