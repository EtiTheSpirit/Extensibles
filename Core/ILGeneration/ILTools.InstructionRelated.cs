using dnlib.DotNet.Emit;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Reflection;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.ExtremelySpecific;
using System.Diagnostics;

namespace HookGenExtender.Core.ILGeneration {

	/// <summary>
	/// IL that assists in generating objects and references.
	/// </summary>
	public static partial class ILTools {

		/// <summary>
		/// Emits an instruction.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="opcode"></param>
		/// <param name="operand"></param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Instruction Emit(this CilBody body, OpCode opcode, object operand = null) {
			Instruction instruction = new Instruction(opcode, operand);
			body.Instructions.Add(instruction);
			return instruction;
		}

		/// <summary>
		/// Emits <c>ldarg_0</c> (<see langword="this"/>, in instance methods) in a method, for convenience.
		/// </summary>
		/// <param name="body"></param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Instruction EmitThis(this CilBody body) => body.Emit(OpCodes.Ldarg_0);

		/// <summary>
		/// Calls the provided constructor.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="constructor"></param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Instruction EmitNew(this CilBody body, MemberRef constructor) => body.Emit(OpCodes.Newobj, constructor);

		/// <summary>
		/// Emits <see cref="OpCodes.Ret"/>
		/// </summary>
		/// <param name="body"></param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Instruction EmitRetn(this CilBody body) => body.Emit(OpCodes.Ret);

		/// <summary>
		/// Emits the code equivalent to <see langword="typeof"/>(<paramref name="type"/>).
		/// </summary>
		/// <param name="body"></param>
		/// <param name="type"></param>
		public static void EmitTypeof(this CilBody body, ExtensiblesGenerator main, ITypeDefOrRef type) {
			body.Emit(OpCodes.Ldtoken, type);
			body.Emit(OpCodes.Call, main.Shared.GetTypeFromHandle);
		}

		/// <summary>
		/// Emits the code equivalent to <see langword="methodof"/>(<paramref name="method"/>).<br/>
		/// Note that there is no such thing as "<see langword="methodof"/>", this is made up as a way of 
		/// representing "returns a <see cref="MethodInfo"/> from a method signature", much like how
		/// <see langword="typeof"/> returns a <see cref="Type"/> from a type signature.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		public static void EmitMethodof(this CilBody body, ExtensiblesGenerator main, MemberRef method) {
			body.Emit(OpCodes.Ldtoken, method);
			body.Emit(OpCodes.Call, main.Shared.GetMethodFromHandle);
		}

		/// <summary>
		/// Emits instructions to throw an <see cref="InvalidOperationException"/> with the provided message.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		public static void EmitInvalidOpException(this CilBody body, ExtensiblesGenerator main, string message) {
			body.Emit(OpCodes.Ldstr, message);
			body.Emit(OpCodes.Newobj, main.Shared.InvalidOperationExceptionCtor);
			body.Emit(OpCodes.Throw);
		}

		/// <summary>
		/// Emits instructions to call <see cref="UnityEngine.Debug.Log"/> with the provided string message.
		/// <para/>
		/// The message can be <see langword="null"/> to use the latest string on the stack instead.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="message">The message to display, or <see langword="null"/> to use the current string on the stack.</param>
		public static void EmitUnityDbgLog(this CilBody body, ExtensiblesGenerator main, string message) {
			if (message != null) body.Emit(OpCodes.Ldstr, message);
			body.Emit(OpCodes.Call, main.Shared.UnityDebugLog);
		}

		/// <summary>
		/// Provide one or more actions that create instructions to load parts of a string. This will follow them with a call to <see cref="string.Concat"/>.
		/// 
		/// All provided instructions *must* result in strings.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="allAreGuaranteedStrings">If true, the system assumes you know <em>for a fact</em> that everything on the stack provided by <paramref name="createParts"/> is unquestionably a string. Setting this to true when this assertion is false will result in invalid IL and will crash the program.</param>
		/// <param name="createParts"></param>
		public static void EmitStringFormat(this CilBody body, ExtensiblesGenerator main, bool allAreGuaranteedStrings, params Action<CilBody, ExtensiblesGenerator>[] createParts) {
			int amount = createParts.Length;
			if (amount == 0) {
				body.Emit(OpCodes.Ldstr, string.Empty);
				return;
			}
			if (amount <= 4) {
				foreach (var act in createParts) act.Invoke(body, main);
				int index = amount - 1;
				if (allAreGuaranteedStrings) {
					MemberRef[] array = allAreGuaranteedStrings ? main.Shared.StringConcatStrings : main.Shared.StringConcatObjects;
					body.Emit(OpCodes.Call, array[index]);
				}
			} else {
				// This one gets more complicated.
				if (allAreGuaranteedStrings) {
					body.Emit(OpCodes.Newarr, main.CorLibTypeSig<string>());
				} else {
					body.Emit(OpCodes.Newarr, main.CorLibTypeSig<object>());
				}
				body.Emit(OpCodes.Dup);
				for (int i = 0; i < amount; i++) {
					Action<CilBody, ExtensiblesGenerator> emitInstructions = createParts[i];

					body.EmitLdc_I4(i);
					emitInstructions.Invoke(body, main);
					body.Emit(OpCodes.Stelem);

					if (i < amount - 1) {
						body.Emit(OpCodes.Dup);
					}
				}
				body.Emit(OpCodes.Call, allAreGuaranteedStrings ? main.Shared.StringConcatStrings[4] : main.Shared.StringConcatObjects[4]);
			}
		}

		/// <summary>
		/// Calls <see cref="object.ToString()"/> on the latest element of the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		public static void EmitTostring(this CilBody body, ExtensiblesGenerator main) {
			body.Emit(OpCodes.Callvirt, main.Shared.ToStringRef);
		}

		/// <summary>
		/// Emits code that loads the provided field. It will automatically emit the proper opcode(s) based on whether or not the field is static.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="memberRef"></param>
		/// <returns></returns>
		public static void EmitLdfldAuto(this CilBody body, FieldDefAndRef field, bool byReference = false) {
			OpCode load;
			if (field.Definition.IsStatic) {
				load = byReference ? OpCodes.Ldsflda : OpCodes.Ldsfld;
			} else {
				load = byReference ? OpCodes.Ldfld : OpCodes.Ldflda;
				body.Emit(OpCodes.Ldarg_0);
			}
			body.Emit(load, field.Reference);
		}

		/// <summary>
		/// Emits code that loads the provided field. It will automatically emit the proper opcode(s) based on whether or not the field is static.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="memberRef"></param>
		/// <returns></returns>
		public static void EmitStfldAuto(this CilBody body, FieldDefAndRef field) {
			OpCode store;
			if (field.Definition.IsStatic) {
				store = OpCodes.Stsfld;
			} else {
				store = OpCodes.Stfld;
				body.Emit(OpCodes.Ldarg_0);
			}
			body.Emit(store, field.Reference);
		}

		/// <summary>
		/// Emits either <see cref="OpCodes.Call"/> or <see cref="OpCodes.Callvirt"/> depending on whether or not the method is static.
		/// If the method is not static, <see cref="OpCodes.Callvirt"/> is emitted, unless <paramref name="noVTable"/> is <see langword="true"/> 
		/// from which <see cref="OpCodes.Call"/> is emitted instead.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		/// <param name="noVTable"></param>
		/// <returns></returns>
		public static Instruction EmitCallAuto(this CilBody body, MethodDefAndRef method, bool noVTable = false) {
			bool canCallVirt = !method.Definition.IsStatic && !noVTable;
			if (canCallVirt) return body.Emit(OpCodes.Callvirt, method.Reference);
			return body.Emit(OpCodes.Call, method.Reference);
		}

		/// <summary>
		/// For use in ldarg, which (for some reason) wants this.
		/// </summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Parameter ParameterIndex(int arg) => new Parameter(arg);

		/// <summary>
		/// Returns the version of Ldarg best suited for the input argument index. This also allows inputting the argument index as an int32.
		/// </summary>
		/// <param name="argN"></param>
		/// <returns></returns>
		public static Instruction EmitLdarg(this CilBody body, int argN, bool asReference = false) {
			// If the value is greater than 3 (or its a reference), but less than the byte max value, use _S.
			if ((argN > 3 || asReference) && argN <= byte.MaxValue) return new Instruction(asReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, ParameterIndex(argN));
			if (asReference) return new Instruction(OpCodes.Ldarga, ParameterIndex(argN));
			Instruction result = argN switch {
				0 => OpCodes.Ldarg_0.ToInstruction(),
				1 => OpCodes.Ldarg_1.ToInstruction(),
				2 => OpCodes.Ldarg_2.ToInstruction(),
				3 => OpCodes.Ldarg_3.ToInstruction(),
				_ => new Instruction(OpCodes.Ldarg, ParameterIndex(argN))
			};
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Returns the version of Ldc_I4 best suited for the input integer value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction EmitLdc_I4(this CilBody body, int value) {
			// If the value is within the sbyte range, and it's not within the range of [-1, 8], use _S.
			if (value >= sbyte.MinValue && value <= sbyte.MaxValue && (value < -1 || value > 8)) return new Instruction(OpCodes.Ldc_I4_S, (sbyte)value);
			Instruction result = value switch {
				-1 => OpCodes.Ldc_I4_M1.ToInstruction(),
				0 => OpCodes.Ldc_I4_0.ToInstruction(),
				1 => OpCodes.Ldc_I4_1.ToInstruction(),
				2 => OpCodes.Ldc_I4_2.ToInstruction(),
				3 => OpCodes.Ldc_I4_3.ToInstruction(),
				4 => OpCodes.Ldc_I4_4.ToInstruction(),
				5 => OpCodes.Ldc_I4_5.ToInstruction(),
				6 => OpCodes.Ldc_I4_6.ToInstruction(),
				7 => OpCodes.Ldc_I4_7.ToInstruction(),
				8 => OpCodes.Ldc_I4_8.ToInstruction(),
				_ => OpCodes.Ldc_I4.ToInstruction(value)
			};
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Loads a local onto the stack, choosing the best instruction to use for this task.
		/// </summary>
		/// <param name="local"></param>
		/// <param name="asReference"></param>
		/// <returns></returns>
		public static Instruction EmitLdloc(this CilBody body, Local local, bool asReference = false) {
			if (!body.Variables.Contains(local)) {
				body.Variables.Add(local);
			}
			body.OrderLocals();
			Instruction result;
			if (asReference) {
				OpCode ldloca = local.Index <= byte.MaxValue ? OpCodes.Ldloca_S : OpCodes.Ldloca;
				result = ldloca.ToInstruction(local);
			} else {
				if (local.Index > 3 && local.Index <= byte.MaxValue) {
					result = OpCodes.Ldloc_S.ToInstruction(local);
				} else {
					result = local.Index switch {
						0 => OpCodes.Ldloc_0.ToInstruction(),
						1 => OpCodes.Ldloc_1.ToInstruction(),
						2 => OpCodes.Ldloc_2.ToInstruction(),
						3 => OpCodes.Ldloc_3.ToInstruction(),
						_ => OpCodes.Ldloc.ToInstruction(local)
					};
				}
			}
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Stores a local from the stack, choosing the best instruction to use for this task.
		/// </summary>
		/// <param name="local"></param>
		/// <param name="asReference"></param>
		/// <returns></returns>
		public static Instruction EmitStloc(this CilBody body, Local local) {
			if (!body.Variables.Contains(local)) {
				body.Variables.Add(local);
			}
			body.OrderLocals();
			Instruction result;
			if (local.Index > 3 && local.Index <= byte.MaxValue) {
				result = OpCodes.Stloc_S.ToInstruction(local);
			} else {
				result = local.Index switch {
					0 => OpCodes.Stloc_0.ToInstruction(),
					1 => OpCodes.Stloc_1.ToInstruction(),
					2 => OpCodes.Stloc_2.ToInstruction(),
					3 => OpCodes.Stloc_3.ToInstruction(),
					_ => OpCodes.Stloc.ToInstruction(local)
				};
			}
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Emits a stloc followed by a ldloc of the same local.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="local"></param>
		/// <returns></returns>
		public static (Instruction, Instruction) EmitStoreThenLoad(this CilBody body, Local local) {
			Instruction st = body.EmitStloc(local);
			Instruction ld = body.EmitLdloc(local);
			return (st, ld);
		}

		/// <summary>
		/// Appends the provided locals to the method body. This will also apply the appropriate local index.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="locals"></param>
		public static void AppendLocals(this CilBody body, params Local[] locals) {
			int nextID = body.Variables.Count;
			for (int i = 0; i < nextID; i++) {
				Local local = body.Variables[i];
				local.SetIndex(i);
			}
			for (int i = 0; i < locals.Length; i++) {
				Local local = locals[i];
				if (body.Variables.Contains(local)) throw new ArgumentException($"The local {local} is already a part of this method.");
				local.SetIndex(i);
				body.Variables.Add(local);
			}
		}

		/// <summary>
		/// Replaces existing locals of the method body with the provided locals. This will also apply the appropriate local index.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="locals"></param>
		public static void OverwriteLocalsWith(this CilBody body, params Local[] locals) {
			body.Variables.Clear();
			AppendLocals(body, locals);
		}

		/// <summary>
		/// Assigns an appropriate index to all locals.
		/// </summary>
		/// <param name="body"></param>
		public static void OrderLocals(this CilBody body) {
			int localCount = body.Variables.Count;
			for (int i = 0; i < localCount; i++) {
				Local local = body.Variables[i];
				local.SetIndex(i);
			}
		}

		/// <summary>
		/// Optimizes the entire method body and appends a <see cref="OpCodes.Ret"/> onto the end if needed.
		/// </summary>
		/// <param name="body"></param>
		public static void FinalizeBody(this CilBody body) {
			body.OptimizeBranches();
			body.OptimizeMacros();
			body.UpdateInstructionOffsets();
			body.BakeDefRefsDown();
		}

		#region Internal Garbage

		internal static void SetIndex(this Local local, int newIndex) {
			if (_localIndexFld == null) {
				_localIndexFld = typeof(Local).GetField("index", BindingFlags.NonPublic | BindingFlags.Instance);
			}
			_localIndexFld.SetValue(local, newIndex);
		}

		private static FieldInfo _localIndexFld = null;

		#endregion
	}
}
