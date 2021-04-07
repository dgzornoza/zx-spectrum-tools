using System.IO;

namespace AsmInstructionSetGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Directory.CreateDirectory("output");

            string json = new JsonGenerator().ExtractInstructionSet();
            File.WriteAllText("output/z80asm-keywords.json", json);
        }
    }
}
