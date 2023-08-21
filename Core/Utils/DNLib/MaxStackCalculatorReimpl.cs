using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.Utils.Debugging {
	
	/// <summary>
	/// A reimplementation (read: copy/paste) of dnlib's stack calculator. This variation is designed to raise exceptions <em>before</em>
	/// spending the time to finish all the other code and only throw the error after context is long gone.
	/// <para/>
	/// This will make debugging faulty IL dramatically easier.
	/// </summary>
	public class MaxStackCalculatorReimpl {
		/// <summary>
		/// Gets max stack value
		/// </summary>
		/// <param name="instructions">All instructions</param>
		/// <param name="exceptionHandlers">All exception handlers</param>
		/// <returns>Max stack value</returns>
		// Token: 0x06001465 RID: 5221 RVA: 0x00043234 File Offset: 0x00041434
		public static uint GetMaxStack(IList<Instruction> instructions, IList<ExceptionHandler> exceptionHandlers) {
			uint maxStack;
			new MaxStackCalculatorReimpl(instructions, exceptionHandlers).Calculate(out maxStack);
			return maxStack;
		}

		/// <summary>
		/// Gets max stack value
		/// </summary>
		/// <param name="instructions">All instructions</param>
		/// <param name="exceptionHandlers">All exception handlers</param>
		/// <param name="maxStack">Updated with max stack value</param>
		/// <returns><c>true</c> if no errors were detected, <c>false</c> otherwise</returns>
		// Token: 0x06001466 RID: 5222 RVA: 0x0004325C File Offset: 0x0004145C
		public static bool GetMaxStack(IList<Instruction> instructions, IList<ExceptionHandler> exceptionHandlers, out uint maxStack) {
			return new MaxStackCalculatorReimpl(instructions, exceptionHandlers).Calculate(out maxStack);
		}

		// Token: 0x06001467 RID: 5223 RVA: 0x00043279 File Offset: 0x00041479
		internal static MaxStackCalculatorReimpl Create() {
			return new MaxStackCalculatorReimpl();
		}

		// Token: 0x06001468 RID: 5224 RVA: 0x00043281 File Offset: 0x00041481
		private MaxStackCalculatorReimpl() {
			instructions = null;
			exceptionHandlers = null;
			stackHeights = new Dictionary<Instruction, int>();
			hasError = false;
			currentMaxStack = 0;
		}

		// Token: 0x06001469 RID: 5225 RVA: 0x000432AB File Offset: 0x000414AB
		private MaxStackCalculatorReimpl(IList<Instruction> instructions, IList<ExceptionHandler> exceptionHandlers) {
			this.instructions = instructions;
			this.exceptionHandlers = exceptionHandlers;
			stackHeights = new Dictionary<Instruction, int>();
			hasError = false;
			currentMaxStack = 0;
		}

		// Token: 0x0600146A RID: 5226 RVA: 0x000432D5 File Offset: 0x000414D5
		internal void Reset(IList<Instruction> instructions, IList<ExceptionHandler> exceptionHandlers) {
			this.instructions = instructions;
			this.exceptionHandlers = exceptionHandlers;
			stackHeights.Clear();
			hasError = false;
			currentMaxStack = 0;
		}

		// Token: 0x0600146B RID: 5227 RVA: 0x00043300 File Offset: 0x00041500
		internal bool Calculate(out uint maxStack) {
			IList<ExceptionHandler> exceptionHandlers = this.exceptionHandlers;
			Dictionary<Instruction, int> stackHeights = this.stackHeights;
			for (int i = 0; i < exceptionHandlers.Count; i++) {
				ExceptionHandler eh = exceptionHandlers[i];
				if (eh != null) {
					Instruction instr = eh.TryStart;
					if (instr != null) {
						stackHeights[instr] = 0;
					}
					instr = eh.FilterStart;
					if (instr != null) {
						stackHeights[instr] = 1;
						currentMaxStack = 1;
					}
					instr = eh.HandlerStart;
					if (instr != null) {
						if (eh.IsCatch || eh.IsFilter) {
							stackHeights[instr] = 1;
							currentMaxStack = 1;
						} else {
							stackHeights[instr] = 0;
						}
					}
				}
			}
			int stack = 0;
			bool resetStack = false;
			IList<Instruction> instructions = this.instructions;
			for (int j = 0; j < instructions.Count; j++) {
				Instruction currentInstruction = instructions[j];
				if (currentInstruction != null) {
					if (resetStack) {
						stackHeights.TryGetValue(currentInstruction, out stack);
						resetStack = false;
					}
					stack = WriteStack(currentInstruction, stack);
					OpCode opCode = currentInstruction.OpCode;
					Code code = opCode.Code;
					if (code == Code.Jmp) {
						if (stack != 0) {
							throw new InvalidOperationException($"Illegal attempt to execute jmp while the stack wasn't empty. This instruction requires an empty stack. Instruction: {currentInstruction} (Instruction index: {j})");
						}
					} else {
						currentInstruction.CalculateStackUsage(out int pushes, out int pops);
						if (pops == -1) {
							stack = 0;
						} else {
							stack -= pops;
							if (stack < 0) {
								bool isCall = opCode.FlowControl == FlowControl.Call;
								if (isCall) {
									throw new InvalidOperationException($"Not enough arguments are on the stack to complete this method call; it is missing {Math.Abs(stack)} argument(s). Call: {currentInstruction} (Instruction index: {j})");
								} else {
									throw new InvalidOperationException($"Attempt to pop something off of the stack (either via Pop or another instruction) failed because the stack was empty or otherwise didn't have enough elements to pop (missing {Math.Abs(stack)} argument(s)). Instruction: {currentInstruction} (Instruction index: {j})");
								}
								//stack = 0;
							}
							stack += pushes;
						}
					}
					if (stack < 0) {
						throw new InvalidOperationException($"The current stack location is negative. Last checked instruction: {currentInstruction} (Instruction index: {j})");
						// stack = 0;
					}
					switch (opCode.FlowControl) {
						case FlowControl.Branch:
							WriteStack(currentInstruction.Operand as Instruction, stack);
							resetStack = true;
							break;
						case FlowControl.Call: {
								if (code == Code.Jmp) {
									resetStack = true;
								}
								break;
							}
						case FlowControl.Cond_Branch: {
								if (code == Code.Switch) {
									IList<Instruction> targets = currentInstruction.Operand as IList<Instruction>;
									if (targets != null) {
										for (int k = 0; k < targets.Count; k++) {
											WriteStack(targets[k], stack);
										}
									}
								} else {
									WriteStack(currentInstruction.Operand as Instruction, stack);
								}
								break;
							}
						case FlowControl.Return:
						case FlowControl.Throw:
							resetStack = true;
							break;
					}
				}
			}
			maxStack = (uint)currentMaxStack;
			return !hasError;
		}

		// Token: 0x0600146C RID: 5228 RVA: 0x000435D8 File Offset: 0x000417D8
		private int WriteStack(Instruction instr, int stack) {
			int result;
			if (instr == null) {
				hasError = true;
				result = stack;
			} else {
				bool hasStackHeight = stackHeights.TryGetValue(instr, out int stackHeightOfInstr);
				if (hasStackHeight) {
					if (stack != stackHeightOfInstr) {
						hasError = true;
					}
					result = stackHeightOfInstr;
				} else {
					stackHeights[instr] = stack;
					if (stack > currentMaxStack) {
						currentMaxStack = stack;
					}
					result = stack;
				}
			}
			return result;
		}

		// Token: 0x04000669 RID: 1641
		private IList<Instruction> instructions;

		// Token: 0x0400066A RID: 1642
		private IList<ExceptionHandler> exceptionHandlers;

		// Token: 0x0400066B RID: 1643
		private readonly Dictionary<Instruction, int> stackHeights;

		// Token: 0x0400066C RID: 1644
		private bool hasError;

		// Token: 0x0400066D RID: 1645
		private int currentMaxStack;
	}
}
