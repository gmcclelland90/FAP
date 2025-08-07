using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace FAP.Domain.Services
{
    public class TemplateEngine
    {
        private static readonly ILogger logger = LogManager.GetLogger("faplog");

        public static string Generate(string template, Dictionary<string, object> data)
        {
            try
            {
                logger.Debug($"TemplateEngine.Generate: Starting template processing with {data.Count} data items");
                
                string result = template;

                // Replace simple variables like $variable$
                logger.Debug($"TemplateEngine.Generate: Starting ReplaceSimpleVariables");
                result = ReplaceSimpleVariables(result, data);
                logger.Debug($"TemplateEngine.Generate: After ReplaceSimpleVariables: {result}");
                
                // Replace loops like $files:{file|...}$ (this handles complex variables within loops)
                logger.Debug($"TemplateEngine.Generate: Starting ReplaceLoops");
                result = ReplaceLoops(result, data);
                logger.Debug($"TemplateEngine.Generate: After ReplaceLoops: {result}");
                
                // Replace any remaining complex variables like $model.LocalNode.Nickname$ (but not loop variables)
                logger.Debug($"TemplateEngine.Generate: Starting ReplaceComplexVariables");
                result = ReplaceComplexVariables(result, data);
                logger.Debug($"TemplateEngine.Generate: After ReplaceComplexVariables: {result}");

                logger.Debug($"TemplateEngine.Generate: Template processing completed");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error generating template");
                return template; // Return original template on error
            }
        }

        private static string ReplaceSimpleVariables(string template, Dictionary<string, object> data)
        {
            // Pattern to match simple variables like $variable$ but NOT complex variables like $file.Icon$
            // The negative lookahead ensures we don't match variables with dots
            var pattern = @"\$([a-zA-Z_][a-zA-Z0-9_]*)(?!\.)\$";
            
            return Regex.Replace(template, pattern, match =>
            {
                string varName = match.Groups[1].Value;
                if (data.TryGetValue(varName, out object value))
                {
                    string replacement = value?.ToString() ?? "";
                    logger.Debug($"TemplateEngine: Replaced ${varName}$ with '{replacement}'");
                    return replacement;
                }
                logger.Debug($"TemplateEngine: No replacement found for ${varName}$");
                return match.Value; // Keep original if not found
            });
        }

        private static string ReplaceComplexVariables(string template, Dictionary<string, object> data)
        {
            // Pattern to match complex variables like $file.Name$
            var pattern = @"\$([a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*)\$";
            
            return Regex.Replace(template, pattern, match =>
            {
                string varPath = match.Groups[1].Value;
                logger.Debug($"TemplateEngine: ReplaceComplexVariables found variable ${varPath}$");
                object? value = GetNestedValue(data, varPath);
                string replacement = value?.ToString() ?? "";
                logger.Debug($"TemplateEngine: Replaced ${varPath}$ with '{replacement}'");
                return replacement;
            });
        }

        private static object? GetNestedValue(Dictionary<string, object> data, string path)
        {
            logger.Debug($"GetNestedValue: Resolving path '{path}'");
            string[] parts = path.Split('.');
            object? current = null;

            // Find the root object
            if (data.TryGetValue(parts[0], out object? rootValue))
            {
                current = rootValue;
                logger.Debug($"GetNestedValue: Found root object '{parts[0]}', type: {current?.GetType().Name ?? "null"}");
                // Navigate through the nested properties
                for (int i = 1; i < parts.Length; i++)
                {
                    if (current == null)
                    {
                        logger.Debug($"GetNestedValue: Current object is null at part '{parts[i]}', breaking.");
                        break;
                    }

                    logger.Debug($"GetNestedValue: Processing part '{parts[i]}' on object of type {current.GetType().Name}");

                    // Use reflection to get the property value
                    var property = current.GetType().GetProperty(parts[i]);
                    if (property != null)
                    {
                        current = property.GetValue(current);
                        logger.Debug($"GetNestedValue: Found property '{parts[i]}', value: '{current?.ToString() ?? "null"}'");
                    }
                    else
                    {
                        // Try as dictionary
                        if (current is Dictionary<string, object> dict)
                        {
                            if (dict.TryGetValue(parts[i], out object? dictValue))
                            {
                                current = dictValue;
                                logger.Debug($"GetNestedValue: Found dictionary key '{parts[i]}', value: '{current?.ToString() ?? "null"}'");
                            }
                            else
                            {
                                logger.Debug($"GetNestedValue: Dictionary does not contain key '{parts[i]}'. Setting current to null.");
                                current = null;
                                break;
                            }
                        }
                        else
                        {
                            logger.Debug($"GetNestedValue: Object is not a dictionary and no property '{parts[i]}' found. Setting current to null.");
                            current = null;
                            break;
                        }
                    }
                }
            }
            else
            {
                logger.Debug($"GetNestedValue: Root object '{parts[0]}' not found in data.");
            }

            logger.Debug($"GetNestedValue: Final value for '{path}': '{current?.ToString() ?? "null"}'");
            return current;
        }

        private static string ReplaceLoops(string template, Dictionary<string, object> data)
        {
            // Pattern to match loops like $collection:{item|...}$
            var pattern = @"\$([a-zA-Z_][a-zA-Z0-9_]*):\{([^|]*)\|([^}]*)\}\$";
            
            return Regex.Replace(template, pattern, match =>
            {
                string collectionName = match.Groups[1].Value;
                string itemName = match.Groups[2].Value.Trim();
                string loopTemplate = match.Groups[3].Value;

                logger.Debug($"TemplateEngine: Processing loop for collection '{collectionName}' with item name '{itemName}'");

                if (data.TryGetValue(collectionName, out object? collection))
                {
                    logger.Debug($"TemplateEngine: Found collection '{collectionName}' of type {collection?.GetType().Name ?? "null"}");
                    
                    if (collection is IEnumerable<object> enumerable)
                    {
                        var result = new StringBuilder();
                        foreach (var item in enumerable)
                        {
                            var itemData = new Dictionary<string, object>(data);
                            itemData[itemName] = item;
                            string itemResult = ReplaceSimpleVariables(loopTemplate, itemData);
                            itemResult = ReplaceConditionals(itemResult, itemData);
                            itemResult = ReplaceComplexVariables(itemResult, itemData);
                            result.Append(itemResult);
                        }
                        logger.Debug($"TemplateEngine: Processed {enumerable.Count()} items in loop");
                        return result.ToString();
                    }
                    else if (collection is System.Collections.IEnumerable enumerableCollection)
                    {
                        var result = new StringBuilder();
                        int count = 0;
                        foreach (var item in enumerableCollection)
                        {
                            logger.Debug($"TemplateEngine: Processing item {count + 1} of type {item?.GetType().Name ?? "null"}");
                            if (item is Dictionary<string, object> dict)
                            {
                                logger.Debug($"TemplateEngine: Item is Dictionary with {dict.Count} keys:");
                                foreach (var kvp in dict)
                                {
                                    logger.Debug($"TemplateEngine:   {kvp.Key} = {kvp.Value}");
                                }
                            }
                            
                            // Create a new data context that includes the loop item
                            var itemData = new Dictionary<string, object>(data);
                            itemData[itemName] = item;
                            
                            logger.Debug($"TemplateEngine: Created itemData with {itemData.Count} items:");
                            foreach (var kvp in itemData)
                            {
                                logger.Debug($"TemplateEngine:   itemData[{kvp.Key}] = {kvp.Value?.GetType().Name ?? "null"}");
                            }
                            
                            logger.Debug($"TemplateEngine: Loop template before processing: {loopTemplate.Substring(0, Math.Min(200, loopTemplate.Length))}");
                            
                                            // Process the loop template with the item data
                string itemResult = loopTemplate;
                
                logger.Debug($"TemplateEngine: Processing item {count + 1} - Loop template before any processing: {itemResult}");
                
                // First replace simple variables (like $file$)
                itemResult = ReplaceSimpleVariables(itemResult, itemData);
                logger.Debug($"TemplateEngine: After ReplaceSimpleVariables: {itemResult}");
                
                // Then replace conditionals (like $if:file.HasIcon|...$)
                itemResult = ReplaceConditionals(itemResult, itemData);
                logger.Debug($"TemplateEngine: After ReplaceConditionals: {itemResult}");
                
                // Finally replace complex variables (like $file.Name$)
                itemResult = ReplaceComplexVariables(itemResult, itemData);
                logger.Debug($"TemplateEngine: After ReplaceComplexVariables: {itemResult}");
                
                logger.Debug($"TemplateEngine: Loop template after processing: {itemResult}");
                            
                            result.Append(itemResult);
                            count++;
                        }
                        logger.Debug($"TemplateEngine: Processed {count} items in loop");
                        return result.ToString();
                    }
                }

                logger.Debug($"TemplateEngine: No collection found for '{collectionName}'");
                return ""; // Return empty string if collection not found
            });
        }

        private static string ReplaceConditionals(string template, Dictionary<string, object> data)
        {
            // Pattern to match conditionals like $if:condition|content|else_content$ or $if:condition|content$
            // Use non-greedy matching to properly capture content
            var pattern = @"\$if:([^|]+?)\|(.*?)(?:\|(.*?))?\$";
            
            return Regex.Replace(template, pattern, match =>
            {
                string condition = match.Groups[1].Value.Trim();
                string trueContent = match.Groups[2].Value;
                string falseContent = match.Groups[3].Success ? match.Groups[3].Value : "";

                logger.Debug($"TemplateEngine: Processing conditional '{condition}' with trueContent: '{trueContent}' and falseContent: '{falseContent}'");

                // Evaluate the condition
                bool conditionResult = EvaluateCondition(condition, data);
                
                string contentToUse = conditionResult ? trueContent : falseContent;
                
                logger.Debug($"TemplateEngine: Conditional '{condition}' evaluated to {conditionResult}, using {(conditionResult ? "true" : "false")} content");
                
                // Process the selected content with the same data context
                logger.Debug($"TemplateEngine: Processing conditional content: '{contentToUse}'");
                string processedContent = ReplaceSimpleVariables(contentToUse, data);
                logger.Debug($"TemplateEngine: After ReplaceSimpleVariables in conditional: '{processedContent}'");
                processedContent = ReplaceComplexVariables(processedContent, data);
                logger.Debug($"TemplateEngine: After ReplaceComplexVariables in conditional: '{processedContent}'");
                
                return processedContent;
            }, RegexOptions.Singleline);
        }

        private static bool EvaluateCondition(string condition, Dictionary<string, object> data)
        {
            // Handle different types of conditions
            condition = condition.Trim();
            
            // Check if it's a simple variable existence check
            if (data.TryGetValue(condition, out object value))
            {
                // If the value exists and is not null/empty, return true
                if (value != null)
                {
                    string strValue = value.ToString();
                    return !string.IsNullOrEmpty(strValue) && strValue != "false" && strValue != "0";
                }
                return false;
            }
            
            // Check if it's a complex path (like file.HasIcon)
            object? complexValue = GetNestedValue(data, condition);
            if (complexValue != null)
            {
                string strValue = complexValue.ToString();
                return !string.IsNullOrEmpty(strValue) && strValue != "false" && strValue != "0";
            }
            
            // Check for equality conditions like "variable==value"
            var equalityMatch = Regex.Match(condition, @"^([^=]+)==(.+)$");
            if (equalityMatch.Success)
            {
                string leftSide = equalityMatch.Groups[1].Value.Trim();
                string rightSide = equalityMatch.Groups[2].Value.Trim();
                
                object? leftValue = GetNestedValue(data, leftSide);
                string leftStr = leftValue?.ToString() ?? "";
                string rightStr = rightSide.Trim('"', '\''); // Remove quotes
                
                return leftStr == rightStr;
            }
            
            // Check for inequality conditions like "variable!=value"
            var inequalityMatch = Regex.Match(condition, @"^([^=]+)!=([^=]+)$");
            if (inequalityMatch.Success)
            {
                string leftSide = inequalityMatch.Groups[1].Value.Trim();
                string rightSide = inequalityMatch.Groups[2].Value.Trim();
                
                object? leftValue = GetNestedValue(data, leftSide);
                string leftStr = leftValue?.ToString() ?? "";
                string rightStr = rightSide.Trim('"', '\''); // Remove quotes
                
                return leftStr != rightStr;
            }
            
            logger.Debug($"TemplateEngine: Condition '{condition}' not found in data, returning false");
            return false;
        }
    }
}