using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        /// <summary>Regex for hack markdown code region, replace \n\n for \n</summary>
        private readonly static Regex hackCodeMarkdownRegex = new ("```[^`]*```", RegexOptions.Compiled | RegexOptions.Multiline);
        /// <summary>Regex for add base uri to relative markdown links</summary>
        private readonly static Regex linksReplaceRegex = new ("\\[.*\\]\\((?<url>.*)\\)", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Regex for select description titles, format 1,2,3,4,5,6 for format markdown titles </summary>
        private readonly static string titleRegexPattern = "^\\#{{{0}}}\\s*(?<title>\\w+[^\\r\\n]*)\\n+";
        
        private readonly static string[] bitwiseOperators = new string[] { "bAND", "bNOT", "bOR", "bXOR" };
        private readonly static string[] logicalOperators = new string[] { "AND", "NOT", "OR", "XOR" };

        private readonly WebClient webClient;
        private IEnumerable<KeywordInfoModel> localKeywords;
        private bool disposedValue;
        

        // bAND, bNOT, bOR, bXOR, 
        // AND, NOT, OR, XOR


        public JsonGenerator()
        {
            webClient = new ();
            localKeywords = JsonSerializer.Deserialize<IEnumerable<KeywordInfoModel>>(File.ReadAllText("Resources/LocalKeywords.json"), 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        

        public async Task<string> ExtractInstructionSet()
        {
            IDictionary<string, string> allKeywords = await GetMainMenuKeywordsAndLinks();
            
            IEnumerable<KeyValuePair<string, string>> keywordsLinks = allKeywords.Where(item => !bitwiseOperators.Concat(logicalOperators).Contains(item.Key));
            IEnumerable<KeyValuePair<string, string>> bitwiseOperatorsLinks = allKeywords.Where(item => bitwiseOperators.Contains(item.Key));
            IEnumerable<KeyValuePair<string, string>> logicalLinks = allKeywords.Where(item => logicalOperators.Contains(item.Key));

            // online info exclude logical operators, have other format            
            IEnumerable<KeywordInfoModel> result = keywordsLinks.Select(async item => await GetOnlineKeywordInfo(item)).Select(item => item.Result);

            // get operators info from local keywords
            result = result.Concat(bitwiseOperatorsLinks.Concat(logicalLinks).Select(item => GetKeywordInfo(item)));

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

        private async Task<KeywordInfoModel> GetOnlineKeywordInfo(KeyValuePair<string, string> keywordLink)
        {
            string link = $"{BaseUrl}{keywordLink.Value}";
            string linkFolder = link.Substring(0, link.LastIndexOf('/') + 1);
            string keyWordData;
            string description = string.Empty;

            try
            {
                keyWordData = await webClient.DownloadStringTaskAsync($"{link}?raw=true");
                Console.WriteLine($"'{keywordLink.Key}':'{keywordLink.Value}' => OK");

                // apply hack:
                description = hackCodeMarkdownRegex.Replace(keyWordData, match => match.Value.Replace("\n\n", "\n"));

                // set absolute urls in markdowns
                description = linksReplaceRegex.Replace(description, (match) =>
                {
                    string urlValue = match.Groups["url"].Value;
                    return urlValue.StartsWith("http") ? match.Value : match.Value.Replace(urlValue, $"{linkFolder}{urlValue}");
                });
                                
                // remove titles
                description = Regex.Replace(description, string.Format(titleRegexPattern, 1), string.Empty);

                // set titles ## (2) in markdown bold
                description = Regex.Replace(description, string.Format(titleRegexPattern, 2), match => $"**{match.Groups["title"].Value.Trim()}**\n\n", RegexOptions.Multiline);

                // change titles ### (3) in markdown italic
                description = Regex.Replace(description, string.Format(titleRegexPattern, 3), match => $"*{match.Groups["title"].Value.Trim()}*\n\n", RegexOptions.Multiline);
            }
            catch (WebException ex)
            {
                WriteToConsoleInColor(ConsoleColor.Red, $"'{keywordLink.Key}':'{keywordLink.Value}' => ERROR: {ex.Message}");

                description = TryGetDescriptionFromLocalKeywords(keywordLink.Key.ToUpper());
            }

            KeywordInfoModel result = new ()
            {
                Keyword = keywordLink.Key.ToUpper(),
                Description = description,
                Link = link,
            };

            return result;
        }

        private KeywordInfoModel GetKeywordInfo(KeyValuePair<string, string> keywordLink)
        {
            try
            {
                KeywordInfoModel keyword = localKeywords.First(item => item.Keyword == keywordLink.Key);
                Console.WriteLine($"'{keywordLink.Key}':'{keywordLink.Value}' => OK");

                return new()
                {
                    Keyword = keywordLink.Key.ToUpper(),
                    Description = keyword.Description,
                    Link = keyword.Link,
                };
            }
            catch (Exception ex)
            {
                WriteToConsoleInColor(ConsoleColor.Red, $"'{keywordLink.Key}':'{keywordLink.Value}' => ERROR: {ex.Message}");
            }

            return null;
        }


        private string TryGetDescriptionFromLocalKeywords(string keyword)
        {
            Console.WriteLine("Try get from local keywords");
            string result = localKeywords.FirstOrDefault(item => item.Keyword == keyword)?.Description ?? string.Empty;
            if (null == result) WriteToConsoleInColor(ConsoleColor.Red, "ERROR: Not found in local keywords");
            else WriteToConsoleInColor(ConsoleColor.Green, "Found in local keywords");
            return result;
        }

        private void WriteToConsoleInColor(ConsoleColor color, string text)
        {
            ConsoleColor current = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = current;
        }
    }
}
