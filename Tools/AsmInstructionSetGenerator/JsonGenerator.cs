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
        private static readonly IEnumerable<int> OpcodesGroupsPages = new List<int> { 70, 98, 123, 144, 172, 187, 204, 242, 261, 280, 294 };

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
            Regex.Match(text, string.Format(@"(?<text>{0}\n.+)\n{1}", startWordSection, endWordSection), RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["text"].Value;

        private static string RemoveHeaderAndFooter(string text, int pageNumber) =>
            FooterRegex.Replace(Regex.Replace(text, string.Format(HeaderRegex, pageNumber), string.Empty), string.Empty);


        private void LoadPdf()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using Stream stream = assembly.GetManifestResourceStream(Z80UserManualPdfPath);
            z80UserManualPdf = new PdfDocument(new PdfReader(stream));
        }


        private IEnumerable<OpcodeInfoModel> ReadOpcodesGroupFromPageNumber(int pageNumber)
        {
            PdfPage page = z80UserManualPdf.GetPage(pageNumber + PageOffset);

            // get text withouth header
            string text = PdfTextExtractor.GetTextFromPage(page, new LocationTextExtractionStrategy());
            text = RemoveHeaderAndFooter(text, pageNumber);

            IEnumerable<string> textLines = text.Split('\n');

            string groupName = textLines.First();

            IEnumerable<int> opcodesPages = textLines.Where(item => OpcodeIndexPageRegex.IsMatch(item))
                .Select(item => int.Parse(OpcodeIndexPageRegex.Match(item).Groups["page"].Value));

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
            OpcodeInfoModel opcodeInfoModel = new ()
            {
                GroupName = groupName,
                Keyword = text.Substring(0, text.IndexOf('\n')),
                Operation = GetTextBetweenSections(text, "Operation", "Op Code"),
                Opcode = GetTextBetweenSections(text, "Op Code", "Operands"),
                Operands = GetTextBetweenSections(text, "Operands", "Description"),
                Description = GetTextBetweenSections(text, "Description", "Condition Bits Affected"),
                ConditionBitsAffected = GetTextBetweenSections(text, "Condition Bits Affected", "Example"),
                Example = indexOfExample > 0 ? text[indexOfExample..] : null,
                Link = $"{Z80UserManualUrl}{startPageNumber + PageOffset}",
            };

            return opcodeInfoModel;
        }
    }
}
