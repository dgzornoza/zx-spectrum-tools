using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZxBasicInstructionSetGenerator.Models;

namespace ZxBasicInstructionSetGenerator
{
    public class JsonGenerator : IDisposable
    {
        private const string BaseUrl = "https://github.com";

        /// <summary>ZxBasic Reserved Identifiers url</summary>
        private readonly static string ZxBasicInstructionsUrl = $"{BaseUrl}/boriel/zxbasic/blob/master/docs/identifier.md";
        /// <summary>Regex for get main menu keywords and links</summary>
        private readonly static Regex menuRegex = new ("<li>.*<a\\s+href=\"(?<url>[^\"]*)\"\\s*>(?<keyword>[^<]*)</a>.*</li>", RegexOptions.Compiled);
        /// <summary>Regex for select description titles, format 1,2,3,4,5,6 for format markdown titles </summary>
        private readonly static string titleRegexPattern = "^\\#{{{0}}}(?<title>[^\\#\\r\\n]*)\\n*";

        private readonly static string[] bitwiseOperators = new string[] { "bAND", "bNOT", "bOR", "bXOR" };
        private readonly static string[] logicalOperators = new string[] { "AND", "NOT", "OR", "XOR" };

        private readonly System.Net.WebClient webClient;
        private bool disposedValue;

        // return => restore
        // REM ???
        // , ALIGN, END, EXP, GOTO, GOSUB, IN, len, ln,, or, out, pause, pi, SGN, StdCall, stop, to, 
        // bAND, bNOT, bOR, bXOR, 
        // AND, NOT, OR, XOR


        public JsonGenerator()
        {
            webClient = new ();
        }

        

        public async Task<string> ExtractInstructionSet()
        {
            IDictionary<string, string> allKeywords = await GetMainMenuKeywordsAndLinks();
            var a = string.Join(Environment.NewLine, allKeywords.Select(item => item.Key));
            IEnumerable<KeyValuePair<string, string>> keywordsLinks = allKeywords.Where(item => !bitwiseOperators.Concat(logicalOperators).Contains(item.Key));
            IEnumerable<KeyValuePair<string, string>> bitwiseOperatorsLinks = allKeywords.Where(item => bitwiseOperators.Contains(item.Key));
            IEnumerable<KeyValuePair<string, string>> logicalLinks = allKeywords.Where(item => logicalOperators.Contains(item.Key));

            // exclude logical operators, have other format            
            IEnumerable<KeywordInfoModel> result = keywordsLinks.Select(async item => await GetKeywordInfo(item)).Select(item => item.Result);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webClient.Dispose();
                }

                disposedValue = true;
            }
        }


        private async Task<IDictionary<string, string>> GetMainMenuKeywordsAndLinks()
        {
            string mainMenuIdentifiers = await webClient.DownloadStringTaskAsync(ZxBasicInstructionsUrl);
            MatchCollection matches = menuRegex.Matches(mainMenuIdentifiers);
            return matches.ToDictionary(keySelector => keySelector.Groups["keyword"].Value, valueSelector => valueSelector.Groups["url"].Value);
        }

        private async Task<KeywordInfoModel> GetKeywordInfo(KeyValuePair<string, string> keywordLink)
        {
            string link = $"{BaseUrl}{keywordLink.Value}";
            string keyWordData;
            string description = null;

            try
            {
                keyWordData = await webClient.DownloadStringTaskAsync($"{link}?raw=true");
                Console.WriteLine($"'{keywordLink.Key}':'{keywordLink.Value}' => OK");

                // remove titles
                description = Regex.Replace(keyWordData, string.Format(titleRegexPattern, 1), string.Empty);

                // set titles ## (2) in markdown bold
                description = Regex.Replace(description, string.Format(titleRegexPattern, 2), delegate (Match m)
                {
                    return $"**{m.Groups["title"].Value.Trim()}**\n\n";
                }, RegexOptions.Multiline);

                // change titles ### (3) in markdown italic
                description = Regex.Replace(description, string.Format(titleRegexPattern, 3), delegate (Match m)
                {
                    return $"*{m.Groups["title"].Value.Trim()}*\n\n";
                }, RegexOptions.Multiline);
            }
            catch (WebException ex)
            {
                Console.WriteLine($"'{keywordLink.Key}':'{keywordLink.Value}' => ERROR: {ex.Message}");
            }

            KeywordInfoModel result = new KeywordInfoModel
            {
                Keyword = keywordLink.Key,
                Description = description,
                Link = link,
            };

            return result;
        }
    }
}
