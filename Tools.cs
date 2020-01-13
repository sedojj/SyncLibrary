using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace IntercomSearchProjectCore
{
    public static class Tools
    {
        public const long UnixEpochTicks = 621355968000000000;
        public const long TicksPerMillisecond = 10000;
        public const long TicksPerSecond = TicksPerMillisecond * 1000;

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime FromUnixTimestamp(this long unixTime)
        {
            return new DateTime(UnixEpochTicks + unixTime * TicksPerSecond);
        }

        public static string ToUnixTimestamp(DateTime? date)
        {
            return ((DateTimeOffset)date).ToUnixTimeSeconds().ToString();
        }

        public static void CreateConversationFile(string fileName, string content)
        {

            try
            {
                // Check if file already exists. If yes, delete it.     
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                // Create a new file     
                using (FileStream fs = File.Create(fileName))
                {
                    // Add some text to file    
                    Byte[] fileContent = new UTF8Encoding(true).GetBytes(content);
                    fs.Write(fileContent, 0, fileContent.Length);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());
            }
        }

        public static string WrapInParagraphIfNeeded(string text)
        {
            if (text == null) return "";

            if (text.StartsWith("<p>"))
            {
                return text;
            }
            else
            {
                return text = "<p>" + text + "</p>";
            }

        }

        public static string SanitizeRichText(string inputString)
        {
            return HttpUtility.HtmlEncode(inputString);
        }

        public static string SanitizeSearchText(string inputString)
        {
            inputString = Regex.Replace(inputString, "<.*?>", " ");
            inputString = Regex.Replace(inputString, "\\s+", " ");
            return Regex.Replace(inputString, "&nbsp;", " ").Replace("\"", " ").Replace("\\", "\\\\");
        }

        public static string RemoveParagraphWrapper(string inputString)
        {
            if (inputString != null)
            {
                if (inputString.StartsWith("<p>"))
                {
                    return inputString.Substring(3).Remove(inputString.Length - 7);
                }
            }
            return inputString;
        }


    }
}
