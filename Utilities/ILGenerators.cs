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
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;

namespace HookGenExtender.Utilities {
	public static class ILGenerators {


		#region Property Get/Set Mirrors

		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a getter for the mirror property that calls the getter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static void CreateGetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDef orgProp, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MemberRef orgGetter = mirrorGenerator.cache.Import(orgProp.GetMethod);
			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(mirror.PropertySig.RetType), attrs);
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(orgGetter));							// (original prop name)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = getter;
			mtd.IsHideBySig = true;
			mtd.IsSpecialName = true;
			mirror.GetMethod = mtd;
		}


		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a setter for the mirror property that calls the setter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="originalSetter"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static void CreateSetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDef orgProp, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MemberRef orgSetter = mirrorGenerator.cache.Import(orgProp.SetMethod);
			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.Module.CorLibTypes.Void, mirror.PropertySig.RetType), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());                               // value
			setter.Instructions.Add(OpCodes.Call.ToInstruction(orgSetter));			                // set (original prop name here)
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = setter;
			mtd.IsHideBySig = true;
			mtd.IsSpecialName = true;
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
		public static void CreateGetterToField(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, MemberRef field, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));							// (original field here)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = getter;
			mtd.IsHideBySig = true;
			mtd.IsSpecialName = true;
			mirror.GetMethod = mtd;
		}


		/// <summary>
		/// Provided with a mirror property and its original field, this will create a setter for the mirror property that sets the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="field"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static void CreateSetterToField(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, MemberRef field, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.Module.CorLibTypes.Void, mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());                               // value
			setter.Instructions.Add(OpCodes.Stfld.ToInstruction(field));							// set (original field name here)
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = setter;
			mtd.IsHideBySig = true;
			mtd.IsSpecialName = true;
			mirror.SetMethod = mtd;
		}

		#endregion

		#region Method Mirrors

		/// <summary>
		/// Generates a method with an identical signature to its counterpart in the original class, where calling this method will invoke the original.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="original"></param>
		/// <param name="originalRefProperty"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public static MethodDefUser GenerateMethodMirror(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			// This is where things get complicated.
			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MemberRef mbr = mirrorGenerator.cache.Import(original);
			TypeSig[] paramTypes = original.Parameters
				.Skip(1)
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
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                  // this.
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));        // Original
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(OptimizedLdarg(i + 1));                                        // Aaaalll the args
			}
			OpCode callCode;
			if (original.IsVirtual || original.IsAbstract) {
				callCode = OpCodes.Callvirt;
			} else {
				callCode = OpCodes.Call;
			}
			cil.Instructions.Add(callCode.ToInstruction(mbr));
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
			mirror.Body = cil;

			return mirror;
		}

		/// <summary>
		/// Generates a delegate pointing to BepInEx's hook orig_MethodName delegates, allowing the mirror method to be used in BIE patches.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="original"></param>
		/// <param name="originalRefProperty"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public static (MethodDefUser?, FieldDefUser?, FieldDefUser?) TryGenerateBIEOrigCall(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			// This is where things get complicated.

			// In the standard technique, code like this gets generated:
			/*
			 *	public virtual int SomeMethod(bool something) {
			 *		return this.Original.SomeMethod(something);
			 *	}
			 */

			// However for BIE hooks, a delegate to the original method needs to be used instead (otherwise a stack overflow will occur as a hook re-enters itself)
			/*
			 *	public virtual int SomeMethod(bool something) {
			 *		return this.orig_SomeMethod.Invoke(something);
			 *	}
			 * 
			 */

			// The hurdle is setting orig_SomeMethod. The hook generator would have to set that field, then invoke the method, then unset that field.


			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			// Start by declaring the field storing the original delegate.
			// To do this, acquire the hook type and the corresponding delegate.
			string fullName = original.DeclaringType.ReflectionFullName;
			string hookFullName = "On." + fullName;
			TypeDef hookClass = mirrorGenerator.BepInExHooksModule!.Find(hookFullName, true);
			if (hookClass == null) {
				return (null, null, null);
			}
			(TypeDef? bieOrigMethodDef, MethodDef? bieOrigInvoke) = hookClass.TryGetOrigDelegateForMethod(original);
			if (bieOrigMethodDef == null) {
				return (null, null, null);
			}
			TypeRef hookClassRef = mirrorGenerator.cache.Import(hookClass);
			
			TypeRef bieOrigMethod = mirrorGenerator.cache.Import(bieOrigMethodDef);
			TypeSig bieOrigMethodSig = mirrorGenerator.cache.Import(bieOrigMethodDef.ToTypeSig());
			IMethodDefOrRef bieOrigInvokeRef = mirrorGenerator.cache.Import(bieOrigInvoke!);

			// Now create the field
			FieldDefUser delegateOrig = new FieldDefUser($"delegate_orig_{original.Name}", new FieldSig(bieOrigMethodSig), FieldAttributes.Private);

			// And now create the method.
			// Start by duplicating its parameters and creating the method frame.
			MemberRef mbr = mirrorGenerator.cache.Import(original);
			TypeSig[] paramTypes = original.Parameters
				.Skip(1)
				.Where(paramDef => !paramDef.IsReturnTypeParameter)
				.Select(paramDef => mirrorGenerator.cache.Import(paramDef.Type))
				.ToArray();
			MethodDefUser mirror = new MethodDefUser(original.Name, MethodSig.CreateInstance(mirrorGenerator.cache.Import(original.ReturnType), paramTypes));
			mirror.Attributes = attrs;

			// And now the code.

			// DILEMMA:
			// Due to the nature of hooks, calling the base method should be the same as calling orig() in a hook.
			// The issue is that this behavior becomes ambiguous - if the user calls their override on their own, and then invokes base(), 
			// what should that do? It's outside the context of a hook, so orig() doesn't even exist.
			// Should it invoke the original method, which means it will run their hook?
			// It might be possible to have an "in use" flag to quietly skip running their method...
			FieldDefUser isCallerInInvocation = new FieldDefUser($"<{original.Name}>IsCallerInInvocation", new FieldSig(mirrorGenerator.MirrorModule.CorLibTypes.Boolean), FieldAttributes.PrivateScope);
			// Let's try that.

			CilBody cil = new CilBody();
			Local? defaultRetnValueLoc = null;
			// To have the jumps work property I have to create some instructions early.
			Instruction ldarg0_First = OpCodes.Ldarg_0.ToInstruction();
			Instruction ldarg0_Second = OpCodes.Ldarg_0.ToInstruction();
			Instruction popDelegate = OpCodes.Pop.ToInstruction();
#region Block 0
			// Block 0 aborts the method if IsCallerInInvocation is true.
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldfld.ToInstruction(isCallerInInvocation));
			cil.Instructions.Add(OpCodes.Brfalse.ToInstruction(ldarg0_First));

			// Wait, don't return quite yet!
			// Might need to push a return value onto the stack, if this method is returning.
			if (mirror.HasReturnType) {
				defaultRetnValueLoc = new Local(mirror.ReturnType, "retn");
				if (mirror.ReturnType.IsValueType) {
					// Value type: Use initobj for default()
					cil.Instructions.Add(OpCodes.Ldloca.ToInstruction(defaultRetnValueLoc));
					cil.Instructions.Add(OpCodes.Initobj.ToInstruction(mirror.ReturnType.ToTypeDefOrRef()));
					cil.Instructions.Add(OpCodes.Ldloc.ToInstruction(defaultRetnValueLoc));
				} else {
					// Reference type: Use ldnull for default()
					cil.Instructions.Add(OpCodes.Ldnull.ToInstruction());
				}
			}

			// NOW it can return.
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
#endregion
#region Block 1
			// This block loads the orig delegate that would have been set by the hook.
			// If it's null, it will set CALLER_IN_INVOCATION$ to true, and then call the original
			// method on the actual original class itself. This CALLER_IN_INVOCATION$ value is used
			// to prevent this code from running a second time when ^ inevitably fires all hooks (see block 1)

			cil.Instructions.Add(ldarg0_First);									// Func<...> del = this...
			cil.Instructions.Add(OpCodes.Ldfld.ToInstruction(delegateOrig));	// ...delegate_orig_(...)
			cil.Instructions.Add(OpCodes.Dup.ToInstruction());					// Func<...> del2 = del;
			cil.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(ldarg0_Second));// if (del == null) {
			cil.Instructions.Add(OpCodes.Pop.ToInstruction());					// Get rid of the delegate that was left on stack 0
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());              //		this...
			cil.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());             //		push true
			cil.Instructions.Add(OpCodes.Stfld.ToInstruction(isCallerInInvocation));    //		...CALLER_IN_INVOCATION$(...) = true (pop)
																				

			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                  // this...
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));        // ...Original
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(OptimizedLdarg(i + 1));                                        // Aaaalll the args
			}
			OpCode callCode;
			if (original.IsVirtual || original.IsAbstract) {
				callCode = OpCodes.Callvirt;
			} else {
				callCode = OpCodes.Call;
			}
			cil.Instructions.Add(callCode.ToInstruction(mbr));                  // Call the actual real method on the real object.
																				// This will trigger the hook. Refer to the start of this method...
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());              // this...
			cil.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());             // push false
			cil.Instructions.Add(OpCodes.Stfld.ToInstruction(isCallerInInvocation));    // ...CALLER_IN_INVOCATION$(...) = false
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
			#endregion

			#region Block 2
			// Block 2 calls the original hook
			// Reminder: delegate field thing is on the stack right now.
			cil.Instructions.Add(ldarg0_Second);													// this.
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));        // Original
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(OptimizedLdarg(i + 1));                                        // Aaaalll the args
			}
			cil.Instructions.Add(OpCodes.Call.ToInstruction(bieOrigInvokeRef));
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
#endregion
			mirror.Body = cil;
			if (defaultRetnValueLoc != null) {
				cil.Variables.Add(defaultRetnValueLoc);
			}

			return (mirror, delegateOrig, isCallerInInvocation);
		}

		private static Instruction OptimizedLdarg(int argN) {
			return argN switch {
				0 => OpCodes.Ldarg_0.ToInstruction(),
				1 => OpCodes.Ldarg_1.ToInstruction(),
				2 => OpCodes.Ldarg_2.ToInstruction(),
				3 => OpCodes.Ldarg_3.ToInstruction(),
				_ => new Instruction(OpCodes.Ldarg, new Local(null, null, (ushort)argN)),
			};
		}

		#endregion

		#region Basic Members and Initializers

		/// <summary>
		/// For use when creating the "Original" property of mirror types, this generates the IL of the getter to get the reference from the weak field.
		/// </summary>
		/// <param name="originalRefProp"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <returns></returns>
		public static MemberRefUser CreateOriginalReferencer(MirrorGenerator mirrorGenerator, PropertyDefUser originalRefProp, FieldDefUser originalWeakRefFld, GenericInstSig weakRef, GenericInstMethodSig tryGetTarget) {
			TypeRef weakReference = weakRef.TryGetTypeRef();
			ModuleDef module = weakRef.Module;
			MemberRefUser tryGetTargetMember = new MemberRefUser(module, "TryGetTarget", MethodSig.CreateInstanceGeneric(1, module.CorLibTypes.Boolean, originalRefProp.PropertySig.RetType), weakReference);
			MethodSpecUser tryGetTargetSpec = new MethodSpecUser(tryGetTargetMember, tryGetTarget);


			CilBody getter = new CilBody();
			Local local = new Local(originalRefProp.PropertySig.RetType, null, 0);
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc.ToInstruction(local));                                       // store in local
			getter.Instructions.Add(OpCodes.Ldloca.ToInstruction(local));										// Load by reference
			getter.Instructions.Add(OpCodes.Call.ToInstruction(tryGetTargetSpec));								// Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.
			getter.Variables.Add(local);

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public | MethodAttributes.CompilerControlled);
			mDef.Body = getter;
			mDef.ReturnType = originalRefProp.PropertySig.RetType;
			mDef.MethodSig.HasThis = true;

			originalRefProp.GetMethod = mDef;
			return tryGetTargetMember;
		}

		[Obsolete("This technique isn't very good. Implementors should have their own CWTs.")]
		public static FieldDefUser CreateStaticCWTInitializer(MirrorGenerator mirrorGenerator, GenericInstSig cwtType, TypeDefUser to, TypeSig key, TypeSig value) {
			ITypeDefOrRef cwtTypeRef = cwtType.ToTypeDefOrRef();

			//MethodDefUser staticInitializer = new MethodDefUser(".cctor", MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void), MethodAttributes.CompilerControlled | MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			MethodDef staticInitializer = to.FindOrCreateStaticConstructor();
			FieldDefUser cwtStorageFld = new FieldDefUser("_bindings", new FieldSig(cwtType), FieldAttributes.Private | FieldAttributes.InitOnly | FieldAttributes.Static);

			MethodSig constructorSig = MethodSig.CreateInstanceGeneric(2, mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser cwtCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", constructorSig, cwtTypeRef);

			CilBody staticInit = staticInitializer.Body;
			staticInit.Instructions.Clear();
			staticInit.Instructions.Add(OpCodes.Newobj.ToInstruction(cwtCtor));
			staticInit.Instructions.Add(OpCodes.Stsfld.ToInstruction(cwtStorageFld));
			staticInit.Instructions.Add(OpCodes.Ret.ToInstruction());

			return cwtStorageFld;
		}

		#endregion


	}
}
