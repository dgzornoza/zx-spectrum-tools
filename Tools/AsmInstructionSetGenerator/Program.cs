using System.IO;

namespace AsmInstructionSetGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string json = new JsonGenerator().ExtractInstructionSet();
            File.WriteAllText("z80asm-keywords.json", json);
        }
    }
}
