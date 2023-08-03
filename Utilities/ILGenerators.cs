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
using TypeAttributes = dnlib.DotNet.TypeAttributes;
using HookGenExtender.PreWrittenCode;
using System.Security.AccessControl;

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
			getter.Instructions.Add(OpCodes.Call.ToInstruction(orgGetter));                         // (original prop name)
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
			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirror.PropertySig.RetType), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());                               // value
			setter.Instructions.Add(OpCodes.Call.ToInstruction(orgSetter));                         // set (original prop name here)
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
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));                            // (original field here)
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

			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());                               // value
			setter.Instructions.Add(OpCodes.Stfld.ToInstruction(field));                            // set (original field name here)
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
		[Obsolete("This technique is no longer supported.")]
		public static MethodDefUser GenerateMethodMirror(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, GeneratorSettings settings) {
			// This is where things get complicated.
			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			MemberRef mbr = mirrorGenerator.cache.Import(original);
			Parameter[] originalParams = original.Parameters
				.Skip(1)
				.Where(paramDef => !paramDef.IsReturnTypeParameter)
				.ToArray();
			TypeSig[] paramTypes = originalParams
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

			// NEW BEHAVIOR
			// This is just a courtesy to the user, but let's remember to name the parameters.
			for (int i = 0; i < originalParams.Length; i++) {
				Parameter current = mirror.Parameters[i];
				current.CreateParamDef();
				current.Name = originalParams[i].Name;
			}

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
		public static (MethodDefUser?, FieldDefUser?, FieldDefUser?, MethodDefUser?) TryGenerateBIEOrigCall(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, TypeDefUser binderType, GenericVar tExtendsExtensible, GeneratorSettings settings) {
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

			/* TO FUTURE XAN / MAINTAINERS:
			 * The way this is implemented is complicated, to put it lightly.
			 * For each and every possible hook, three things are generated in the Extensible class...
			 * 1: The advanced proxy method 
			 *		- (if called in the context of a hook, invoking the base of that method invokes orig(self, ...))
			 *		- (if called outside of a hook, invoking the base of that method invokes the method of the original object and triggers hooks)
			 *		
			 * 2: The context tracker (is the current invocation in the process of a hook, or is it outside of a hook?)
			 * 3: The current original delegate to call when appropriate.
			 * 
			 * Each Extensible class contains an inner class named "Binder". This Binder class keeps track of all instances of the extensible
			 * and also provides the method of creating these instances. The binder is also the class that registers its methods with the
			 * BIE hooks.
			 * 
			 * The Binder class also includes mirror methods of its own, but as mentioned, these are the actual hooks provided to BIE and thus are static.
			 * These get generated in this method as well.
			 * 
			 * For more information on the binder class, find its generator method in this class.
			 * 
			 */
			

			MethodAttributes attrs = MethodAttributes.Public;
			if (settings.MirrorsAreVirtual) attrs |= MethodAttributes.Virtual;

			// Start by declaring the field storing the original delegate.
			// To do this, acquire the hook type and the corresponding delegate.
			string fullName = original.DeclaringType.ReflectionFullName;
			string hookFullName = "On." + fullName;
			TypeDef hookClass = mirrorGenerator.BepInExHooksModule!.Find(hookFullName, true);
			if (hookClass == null) {
				return (null, null, null, null);
			}
			(TypeDef? bieOrigMethodDef, MethodDef? bieOrigInvoke, EventDef? bindEvt) = hookClass.TryGetOrigDelegateForMethod(original);
			if (bieOrigMethodDef == null) {
				return (null, null, null, null);
			}
			TypeRef hookClassRef = mirrorGenerator.cache.Import(hookClass);

			TypeRef bieOrigMethod = mirrorGenerator.cache.Import(bieOrigMethodDef);
			TypeSig bieOrigMethodSig = mirrorGenerator.cache.Import(bieOrigMethodDef.ToTypeSig());
			IMethodDefOrRef bieOrigInvokeRef = mirrorGenerator.cache.Import(bieOrigInvoke!);

			// Now create the field
			FieldDefUser delegateOrig = new FieldDefUser($"delegate_orig_{original.Name}", new FieldSig(bieOrigMethodSig), FieldAttributes.PrivateScope);

			// And now create the method.
			// Start by duplicating its parameters and creating the method frame.
			MemberRef mbr = mirrorGenerator.cache.Import(original);

			#region Generate Extensible Virtual
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
					cil.Instructions.Add(OpCodes.Ldloc_S.ToInstruction(defaultRetnValueLoc));
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

			cil.Instructions.Add(ldarg0_First);                                 // Func<...> del = this...
			cil.Instructions.Add(OpCodes.Ldfld.ToInstruction(delegateOrig));    // ...delegate_orig_(...)
			cil.Instructions.Add(OpCodes.Dup.ToInstruction());                  // Func<...> del2 = del;
			cil.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(ldarg0_Second));// if (del == null) {
			cil.Instructions.Add(OpCodes.Pop.ToInstruction());                  // Get rid of the delegate that was left on stack 0
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
			cil.Instructions.Add(ldarg0_Second);                                                    // this.
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

			// mirror.CustomAttributes.Add(CreateNewBoundAttribute(mirrorGenerator.BoundAttributeCtor!, bieOrigMethodDef.Name));
			#endregion

			#region Binder Implementation
			// Now this implementation needs to be moved to the binder type.
			// In this scenario, the binder has the event receivers.
			// The orig_ invoke method contains all the parameters (it has a "this" parameter which is the delegate, conveniently this can be used in the
			// static impl too, so this does indeed include all the parameters, even the delegate itself as arg0)

			#region Generate Static Hook Receiver Methods
			TypeSig[] allParamTypes = bieOrigInvoke!.Parameters.Select(paramDef => mirrorGenerator.cache.Import(paramDef.Type)).ToArray();

			MethodSig binderImplSig = MethodSig.CreateStatic(mirrorGenerator.cache.Import(original.ReturnType), allParamTypes);
			MethodDefUser binderImpl = new MethodDefUser(original.Name, binderImplSig);
			binderImpl.Attributes |= MethodAttributes.Static | MethodAttributes.PrivateScope;

			GenericInstSig binderGen = new GenericInstSig(binderType.ToTypeSig().ToClassOrValueTypeSig(), tExtendsExtensible);
			FieldDef instancesFld = binderType.GetField("_instances");
			GenericInstSig instancesFldInstance = new GenericInstSig(mirrorGenerator.CWTTypeSig, binderImplSig.RetType, tExtendsExtensible);
			MethodSig tryGetValueSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, binderImplSig.RetType, tExtendsExtensible);
			MemberRefUser tryGetValue = new MemberRefUser(mirrorGenerator.MirrorModule, "TryGetValue", tryGetValueSig, instancesFldInstance.ToTypeDefOrRef());

			CilBody body = new CilBody();
			Local target = new Local(tExtendsExtensible, "target");
			body.Variables.Add(target);
			Local? returnValue = null;
			if (binderImpl.HasReturnType) {
				returnValue = new Local(binderImpl.ReturnType, "returnValue");
				body.Variables.Add(returnValue);
			}
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesFld));
			body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(target));
			body.Instructions.Add(OpCodes.Call.ToInstruction(tryGetValue));
			Instruction nop = new Instruction(OpCodes.Nop);
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(nop));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // target
			body.Instructions.Add(OpCodes.Dup.ToInstruction());
			body.Instructions.Add(OpCodes.Dup.ToInstruction());
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction()); // orig
			body.Instructions.Add(OpCodes.Stfld.ToInstruction(delegateOrig)); // Put that into the delegate slot. Consumes second dup
																			  // Call
			for (int i = 2; i < allParamTypes.Length; i++) {
				// 0 = orig
				// 1 = self
				// 2... = args...
				body.Instructions.Add(OptimizedLdarg(i));
			}

			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(mirror)); // Call. Consumes first dup.
			if (binderImpl.HasReturnType) {
				// WAIT! If this is a returning method, the method pushed something onto the stack. Store it.
				body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			}

			// Unload, consumes the first ldloc_0 before the two dups
			body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			body.Instructions.Add(OpCodes.Stfld.ToInstruction(delegateOrig)); // Erase the delegate slot. Consumes initial ldloc_0

			if (binderImpl.HasReturnType) {
				// Load up that return value if we need to.
				body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			}
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(nop);
			// Now if we make it here, it means that there was no bound type, we need to just directly call orig here and now.
			for (int i = 0; i < allParamTypes.Length; i++) {
				// 0... = orig, self, args...
				// To future Xan: This is not a method, there is no "this" that gets popped. Do not ldarg_0 before the call.
				body.Instructions.Add(OptimizedLdarg(i));
			}
			body.Instructions.Add(OpCodes.Call.ToInstruction(bieOrigInvokeRef));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			body.MaxStack = 32;
			body.KeepOldMaxStack = true;
			binderImpl.Body = body;
			#endregion

			#region Generate Static Hook
			MethodDef cctor = binderType.FindOrCreateStaticConstructor();
			CilBody cctorBody;
			if (cctor.Body.Instructions.Count == 1) {
				cctorBody = new CilBody();
				cctor.Body = cctorBody;
			} else {
				cctorBody = cctor.Body;
			}

			MethodSig delegateConstructorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object, mirrorGenerator.MirrorModule.CorLibTypes.IntPtr);
			MemberRefUser delegateConstructor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, bieOrigMethod);

			MethodSig addSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, bieOrigMethodSig);
			MemberRefUser addEvent = new MemberRefUser(mirrorGenerator.MirrorModule, $"add_{mirror.Name}", addSig, bieOrigMethod.DeclaringType);

			ITypeDefOrRef genericInstanceOfBinder = binderGen.ToTypeDefOrRef();
			MemberRefUser binderImplRef = new MemberRefUser(mirrorGenerator.MirrorModule, binderImpl.Name, binderImplSig, genericInstanceOfBinder);

			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			cctorBody.Instructions.Add(OpCodes.Ldftn.ToInstruction(binderImplRef));
			cctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(delegateConstructor));
			cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(addEvent));

			#endregion

			#endregion

			return (mirror, delegateOrig, isCallerInInvocation, binderImpl);
		}

		private static Instruction OptimizedLdarg(int argN) {
			return argN switch {
				0 => OpCodes.Ldarg_0.ToInstruction(),
				1 => OpCodes.Ldarg_1.ToInstruction(),
				2 => OpCodes.Ldarg_2.ToInstruction(),
				3 => OpCodes.Ldarg_3.ToInstruction(),
				_ => new Instruction(argN <= byte.MaxValue ? OpCodes.Ldarg_S : OpCodes.Ldarg, new Local(null, null, (ushort)argN)),
			};
		}

		#endregion

		#region Basic Members and Initializers

		public static MethodDefUser GenerateBinderBindMethod(MirrorGenerator mirrorGenerator, ITypeDefOrRef originalImported, TypeDefUser extensible, GenericVar tExtendsExtensible, FieldDefUser instancesCache, FieldDefUser didFirstInit, GenericInstSig instancesSig, FieldDefUser mirrorOriginalWeakRefFld) {
			GenericInstSig extensibleWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, tExtendsExtensible);
			GenericInstSig originalWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, originalImported.ToTypeSig());
			MethodSig bindSig = MethodSig.CreateStatic(extensibleWeakRef, originalImported.ToTypeSig());
			MethodDefUser bind = new MethodDefUser("Bind", bindSig, MethodAttributes.Public | MethodAttributes.Static);

			MethodSig tryGetValueSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, originalImported.ToTypeSig(), tExtendsExtensible);
			MemberRefUser tryGetValue = new MemberRefUser(mirrorGenerator.MirrorModule, "TryGetValue", tryGetValueSig, instancesSig.ToTypeDefOrRef());

			MethodSig invalidOpExcCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			MemberRefUser invalidOpExcCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			MethodSig createInstanceSig = MethodSig.CreateStaticGeneric(1, tExtendsExtensible);
			MemberRefUser createInstanceRef = new MemberRefUser(mirrorGenerator.MirrorModule, "CreateInstance", createInstanceSig, mirrorGenerator.cache.Import(typeof(Activator)));
			MethodSpecUser createInstance = new MethodSpecUser(createInstanceRef, new GenericInstMethodSig(tExtendsExtensible));

			MethodSig setTargetSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalImported.ToTypeSig());
			MemberRefUser setTarget = new MemberRefUser(mirrorGenerator.MirrorModule, "SetTarget", setTargetSig, originalWeakRef.ToTypeDefOrRef());

			MethodSig cwtAddSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalImported.ToTypeSig(), tExtendsExtensible);
			MemberRefUser cwtAdd = new MemberRefUser(mirrorGenerator.MirrorModule, "Add", cwtAddSig, instancesSig.ToTypeDefOrRef());

			MethodSig weakRefCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, tExtendsExtensible);
			MemberRefUser weakRefCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", weakRefCtorSig, extensibleWeakRef.ToTypeDefOrRef());

			CilBody body = new CilBody();
			Local discard = new Local(tExtendsExtensible, "_");
			body.Variables.Add(discard);
			body.Variables.Add(new Local(tExtendsExtensible, "instance"));
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesCache));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(discard));
			body.Instructions.Add(OpCodes.Call.ToInstruction(tryGetValue));
			Instruction throwMsg = new Instruction(OpCodes.Ldstr, $"Duplicate binding! Only one instance of your current Extensible type can be found to an instance of type {originalImported.Name} at a time. If you need more, make more classes.");
			body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(throwMsg));

			body.Instructions.Add(OpCodes.Call.ToInstruction(createInstance)); // new T();
			body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			body.Instructions.Add(OpCodes.Ldfld.ToInstruction(mirrorOriginalWeakRefFld));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(setTarget));
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesCache));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(cwtAdd));
			body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefCtor));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(throwMsg);
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			body.Instructions.Add(OpCodes.Throw.ToInstruction());
			body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.KeepOldMaxStack = true;


			bind.Body = body;
			return bind;
		}

		[Obsolete]
		public static MemberRefUser CreateBoundAttributeConstructor(MirrorGenerator mirrorGenerator) {
			TypeDefUser boundAttribute = new TypeDefUser("<Extensible>EventHookMethodAttribute", mirrorGenerator.cache.Import(typeof(Attribute)));
			boundAttribute.Attributes = TypeAttributes.NotPublic | TypeAttributes.SpecialName;
			mirrorGenerator.MirrorModule.Types.Add(boundAttribute);

			FieldDefUser nameFld = new FieldDefUser("hookEventName", new FieldSig(mirrorGenerator.MirrorModule.CorLibTypes.String));
			boundAttribute.Fields.Add(nameFld);

			MethodDefUser constructor = new MethodDefUser(".ctor", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String), MethodAttributes.Assembly | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			CilBody body = new CilBody();
			body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Stfld.ToInstruction(nameFld));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			boundAttribute.Methods.Add(constructor);

			ITypeDefOrRef attributeUsage = mirrorGenerator.cache.Import(typeof(AttributeUsageAttribute));
			TypeSig attributeTargetsSig = mirrorGenerator.cache.ImportAsTypeSig(typeof(AttributeTargets));
			MemberRefUser ctorRef = new MemberRefUser(mirrorGenerator.Module, ".ctor", constructor.MethodSig, boundAttribute);
			MemberRefUser attrUsageRef = new MemberRefUser(mirrorGenerator.Module, ".ctor", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, attributeTargetsSig), attributeUsage);

			boundAttribute.CustomAttributes.Add(
			new CustomAttribute(
					attrUsageRef,
					new CAArgument[] {
						new CAArgument(attributeTargetsSig, AttributeTargets.Method | AttributeTargets.Property)
					},
					new CANamedArgument[] {
						new CANamedArgument(false, mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "Inherited",
						 new CAArgument(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, true)),

						new CANamedArgument(false, mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "AllowMultiple",
						 new CAArgument(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, false))
					}
				)
			);

			return ctorRef;
		}

		[Obsolete]
		public static CustomAttribute CreateNewBoundAttribute(MemberRefUser ctor, string orgEventName) {
			return new CustomAttribute(
				ctor,
				new CAArgument[] {
					new CAArgument(ctor.Module.CorLibTypes.String, orgEventName)
				}
			);
		}


		/// <summary>
		/// For use when creating the "Original" property of mirror types, this generates the IL of the getter to get the reference from the weak field.
		/// </summary>
		/// <param name="originalRefProp"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <returns></returns>
		public static void CreateOriginalReferencer(MirrorGenerator mirrorGenerator, PropertyDefUser originalRefProp, FieldDefUser originalWeakRefFld, GenericInstSig weakRef) {
			ModuleDef module = weakRef.Module;
			ITypeDefOrRef weakRefTypeRef = weakRef.ToTypeDefOrRef();

			MethodSig tryGetTargetSig = MethodSig.CreateInstance(module.CorLibTypes.Boolean, originalRefProp.PropertySig.RetType);
			MemberRefUser tryGetTargetMember = new MemberRefUser(module, "TryGetTarget", tryGetTargetSig, weakRefTypeRef);

			CilBody getter = new CilBody();
			Local local = new Local(originalRefProp.PropertySig.RetType, "storedOriginal", 0);
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc.ToInstruction(local));                                        // store in local
			getter.Instructions.Add(OpCodes.Ldloca.ToInstruction(local));                                       // Load by reference
			getter.Instructions.Add(OpCodes.Call.ToInstruction(tryGetTargetMember));                            // Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.
			getter.Variables.Add(local);

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public | MethodAttributes.CompilerControlled);
			mDef.Body = getter;

			originalRefProp.GetMethod = mDef;
		}

		public static MethodDefUser CreateConstructor(MirrorGenerator mirrorGenerator, PropertyDefUser originalRefProp, FieldDefUser originalWeakRefFld, GenericInstSig weakRefGenericInstance, GenericInstMethodSig constructor) {
			ITypeDefOrRef weakRefTypeRef = weakRefGenericInstance.ToTypeDefOrRef();

			//MethodSig constructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalRefProp.PropertySig.RetType);
			MethodSig constructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser constructorMember = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", constructorSignature, weakRefTypeRef);
			MethodSpecUser constructorSpec = new MethodSpecUser(constructorMember, constructor);

			MethodDefUser ctor = new MethodDefUser(".ctor", constructorSignature, MethodAttributes.PrivateScope | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			CilBody ctorBody = new CilBody();
			ctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(constructorSpec));
			ctorBody.Instructions.Add(OpCodes.Stfld.ToInstruction(originalWeakRefFld));
			ctorBody.Instructions.Add(OpCodes.Ret.ToInstruction());
			ctor.Body = ctorBody;

			return ctor;
		}

		[Obsolete]
		public static FieldDefUser CreateStaticCWTInitializer(MirrorGenerator mirrorGenerator, GenericInstSig cwtType, TypeDefUser to) {
			ITypeDefOrRef cwtTypeRef = cwtType.ToTypeDefOrRef();

			//MethodDefUser staticInitializer = new MethodDefUser(".cctor", MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void), MethodAttributes.CompilerControlled | MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			MethodDef staticInitializer = to.FindOrCreateStaticConstructor();
			FieldDefUser cwtStorageFld = new FieldDefUser("Extensible<bindings>", new FieldSig(cwtType), FieldAttributes.PrivateScope | FieldAttributes.InitOnly | FieldAttributes.Static);

			MethodSig constructorSig = MethodSig.CreateInstanceGeneric(2, mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser cwtCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", constructorSig, cwtTypeRef);

			CilBody staticInit = staticInitializer.Body;
			staticInit.Instructions.Clear();

			// Create the CWT.
			staticInit.Instructions.Add(OpCodes.Newobj.ToInstruction(cwtCtor));
			staticInit.Instructions.Add(OpCodes.Stsfld.ToInstruction(cwtStorageFld));
			staticInit.Instructions.Add(OpCodes.Ret.ToInstruction());

			return cwtStorageFld;
		}

		[Obsolete]
		public static void CreateLegacyExtensibleBinderBaseCctor(MirrorGenerator mirrorGenerator, TypeDefUser baseType, FieldDefUser binders, GenericInstSig dictionary) {
			MethodSig ctorSig = MethodSig.CreateInstanceGeneric(2, mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser dictCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", ctorSig, dictionary.ToTypeDefOrRef());

			MethodDef staticCtor = baseType.FindOrCreateStaticConstructor();
			CilBody body = staticCtor.Body;
			body.Instructions.Clear();
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(dictCtor));
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(binders));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
		}

		[Obsolete]
		public static void CreateLegacyExtensibleBinderFactory(MirrorGenerator mirrorGenerator, ITypeDefOrRef dictionaryType, TypeDefUser binderType, MethodDefUser staticCreateBinder, MethodDefUser binderCtor, FieldDefUser binders, ITypeDefOrRef typeType) {
			CilBody body = new CilBody();
			TypeSig typeTypeSig = typeType.ToTypeSig();
			TypeSig binderTypeSig = binderType.ToTypeSig();
			Local type = new Local(typeTypeSig, "type");
			Local binder = new Local(binders.FieldType, "binder");
			Local exists = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "exists");
			Local instance = new Local(binderTypeSig, "instance");
			body.Variables.Add(type);
			body.Variables.Add(binder);
			body.Variables.Add(exists);
			body.Variables.Add(instance);

			GenericInstSig genericInstSig = new GenericInstSig(binderTypeSig.ToClassOrValueTypeSig(), 2);
			MethodSig getTypeFromHandleSig = MethodSig.CreateInstance(typeTypeSig, mirrorGenerator.cache.ImportAsTypeSig(typeof(RuntimeTypeHandle)));
			MemberRefUser getTypeFromHandle = new MemberRefUser(mirrorGenerator.MirrorModule, "GetTypeFromHandle", getTypeFromHandleSig, typeType);

			MethodSig tryGetValueSig = MethodSig.CreateInstanceGeneric(2, mirrorGenerator.MirrorModule.CorLibTypes.Boolean, genericInstSig.GenericArguments[0], genericInstSig.GenericArguments[1]);
			MemberRefUser tryGetValue = new MemberRefUser(mirrorGenerator.MirrorModule, "TryGetValue", tryGetValueSig, dictionaryType);

			MethodSig invalidOpExcCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			MemberRefUser invalidOpExcCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			MethodSig dictionaryAddSig = MethodSig.CreateInstanceGeneric(2, mirrorGenerator.MirrorModule.CorLibTypes.Void, genericInstSig.GenericArguments[0], genericInstSig.GenericArguments[1]);
			MemberRefUser dictionaryAdd = new MemberRefUser(mirrorGenerator.MirrorModule, "Add", dictionaryAddSig, dictionaryType);

			body.Instructions.Add(new Instruction(OpCodes.Ldtoken, genericInstSig.GenericArguments[1]));
			body.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
			body.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(binders));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(binder));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ceq.ToInstruction());
			body.Instructions.Add(OpCodes.Stloc_2.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_2.ToInstruction());
			Instruction errMsg = OpCodes.Ldstr.ToInstruction("Do not create more than one ExtensibleBinder for your type! Create it once (in your mod's Awake/OnEnable, for example) and then use the stored reference.");
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(errMsg));
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(binderCtor));
			body.Instructions.Add(OpCodes.Stloc_3.ToInstruction());
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(binders));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_3.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(dictionaryAdd));
			body.Instructions.Add(OpCodes.Ldloc_3.ToInstruction());
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(errMsg);
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			body.Instructions.Add(OpCodes.Throw.ToInstruction());
			body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			staticCreateBinder.Body = body;
		}

		#endregion


	}
}
