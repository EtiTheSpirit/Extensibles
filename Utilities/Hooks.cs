// #define DEBUG_HELPER_ENABLED
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	public static class Hooks {

		
		/// <summary>
		/// For use in ldarg, which (for some reason) wants this.
		/// </summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static Parameter ParameterIndex(int arg) => new Parameter(arg);
		
		/// <summary>
		/// Returns the version of Ldarg best suited for the input argument index. This also allows inputting the argument index as an int32.
		/// </summary>
		/// <param name="argN"></param>
		/// <returns></returns>
		public static Instruction OptimizedLdarg(int argN, bool asReference = false) {
			// If the value is greater than 3 (or its a reference), but less than the byte max value, use _S.
			if ((argN > 3 || asReference) && argN < byte.MaxValue) return new Instruction(asReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, ParameterIndex(argN));
			if (asReference) return new Instruction(OpCodes.Ldarga, ParameterIndex(argN));
			return argN switch {
				0 => OpCodes.Ldarg_0.ToInstruction(),
				1 => OpCodes.Ldarg_1.ToInstruction(),
				2 => OpCodes.Ldarg_2.ToInstruction(),
				3 => OpCodes.Ldarg_3.ToInstruction(),
				_ => new Instruction(OpCodes.Ldarg, ParameterIndex(argN))
			};
		}

		/// <summary>
		/// Returns the version of Ldc_I4 best suited for the input integer value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction OptimizedLdc_I4(int value) {
			// If the value is within the sbyte range, and it's not within the range of [-1, 8], use _S.
			if (value >= sbyte.MinValue && value <= sbyte.MaxValue && (value < -1 || value > 8)) return new Instruction(OpCodes.Ldc_I4_S, (sbyte)value);
			return value switch {
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
		}

		/// <summary>
		/// Given the definition of a member in the original module, this creates a <see cref="MemberRefUser"/> to that member.
		/// </summary>
		/// <param name="original">The original member.</param>
		/// <param name="generator">The mirror generator, for importing types.</param>
		/// <param name="in">The parent type, useful for generics.</param>
		/// <returns></returns>
		/// <exception cref="NotSupportedException"></exception>
		public static MemberRef MakeMemberReference(this IMemberDef original, MirrorGenerator generator, ITypeDefOrRef @in = null, bool import = true) {
			if (original.IsMethodDef) {
				if (import) {
					return generator.cache.Import((MethodDef)original);
				}

				MethodSig signature = ((MethodDef)original).MethodSig;
				ITypeDefOrRef declaringType = @in ?? original.DeclaringType;
				return new MemberRefUser(generator.MirrorModule, original.Name, signature, declaringType);

			} else if (original.IsFieldDef) {
				if (import) {
					return generator.cache.Import((FieldDef)original);
				}

				FieldSig signature = ((FieldDef)original).FieldSig;
				ITypeDefOrRef declaringType = @in ?? original.DeclaringType;
				return new MemberRefUser(generator.MirrorModule, original.Name, signature, declaringType);
			} else if (original.IsPropertyDef) {
				throw new NotSupportedException("To reference properties, explicitly reference their get or set method instead.");
			}
			throw new NotSupportedException("The provided member type is not supported.");
		}

		/// <summary>
		/// Alias to <see cref="Parameter.CreateParamDef"/> that returns the existing or new instance.
		/// </summary>
		/// <param name="param"></param>
		/// <returns></returns>
		public static ParamDef GetOrCreateParamDef(this Parameter param) {
			param.CreateParamDef();
			return param.ParamDef;
		}
	}
}
