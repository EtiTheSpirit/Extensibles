using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ILGeneration {

	/// <summary>
	/// Methods to improve the behavior of CIL bodies.
	/// </summary>
	public static class ExtendedCILBody {

		/// <summary>
		/// Takes all instructions whose operands are <see cref="IHasTypeDefOrRef"/> or <see cref="IMemberDefAndRef"/>, and resolves their references.
		/// </summary>
		public static void BakeDefRefsDown(this CilBody body) {
			foreach (Instruction i in body.Instructions) {
				if (i.Operand is IHasTypeDefOrRef df) {
					i.Operand = df.Reference;
				} else if (i.Operand is IMemberDefAndRef dr) {
					i.Operand = dr.Reference;
				}
			}
		}

	}
}
