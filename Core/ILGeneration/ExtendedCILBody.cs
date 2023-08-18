using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.Utils.Ext;
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

		/// <summary>
		/// Returns true if the provided instruction is any of the <c>br*</c> instructions, conditional or not. Note that this <strong>excludes</strong> <c>jmp</c>.
		/// (To distant future Xan: Reminder that C# JMP is not x86 JMP. That's what BR is.)
		/// </summary>
		/// <param name="instruction"></param>
		/// <returns></returns>
		public static bool IsAnyJump(this Instruction instruction) => instruction.OpCode.OperandType == OperandType.InlineBrTarget || instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget;

		/// <summary>
		/// Returns whether or not the provided instruction is a <c>nop</c> designed as the destination for a branch instruction.
		/// <para/>
		/// This only returns true for members created with <see cref="ILTools.NewBrDest(CilBody)"/>.
		/// </summary>
		/// <param name="instruction"></param>
		/// <returns></returns>
		public static bool IsBrDestNop(this Instruction instruction) => instruction.OpCode == OpCodes.Nop && instruction.Operand is CilBody;

		/// <summary>
		/// Looks for jumps to <see cref="OpCodes.Nop"/>, and replaces those jumps such that they instead jump to the instruction immediately following it.
		/// It will then remove the nops from the IL.
		/// <para/>
		/// <strong>IMPORTANT IMPLEMENTATION NOTE:</strong> This looks for nops where <strong>their operand is the method body.</strong> This is how it differentiates
		/// between a jump destination nop and an ordinary nop. See <see cref="ILTools.NewBrDest(CilBody)"/>.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="raiseErrorOnStrays">If true, any nops left behind that classify as jump targets (created with <see cref="ILTools.NewBrDest(CilBody)"/>) will raise an exception warning you of their presence.</param>
		public static void OptimizeNopJumps(this CilBody body, bool raiseErrorOnStrays = true) {
			Instruction[] jumps = body.Instructions.Where(instruction => instruction.IsAnyJump()).ToArray();
			List<Instruction> allDests = body.Instructions.Where(instruction => instruction.IsBrDestNop()).ToList();
			List<int> allDestsInstructionIndices = allDests.Select(instruction => body.Instructions.IndexOf(instruction)).ToList();

			List<Instruction> garbage = new List<Instruction>();
			foreach (Instruction jump in jumps) { 
				// Below: Use a while loop instead of an if statement.
				// This addresses an edge case where a jump destination immediately follows another.
				// Without the while loop, it would leave some branches without a destination as the instruction gets deleted.
				while (jump.Operand is Instruction jumpDest && jumpDest.IsBrDestNop()) {
					garbage.Add(jumpDest);
					if (body.Instructions.TryGetElementAfter(jump, out Instruction nextDest)) {
						jump.Operand = nextDest;
					} else {
						// There's nothing after this nop. This is invalid. This method should only be called when the function is done, which means
						// that it MUST end in either a ret or a throw.
						throw new InvalidOperationException($"When searching for a jump destination, {nameof(OptimizeNopJumps)} hit the end of the method body. This should be impossible, unless the method is incorrectly written or otherwise incomplete.");
					}
				}
			}

			// Now I can clear out the garbage ones.
			foreach (Instruction jumpDest in garbage) {
				body.Instructions.Remove(jumpDest);
				int index = allDests.IndexOf(jumpDest);
				allDests.RemoveAt(index);
				allDestsInstructionIndices.RemoveAt(index);
			}

			// Now check for strays:
			if (allDests.Count > 0) {
				if (raiseErrorOnStrays) {
					string error = "One or more stray jump destinations were found! Array indices: ";
					foreach (int index in allDestsInstructionIndices) {
						error += $"Instructions[{index}], ";
					}
					throw new InvalidOperationException(error);
				} else {
					foreach (Instruction jump in allDests) {
						jump.Operand = null; // Just clear the operand so that the system doesn't fail to generate code.
					}
				}
			}
		}

		/// <summary>
		/// In the provided order, this calls:
		/// <list type="number">
		/// <item><see cref="BakeDefRefsDown(CilBody)"/></item>
		/// <item><see cref="OptimizeNopJumps(CilBody)"/></item>
		/// <item><see cref="CilBody.OptimizeBranches()"/></item>
		/// <item><see cref="CilBody.OptimizeMacros()"/></item>
		/// <item><see cref="CilBody.UpdateInstructionOffsets()"/></item>
		/// </list>
		/// </summary>
		/// <param name="body"></param>
		public static void FinalizeMethodBody(this CilBody body) {
			body.BakeDefRefsDown();
			body.OptimizeNopJumps();
			body.OptimizeBranches();
			body.OptimizeMacros();
			body.UpdateInstructionOffsets();
		}

	}
}
