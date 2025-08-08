using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FAP.Domain.Services
{
    public class TemplateEngine
    {
        private static readonly ILogger logger = NullLogger.Instance;

        public static string Generate(string template, Dictionary<string, object> data)
        {
            try
            {
            logger.LogDebug("TemplateEngine.Generate: Starting template processing with {Count} data items", data.Count);
                
                string result = template;

                // Replace simple variables like $variable$
            logger.LogDebug("TemplateEngine.Generate: Starting ReplaceSimpleVariables");
                result = ReplaceSimpleVariables(result, data);
            logger.LogDebug("TemplateEngine.Generate: After ReplaceSimpleVariables: {Result}", result);
                
                // Replace loops like $files:{file|...}$ (this handles complex variables within loops)
            logger.LogDebug("TemplateEngine.Generate: Starting ReplaceLoops");
                result = ReplaceLoops(result, data);
            logger.LogDebug("TemplateEngine.Generate: After ReplaceLoops: {Result}", result);
                
                // Replace any remaining complex variables like $model.LocalNode.Nickname$ (but not loop variables)
            logger.LogDebug("TemplateEngine.Generate: Starting ReplaceComplexVariables");
                result = ReplaceComplexVariables(result, data);
            logger.LogDebug("TemplateEngine.Generate: After ReplaceComplexVariables: {Result}", result);

            logger.LogDebug("TemplateEngine.Generate: Template processing completed");
                return result;
            }
            catch (Exception ex)
            {
            logger.LogError(ex, "Error generating template");
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
                    logger.LogDebug("TemplateEngine: Replaced ${Var}$ with '{Replacement}'", varName, replacement);
                    return replacement;
                }
                logger.LogDebug("TemplateEngine: No replacement found for ${Var}$", varName);
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
            logger.LogDebug("TemplateEngine: ReplaceComplexVariables found variable ${VarPath}$", varPath);
                object? value = GetNestedValue(data, varPath);
                string replacement = value?.ToString() ?? "";
            logger.LogDebug("TemplateEngine: Replaced ${VarPath}$ with '{Replacement}'", varPath, replacement);
                return replacement;
            });
        }

        private static object? GetNestedValue(Dictionary<string, object> data, string path)
        {
            logger.LogDebug("GetNestedValue: Resolving path '{Path}'", path);
            string[] parts = path.Split('.');
            object? current = null;

            // Find the root object
            if (data.TryGetValue(parts[0], out object? rootValue))
            {
                current = rootValue;
                logger.LogDebug("GetNestedValue: Found root object '{Root}', type: {Type}", parts[0], current?.GetType().Name ?? "null");
                // Navigate through the nested properties
                for (int i = 1; i < parts.Length; i++)
                {
                    if (current == null)
                    {
                        logger.LogDebug("GetNestedValue: Current object is null at part '{Part}', breaking.", parts[i]);
                        break;
                    }

                    logger.LogDebug("GetNestedValue: Processing part '{Part}' on object of type {Type}", parts[i], current.GetType().Name);

                    // Use reflection to get the property value
                    var property = current.GetType().GetProperty(parts[i]);
                    if (property != null)
                    {
                        current = property.GetValue(current);
                        logger.LogDebug("GetNestedValue: Found property '{Part}', value: '{Value}'", parts[i], current?.ToString() ?? "null");
                    }
                    else
                    {
                        // Try as dictionary
                        if (current is Dictionary<string, object> dict)
                        {
                            if (dict.TryGetValue(parts[i], out object? dictValue))
                            {
                                current = dictValue;
                                logger.LogDebug("GetNestedValue: Found dictionary key '{Part}', value: '{Value}'", parts[i], current?.ToString() ?? "null");
                            }
                            else
                            {
                                logger.LogDebug("GetNestedValue: Dictionary does not contain key '{Part}'. Setting current to null.", parts[i]);
                                current = null;
                                break;
                            }
                        }
                        else
                        {
                            logger.LogDebug("GetNestedValue: Object is not a dictionary and no property '{Part}' found. Setting current to null.", parts[i]);
                            current = null;
                            break;
                        }
                    }
                }
            }
            else
            {
            logger.LogDebug("GetNestedValue: Root object '{Root}' not found in data.", parts[0]);
            }

            logger.LogDebug("GetNestedValue: Final value for '{Path}': '{Value}'", path, current?.ToString() ?? "null");
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

            logger.LogDebug("TemplateEngine: Processing loop for collection '{Collection}' with item name '{ItemName}'", collectionName, itemName);

                if (data.TryGetValue(collectionName, out object? collection))
                {
                    logger.LogDebug("TemplateEngine: Found collection '{Collection}' of type {Type}", collectionName, collection?.GetType().Name ?? "null");
                    
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
                        logger.LogDebug("TemplateEngine: Processed {Count} items in loop", enumerable.Count());
                        return result.ToString();
                    }
                    else if (collection is System.Collections.IEnumerable enumerableCollection)
                    {
                        var result = new StringBuilder();
                        int count = 0;
                        foreach (var item in enumerableCollection)
                        {
                            logger.LogDebug("TemplateEngine: Processing item {Index} of type {Type}", count + 1, item?.GetType().Name ?? "null");
                            if (item is Dictionary<string, object> dict)
                            {
                                logger.LogDebug("TemplateEngine: Item is Dictionary with {Count} keys:", dict.Count);
                                foreach (var kvp in dict)
                                {
                                    logger.LogDebug("TemplateEngine:   {Key} = {Value}", kvp.Key, kvp.Value);
                                }
                            }
                            
                            // Create a new data context that includes the loop item
                            var itemData = new Dictionary<string, object>(data);
                            itemData[itemName] = item;
                            
                            logger.LogDebug("TemplateEngine: Created itemData with {Count} items:", itemData.Count);
                            foreach (var kvp in itemData)
                            {
                                logger.LogDebug("TemplateEngine:   itemData[{Key}] = {Type}", kvp.Key, kvp.Value?.GetType().Name ?? "null");
                            }
                            
                            logger.LogDebug("TemplateEngine: Loop template before processing: {Snippet}", loopTemplate.Substring(0, Math.Min(200, loopTemplate.Length)));
                            
                                            // Process the loop template with the item data
                string itemResult = loopTemplate;
                
                logger.LogDebug("TemplateEngine: Processing item {Index} - Loop template before any processing: {ItemResult}", count + 1, itemResult);
                
                // First replace simple variables (like $file$)
                itemResult = ReplaceSimpleVariables(itemResult, itemData);
                logger.LogDebug("TemplateEngine: After ReplaceSimpleVariables: {ItemResult}", itemResult);
                
                // Then replace conditionals (like $if:file.HasIcon|...$)
                itemResult = ReplaceConditionals(itemResult, itemData);
                logger.LogDebug("TemplateEngine: After ReplaceConditionals: {ItemResult}", itemResult);
                
                // Finally replace complex variables (like $file.Name$)
                itemResult = ReplaceComplexVariables(itemResult, itemData);
                logger.LogDebug("TemplateEngine: After ReplaceComplexVariables: {ItemResult}", itemResult);
                
                logger.LogDebug("TemplateEngine: Loop template after processing: {ItemResult}", itemResult);
                            
                            result.Append(itemResult);
                            count++;
                        }
                        logger.LogDebug("TemplateEngine: Processed {Count} items in loop", count);
                        return result.ToString();
                    }
                }

                logger.LogDebug("TemplateEngine: No collection found for '{Collection}'", collectionName);
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

            logger.LogDebug("TemplateEngine: Processing conditional '{Condition}' with trueContent: '{TrueContent}' and falseContent: '{FalseContent}'", condition, trueContent, falseContent);

                // Evaluate the condition
                bool conditionResult = EvaluateCondition(condition, data);
                
                string contentToUse = conditionResult ? trueContent : falseContent;
                
            logger.LogDebug("TemplateEngine: Conditional '{Condition}' evaluated to {Result}", condition, conditionResult);
                
                // Process the selected content with the same data context
            logger.LogDebug("TemplateEngine: Processing conditional content: '{Content}'", contentToUse);
                string processedContent = ReplaceSimpleVariables(contentToUse, data);
            logger.LogDebug("TemplateEngine: After ReplaceSimpleVariables in conditional: '{Content}'", processedContent);
                processedContent = ReplaceComplexVariables(processedContent, data);
            logger.LogDebug("TemplateEngine: After ReplaceComplexVariables in conditional: '{Content}'", processedContent);
                
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
            
            logger.LogDebug("TemplateEngine: Condition '{Condition}' not found in data, returning false", condition);
            return false;
        }
    }
}