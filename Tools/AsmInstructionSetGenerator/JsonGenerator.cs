using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AsmInstructionSetGenerator.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace AsmInstructionSetGenerator
{
    public class JsonGenerator : IDisposable
    {
        /// <summary>offset between page number and pdf page number</summary>
        private const int PageOffset = 14;
        private const string KeyPageNumberRegex = "page";

        /// <summary>Header regex, format {0} => page number</summary>
        private const string HeaderRegex = "Z80\\sCPU\\nUser\\sManual\\n{0}\\n";
        private static readonly Regex FooterRegex = new ("(UM008011-0816 Z80 Instruction Description)|(Z80 Instruction Set UM008011-0816)");

        private static readonly IEnumerable<int> OpcodesGroupsPages = new List<int> { 70, 98, 123, 144, 172, 187, 204, 242, 261, 280, 294, };

        private static readonly Regex OpcodePageIndexRegex = new ($"^(?:.[^–]*)\\s–\\ssee\\spage\\s(?<{KeyPageNumberRegex}>\\d*)$");
        private static readonly string Z80UserManualPdfPath = $"{Assembly.GetEntryAssembly().GetName().Name}.Resources.AssemblerZ80.pdf";

        private PdfDocument z80UserManualPdf;


        public JsonGenerator()
        {
            LoadPdf();
        }

        public string ExtractInstructionSet()
        {
            IEnumerable<OpcodeGroupModel> result = OpcodesGroupsPages.Select(ReadOpcodesGroupFromPageNumber).ToList();
            return Newtonsoft.Json.JsonConvert.SerializeObject(result);
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


        private OpcodeGroupModel ReadOpcodesGroupFromPageNumber(int pageNumber)
        {
            PdfPage page = z80UserManualPdf.GetPage(pageNumber + PageOffset);

            // get text withouth header
            string text = PdfTextExtractor.GetTextFromPage(page, new LocationTextExtractionStrategy());
            text = RemoveHeaderAndFooter(text, pageNumber);

            IEnumerable<string> textLines = text.Split('\n');

            // resultmodel
            OpcodeGroupModel result = new ()
            {
                GroupName = textLines.First(),
            };

            IEnumerable<int> opcodesPages = textLines.Where(item => OpcodePageIndexRegex.IsMatch(item))
                .Select(item => int.Parse(OpcodePageIndexRegex.Match(item).Groups[KeyPageNumberRegex].Value));

            // extract opcodes from current page + page numbers
            result.Opcodes = opcodesPages.Zip(opcodesPages.Skip(1), (current, next) => ExtractOpcodeInfo(current, next - current)).ToList();

            return result;
        }


        private OpcodeInfoModel ExtractOpcodeInfo(int startPageNumber, int pages)
        {
            OpcodeInfoModel opcodeInfoModel = new ();

            string text = string.Empty;
            for (int i = startPageNumber; i < startPageNumber + pages; i++)
            {
                PdfPage page = z80UserManualPdf.GetPage(i + PageOffset);

                string pageText = PdfTextExtractor.GetTextFromPage(page, new LocationTextExtractionStrategy());
                pageText = RemoveHeaderAndFooter(pageText, i);

                text += $"{pageText}{Environment.NewLine}";
            }

            opcodeInfoModel.Name = text.Substring(0, text.IndexOf('\n'));
            opcodeInfoModel.Operation = GetTextBetweenSections(text, "Operation", "Op Code");
            opcodeInfoModel.Opcode = GetTextBetweenSections(text, "Op Code", "Operands");
            opcodeInfoModel.Operands = GetTextBetweenSections(text, "Operands", "Description");
            opcodeInfoModel.Description = GetTextBetweenSections(text, "Description", "Condition Bits Affected");
            opcodeInfoModel.ConditionBitsAffected = GetTextBetweenSections(text, "Condition Bits Affected", "Example");

            int indexOfExample = text.IndexOf("Example");
            opcodeInfoModel.Example = indexOfExample > 0 ? text.Substring(indexOfExample) : null;

            return opcodeInfoModel;
        }





        #region [IDisposable]

        private bool disposedValue;
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


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion [IDisposable]
    }
}
