using System;
using System.IO;
using System.Threading.Tasks;

namespace ZxBasicInstructionSetGenerator
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            Directory.CreateDirectory("output");

            using JsonGenerator jsonGenerator = new ();
            string json = await jsonGenerator.ExtractInstructionSet();
            File.WriteAllText("output/zxbasic-keywords.json", json);
        }
    }
}
