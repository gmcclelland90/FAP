using System.Text.Json;

namespace FAP.Domain
{
    public static class JsonConfiguration
    {
        // Preserve existing persisted files: PascalCase, case-insensitive, indented
        public static readonly JsonSerializerOptions IndentedOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Compact variant (used by queue), PascalCase, case-insensitive
        public static readonly JsonSerializerOptions CompactOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }
}


