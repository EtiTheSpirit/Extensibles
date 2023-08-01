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

		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a getter for the mirror property that calls the getter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="original"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static MethodDefUser CreateGetter(PropertyDefUser mirror, PropertyDef original, PropertyDefUser originalRefProperty) {
			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(original.PropertySig.RetType), original.GetMethod.Attributes);
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(original.GetMethod));                // (original prop name)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = getter;
			return mtd;
		}


		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a setter for the mirror property that calls the setter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="original"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static MethodDefUser CreateSetter(PropertyDefUser mirror, PropertyDef original, PropertyDefUser originalRefProperty) {
			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(original.PropertySig.RetType), original.SetMethod.Attributes);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());								// value
			setter.Instructions.Add(OpCodes.Call.ToInstruction(original.SetMethod));                // set (original prop name)
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = setter;
			return mtd;
		}

		/// <summary>
		/// For use when creating the "Original" property of mirror types, this generates the IL of the getter to get the reference from the weak field.
		/// </summary>
		/// <param name="originalRefProp"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <returns></returns>
		public static MethodDefUser CreateOriginalReferencer(PropertyDefUser originalRefProp, FieldDefUser originalWeakRefFld, TypeDef weakRefType) {
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc_0.ToInstruction());											// store in local slot 0
			getter.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(0));									// load slot 0 as by-reference value
			getter.Instructions.Add(OpCodes.Call.ToInstruction(weakRefType.FindMethod("TryGetTarget")));   // Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public);
			mDef.Body = getter;
			mDef.ReturnType = originalRefProp.PropertySig.RetType;
			mDef.MethodSig.HasThis = true;

			return mDef;
		}

	}
}
