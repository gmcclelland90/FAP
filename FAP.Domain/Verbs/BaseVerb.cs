#region Copyright Kayomani 2011.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.

/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */

#endregion

using System.Text.Json;

namespace FAP.Domain.Verbs
{
    public class BaseVerb
    {
        public static T? Deserialise<T>(string json)
        {
            var typeInfo = FAP.Domain.FapJsonContext.Default.GetTypeInfo(typeof(T));
            if (typeInfo is null)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = null };
                return JsonSerializer.Deserialize<T>(json, options);
            }
            return (T?)JsonSerializer.Deserialize(json, typeInfo);
        }

        public static string Serialize<T>(T obj)
        {
            var typeInfo = FAP.Domain.FapJsonContext.Default.GetTypeInfo(typeof(T));
            if (typeInfo is null)
            {
                // Fallback to options if T wasn't registered in the context
                var options = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = null, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
                return JsonSerializer.Serialize(obj, options);
            }
            return JsonSerializer.Serialize(obj, typeInfo);
        }
    }
}