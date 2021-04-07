using System.Text.Json.Serialization;

namespace AsmInstructionSetGenerator.Models
{
    /// <summary>
    /// Model for define Assembly opcode info.
    /// </summary>
    public class OpcodeInfoModel
    {
        public string GroupName { get; set; }

        public string Name { get; set; }

        public string Operation { get; set; }

        public string Opcode { get; set; }

        public string Operands { get; set; }

        public string ConditionBitsAffected { get; set; }

        public string Description { get; set; }

        public string Example { get; set; }
    }
}
