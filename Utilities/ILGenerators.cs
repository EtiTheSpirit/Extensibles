using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace HookGenExtender.Utilities {
	public static class ILGenerators {

		#region Property Get/Set Mirrors

		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a getter for the mirror property that calls the getter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static void CreateGetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.mirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(mirror.PropertySig.RetType), attrs);
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(mtd));								// (original prop name)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = getter;
			mirror.GetMethod = mtd;
		}


		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a setter for the mirror property that calls the setter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="originalSetter"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static void CreateSetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.mirrorsAreVirtual) attrs |= MethodAttributes.Virtual;
			
			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirror.PropertySig.RetType), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());								// value
			setter.Instructions.Add(OpCodes.Call.ToInstruction(mtd));				                // set (original prop name here)
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = setter;
			mirror.SetMethod = mtd;
		}

		#endregion

		#region Field Get/Set Mirrors
		/// <summary>
		/// Provided with a mirror property and its original field, this will create a getter for the mirror property that references the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="field"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static MethodDefUser CreateGetterToField(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, MemberRef field, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			if (settings.mirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));							// (original field here)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = getter;
			return mtd;
		}


		/// <summary>
		/// Provided with a mirror property and its original field, this will create a setter for the mirror property that sets the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="field"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static MethodDefUser CreateSetterToField(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, MemberRef field, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			if (settings.mirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());                               // value
			setter.Instructions.Add(OpCodes.Stfld.ToInstruction(field));							// set (original field name here)
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = setter;
			return mtd;
		}

		#endregion

		#region Method Mirrors

		public static MethodDefUser GenerateMethodMirror(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			// This is where things get complicated.
			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.mirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			TypeSig[] paramTypes = original.Parameters
				.Where(paramDef => !paramDef.IsReturnTypeParameter)
				.Select(paramDef => mirrorGenerator.cache.Import(paramDef.Type))
				.ToArray();
			MethodDefUser mirror = new MethodDefUser(original.Name, MethodSig.CreateInstance(mirrorGenerator.cache.Import(original.ReturnType), paramTypes));
			mirror.Attributes = attrs;

			CilBody cil = new CilBody();
			// The system needs to simply invoke the original method.
			// The hurdle is making all the args work.
			// Thankfully this can be achieved like so:
			// Start by pushing the reference to the original, as "this" for the receiver.
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());									// this.
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));		// Original
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(OptimizedLdarg(i + 1));										// Aaaalll the args
			}
			OpCode callCode;
			if (original.IsVirtual || original.IsAbstract) {
				callCode = OpCodes.Callvirt;
			} else {
				callCode = OpCodes.Call;
			}
			cil.Instructions.Add(callCode.ToInstruction(mirror));
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
			mirror.Body = cil;

			return mirror;
		}

		private static Instruction OptimizedLdarg(int argN) {
			return argN switch {
				0 => OpCodes.Ldarg_0.ToInstruction(),
				1 => OpCodes.Ldarg_1.ToInstruction(),
				2 => OpCodes.Ldarg_2.ToInstruction(),
				3 => OpCodes.Ldarg_3.ToInstruction(),
				_ => new Instruction(OpCodes.Ldarg, new LazyLocal((ushort)argN)),
			};
		}

		#endregion

		#region The field and property storing original

		/// <summary>
		/// For use when creating the "Original" property of mirror types, this generates the IL of the getter to get the reference from the weak field.
		/// </summary>
		/// <param name="originalRefProp"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <returns></returns>
		public static MemberRefUser CreateOriginalReferencer(MirrorGenerator mirrorGenerator, PropertyDefUser originalRefProp, FieldDefUser originalWeakRefFld, GenericInstSig weakRef, GenericInstMethodSig tryGetTarget) {
			TypeRef weakReference = weakRef.TryGetTypeRef();
			ModuleDef module = weakRef.Module;
			MemberRefUser tryGetTargetMember = new MemberRefUser(module, "TryGetTarget", MethodSig.CreateInstanceGeneric(1, module.CorLibTypes.Boolean, originalWeakRefFld.FieldType), weakReference);
			MethodSpecUser tryGetTargetSpec = new MethodSpecUser(tryGetTargetMember, tryGetTarget);


			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc_0.ToInstruction());                                           // store in local slot 0
																												// they forgot the ushort overload...
			getter.Instructions.Add(new Instruction(OpCodes.Ldloca, new LazyLocal(0)));		// load slot 0 as by-reference value
			getter.Instructions.Add(OpCodes.Call.ToInstruction(tryGetTargetSpec));								// Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public | MethodAttributes.CompilerControlled);
			mDef.Body = getter;
			mDef.ReturnType = originalRefProp.PropertySig.RetType;
			mDef.MethodSig.HasThis = true;

			originalRefProp.GetMethod = mDef;
			return tryGetTargetMember;
		}

		#endregion


	}
}
