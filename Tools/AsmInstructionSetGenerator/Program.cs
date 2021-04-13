using System.IO;

namespace AsmInstructionSetGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Directory.CreateDirectory("output");

            using JsonGenerator jsonGenerator = new ();
            string json = jsonGenerator.ExtractInstructionSet();
            File.WriteAllText("output/z80asm-keywords.json", json);
        }
    }
}
