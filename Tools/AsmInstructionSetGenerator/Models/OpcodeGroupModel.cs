using System.Collections.Generic;

namespace AsmInstructionSetGenerator.Models
{
    public class OpcodeGroupModel
    {
        public string GroupName { get; set; }

        public IEnumerable<OpcodeInfoModel> Opcodes { get; set; }
    }

}