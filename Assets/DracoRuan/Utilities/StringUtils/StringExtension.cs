using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using UnityEngine;

namespace DracoRuan.Utilities.StringUtils
{
    public static class StringExtension
    {
        public static bool FastEqual(this string s, string target) => string.CompareOrdinal(s, target) == 0;
        
        /// <summary>
        /// Converts a string to lower snake case format.
        /// Example: "HelloMyWorld" -> "hello_my_world", "Hello My World" -> "hello_my_world"
        /// </summary>
        /// <param name="input">The input string to convert</param>
        /// <returns>The string converted to lower snake case</returns>
        public static string ToSnakeCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            
            var result = new StringBuilder();
            var previousChar = char.MinValue;
            
            foreach (var currentChar in input)
            {
                // Skip spaces and convert to underscores
                if (char.IsWhiteSpace(currentChar))
                {
                    if (previousChar != '_' && result.Length > 0)
                    {
                        result.Append('_');
                    }
                    previousChar = '_';
                    continue;
                }
                
                // Add underscore before uppercase letters (except at the beginning)
                if (char.IsUpper(currentChar) && result.Length > 0 && previousChar != '_')
                {
                    result.Append('_');
                }
                
                // Convert to lowercase and add to result
                result.Append(char.ToLower(currentChar));
                previousChar = currentChar;
            }
            
            return result.ToString();
        }
        
        /// <summary>
        /// Tách một chuỗi thành danh sách các chuỗi con nếu chuỗi dài hơn maxLength.
        /// Mỗi chuỗi con sẽ có độ dài tối đa là subLength.
        /// </summary>
        /// <param name="input">Chuỗi đầu vào cần tách</param>
        /// <param name="maxLength">Độ dài tối đa cho phép trước khi tách</param>
        /// <param name="subLength">Độ dài tối đa của mỗi chuỗi con</param>
        /// <returns>Danh sách các chuỗi con</returns>
        /// <example>
        /// <code>
        /// string longText = "Đây là một chuỗi rất dài cần được tách ra thành nhiều phần nhỏ hơn";
        /// var result = longText.SplitIfExceeds(20, 10);
        /// // Kết quả: ["Đây là một", "chuỗi rất", "dài cần", "được tách", "ra thành", "nhiều phần", "nhỏ hơn"]
        /// </code>
        /// </example>
        public static List<string> SplitIfExceeds(this string input, int maxLength, int subLength)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new List<string> { String.Empty };
            }
            
            if (maxLength <= 0)
            {
                throw new ArgumentException("maxLength phải là số nguyên dương", nameof(maxLength));
            }
            
            if (subLength <= 0)
            {
                throw new ArgumentException("subLength phải là số nguyên dương", nameof(subLength));
            }
            
            if (subLength > maxLength)
            {
                throw new ArgumentException("subLength không được lớn hơn maxLength", nameof(subLength));
            }
            
            // Nếu chuỗi ngắn hơn hoặc bằng maxLength, trả về chuỗi gốc
            if (input.Length <= maxLength)
            {
                return new List<string> { input };
            }
            
            var result = new List<string>();
            int currentIndex = 0;
            
            while (currentIndex < input.Length)
            {
                // Tính toán độ dài của chuỗi con tiếp theo
                int remainingLength = input.Length - currentIndex;
                int currentSubLength = Mathf.Min(subLength, remainingLength);
                
                // Lấy chuỗi con
                string substring = input.Substring(currentIndex, currentSubLength);
                result.Add(substring);
                
                // Di chuyển index
                currentIndex += currentSubLength;
            }
            
            return result;
        }

        /// <summary>
        /// Parse URL parameters to a dictionary of parameters.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Dictionary<string, string>? ParseUrlParameters(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // Try to parse as URI
            if (!Uri.TryCreate(input, UriKind.Absolute, out Uri uri))
                return null;

            // Check if it has a valid scheme (http, https, ftp, etc.)
            if (string.IsNullOrEmpty(uri.Scheme))
                return null;

            // Parse query parameters
            var query = HttpUtility.ParseQueryString(uri.Query);
            Dictionary<string, string> result = query.AllKeys.ToDictionary(key => key, key => query[key]);
            return result;
        }
    }
}