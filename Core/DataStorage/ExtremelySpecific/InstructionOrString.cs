using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.ExtremelySpecific {
	public struct InstructionOrString {

		public Instruction instruction;

		public string text;

		public Instruction ToInstruction() {
			if (instruction != null) return instruction;
			return new Instruction(OpCodes.Ldstr, text);
		}

		public static implicit operator InstructionOrString(Instruction instruction) => new InstructionOrString { instruction = instruction };
		public static implicit operator InstructionOrString(string text) => new InstructionOrString { text = text };

	}
}
