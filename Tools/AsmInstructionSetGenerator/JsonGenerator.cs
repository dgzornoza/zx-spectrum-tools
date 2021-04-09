using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsmInstructionSetGenerator.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace AsmInstructionSetGenerator
{
    public class JsonGenerator : IDisposable
    {
        /// <summary>Z80 user manual in github pages for create links to pdf opcodes pages and view online.</summary>
        private const string Z80UserManualUrl = "https://dgzornoza.github.io/zx-spectrum-tools/Docs/AssemblerZ80.pdf#page=";

        /// <summary>offset between page number and pdf page number.</summary>
        private const int PageOffset = 14;

        /// <summary>Header regex, format {0} => page number.</summary>
        private const string HeaderRegex = "Z80\\sCPU\\nUser\\sManual\\n{0}\\n";
        private static readonly Regex FooterRegex = new ("(UM008011-0816 Z80 Instruction Description)|(Z80 Instruction Set UM008011-0816)");

        /// <summary>opcodes groups index pages in pdf.</summary>
        private static readonly IEnumerable<OpcodesGroupPageIndex> OpcodesGroupsPages = new List<OpcodesGroupPageIndex>
        {
            new () { PageNumber = 70, LastOpcodePages = 1 },
            new () { PageNumber = 98, LastOpcodePages = 1 },
            new () { PageNumber = 123, LastOpcodePages = 2 },
            new () { PageNumber = 144, LastOpcodePages = 2 },
            new () { PageNumber = 172, LastOpcodePages = 1 },
            new () { PageNumber = 187, LastOpcodePages = 1 },
            new () { PageNumber = 204, LastOpcodePages = 2 },
            new () { PageNumber = 242, LastOpcodePages = 2 },
            new () { PageNumber = 261, LastOpcodePages = 2 },
            new () { PageNumber = 280, LastOpcodePages = 2 },
            new () { PageNumber = 294, LastOpcodePages = 2 },
        };

        /// <summary>Regex for get opcodes page numbers from opcode group index page.</summary>
        private static readonly Regex OpcodeIndexPageRegex = new ($"^(?:.[^–]*)\\s–\\ssee\\spage\\s(?<page>\\d*)$");

        /// <summary>Z80 pdf user manual.</summary>
        private static readonly string Z80UserManualPdfPath = $"{Assembly.GetEntryAssembly().GetName().Name}.Resources.AssemblerZ80.pdf";

        private PdfDocument z80UserManualPdf;
        private bool disposedValue;


        public JsonGenerator()
        {
            LoadPdf();
        }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        public string ExtractInstructionSet()
        {
            IEnumerable<OpcodeInfoModel> result = OpcodesGroupsPages.SelectMany(ReadOpcodesGroupFromPageNumber).ToList();
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    z80UserManualPdf.Close();
                }

                disposedValue = true;
            }
        }


        private static string GetTextBetweenSections(string text, string startWordSection, string endWordSection) =>
            Regex.Match(text, $"{startWordSection}\n(?<text>.+)\n{endWordSection}", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["text"].Value;

        private static bool ContainsSection(string text, string wordSection) => Regex.IsMatch(text, $"^{wordSection}$", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static string RemoveHeaderAndFooter(string text, int pageNumber) =>
            FooterRegex.Replace(Regex.Replace(text, string.Format(HeaderRegex, pageNumber), string.Empty), string.Empty);


        private void LoadPdf()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using Stream stream = assembly.GetManifestResourceStream(Z80UserManualPdfPath);
            z80UserManualPdf = new PdfDocument(new PdfReader(stream));
        }


        private IEnumerable<OpcodeInfoModel> ReadOpcodesGroupFromPageNumber(OpcodesGroupPageIndex opcodesGroupPageIndex)
        {
            PdfPage page = z80UserManualPdf.GetPage(opcodesGroupPageIndex.PageNumber + PageOffset);

            // get text withouth header
            string text = PdfTextExtractor.GetTextFromPage(page, new LocationTextExtractionStrategy());
            text = RemoveHeaderAndFooter(text, opcodesGroupPageIndex.PageNumber);

            IEnumerable<string> textLines = text.Split('\n');

            string groupName = textLines.First();

            List<int> opcodesPages = textLines.Where(item => OpcodeIndexPageRegex.IsMatch(item))
                .Select(item => int.Parse(OpcodeIndexPageRegex.Match(item).Groups["page"].Value)).ToList();

            // add last page with range
            opcodesPages.Add(opcodesPages.Last() + opcodesGroupPageIndex.LastOpcodePages);

            // extract opcodes from current page + page numbers
            return opcodesPages.Zip(opcodesPages.Skip(1), (current, next) => ExtractOpcodeInfo(groupName, current, next - current));
        }


        private OpcodeInfoModel ExtractOpcodeInfo(string groupName, int startPageNumber, int pages)
        {
            StringBuilder strBuilder = new ();
            for (int i = startPageNumber; i < startPageNumber + pages; i++)
            {
                PdfPage page = z80UserManualPdf.GetPage(i + PageOffset);

                string pageText = PdfTextExtractor.GetTextFromPage(page, new LocationTextExtractionStrategy());
                pageText = RemoveHeaderAndFooter(pageText, i);

                strBuilder.Append($"{pageText}{Environment.NewLine}");
            }

            string text = strBuilder.ToString();
            int indexOfExample = text.IndexOf("Example");
            int indexOfConditionBitsAffected = text.IndexOf("Condition Bits Affected");
            OpcodeInfoModel opcodeInfoModel = new ()
            {
                GroupName = groupName.Trim(),
                Keyword = text.Substring(0, text.IndexOf('\n')).Trim(),
                Operation = GetTextBetweenSections(text, "Operation", "Op Code").Trim(),
                Opcode = GetTextBetweenSections(text, "Op Code", "(Operand|Operands)").Trim(),
                Operands = GetTextBetweenSections(text, "(Operand|Operands)", "Description").Trim(),
                Description = GetTextBetweenSections(text, "Description", "Condition Bits Affected").Trim(),
                ConditionBitsAffected = (indexOfExample > 0 ? GetTextBetweenSections(text, "Condition Bits Affected", "Example") : text[(indexOfConditionBitsAffected + "Condition Bits Affected".Length) ..]).Trim(),
                Example = indexOfExample > 0 ? text[(indexOfExample + "Example".Length) ..].Trim() : null,
                Link = $"{Z80UserManualUrl}{startPageNumber + PageOffset}".Trim(),
            };

            // remove carriage return in some properties
            opcodeInfoModel

            return opcodeInfoModel;
        }
    }
}
