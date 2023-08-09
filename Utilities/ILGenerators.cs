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
using System.Security.AccessControl;

namespace HookGenExtender.Utilities {
	public static class ILGenerators {

		/// <summary>
		/// The virtual flag for property getters/setters. This might be 0, in which case overridable properties are not supported yet
		/// </summary>
		private const MethodAttributes PROPERTIES_VIRTUAL = 0;

		#region (Incomplete) Caches

		// TODO: Cache everything!

		private static MethodSig invalidOpExcCtorSig = null;
		private static MemberRefUser invalidOpExcCtor = null;


		private static TypeSig typeTypeSig = null;
		private static TypeSig runtimeTypeHandleSig = null;
		private static ITypeDefOrRef typeTypeRef = null;
		private static TypeSig bindingFlagsSig = null;
		private static TypeSig methodInfoSig = null;
		private static TypeSig systemReflectionBinderSig = null;
		private static TypeSig typeArraySig = null;
		private static TypeSig paramModifierArraySig = null;
		private static MemberRefUser getType = null;
		private static MemberRefUser getTypeFromHandle = null;
		private static MemberRefUser getMethod = null;


		#endregion

		#region Property Get/Set Mirrors

		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a getter for the mirror property that calls the getter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		public static void CreateGetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDef orgProp, PropertyDefUser originalRefProperty) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled | PROPERTIES_VIRTUAL;

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
		public static void CreateSetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDef orgProp, PropertyDefUser originalRefProperty) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled | PROPERTIES_VIRTUAL;

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
		public static void CreateGetterToField(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, MemberRef field, PropertyDefUser originalRefProperty) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			// ABOVE: Unlike properties, fields are sealed!

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
		public static void CreateSetterToField(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, MemberRef field, PropertyDefUser originalRefProperty) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled;
			// ABOVE: Unlike properties, fields are sealed!

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
		/// Generates a delegate pointing to BepInEx's hook orig_MethodName delegates, allowing the mirror method to be used in BIE patches.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="original"></param>
		/// <param name="originalRefProperty"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public static (MethodDefUser, FieldDefUser, FieldDefUser, MethodDefUser) TryGenerateBIEOrigCallAndProxies(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, TypeDefUser binderType, GenericVar tExtendsExtensible, TypeRef originalClass, MethodDef hookBinders) {
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

			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.Virtual;

			// Start by declaring the field storing the original delegate.
			// To do this, acquire the hook type and the corresponding delegate.
			string fullName = original.DeclaringType.ReflectionFullName;
			string hookFullName = "On." + fullName;
			TypeDef hookClass = mirrorGenerator.BepInExHooksModule!.Find(hookFullName, true);
			if (hookClass == null) {
				return (null, null, null, null);
			}
			(TypeDef bieOrigMethodDef, MethodDef bieOrigInvoke, EventDef bindEvt) = hookClass.TryGetOrigDelegateForMethod(original);
			if (bieOrigMethodDef == null) {
				return (null, null, null, null);
			}
			TypeRef hookClassRef = mirrorGenerator.cache.Import(hookClass);

			TypeRef bieOrigMethod = mirrorGenerator.cache.Import(bieOrigMethodDef);
			TypeSig bieOrigMethodSig = mirrorGenerator.cache.Import(bieOrigMethodDef.ToTypeSig());
			IMethodDefOrRef bieOrigInvokeRef = mirrorGenerator.cache.Import(bieOrigInvoke!);

			// Now create the field
			FieldDefUser delegateOrig = new FieldDefUser($"<orig_{original.Name}>ExtensibleCallback", new FieldSig(bieOrigMethodSig), MirrorGenerator.PRIVATE_FIELD_TYPE);

			// And now create the method.
			// Start by duplicating its parameters and creating the method frame.
			MemberRef mbr = mirrorGenerator.cache.Import(original);
			TypeSig originalReturnType = mirrorGenerator.cache.Import(original.ReturnType);

			#region Generate Extensible Virtual
			TypeSig[] paramTypes = original.Parameters
				.Skip(1) // skip 'this'
				.Where(paramDef => !paramDef.IsReturnTypeParameter)
				.Select(paramDef => mirrorGenerator.cache.Import(paramDef.Type))
				.ToArray();
			MethodDefUser mirror = new MethodDefUser(original.Name, MethodSig.CreateInstance(originalReturnType, paramTypes));
			for (int i = 0; i <= paramTypes.Length; i++) {
				mirror.SetParameterName(i, original.Parameters[i].Name);
			}
			mirror.Attributes = attrs;

			// And now the code.

			// DILEMMA:
			// Due to the nature of hooks, calling the base method should be the same as calling orig() in a hook.
			// The issue is that this behavior becomes ambiguous - if the user calls their override on their own, and then invokes base(), 
			// what should that do? It's outside the context of a hook, so orig() doesn't even exist.
			// Should it invoke the original method, which means it will run their hook?
			// It might be possible to have an "in use" flag to quietly skip running their method...
			FieldDefUser isCallerInInvocation = new FieldDefUser($"<{original.Name}>IsCallerInInvocation", new FieldSig(mirrorGenerator.MirrorModule.CorLibTypes.Boolean), MirrorGenerator.PRIVATE_FIELD_TYPE);
			isCallerInInvocation.IsSpecialName = true;
			isCallerInInvocation.IsRuntimeSpecialName = true;
			// Let's try that.

			CilBody cil = new CilBody();
			// To have the jumps work property I have to create some instructions early.
			//Instruction ldarg0_First = OpCodes.Ldarg_0.ToInstruction();
			//Instruction ldarg0_Second = OpCodes.Ldarg_0.ToInstruction();
			//Instruction popDelegate = OpCodes.Pop.ToInstruction();

			// TO FUTURE XAN / MAINTAINERS:
			// This technique below looks weird (especially with it calling orig() *outside* of the null check, where it might throw NRE)
			// The catch is that this is exactly the path that should be taken.
			// A case where orig is null and IsInInvocation is true should be impossible, because that means it bypassed the binder somehow, which is not allowed.
			// This has been relayed by the explicit invalid op exception.


			invalidOpExcCtorSig ??= MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			invalidOpExcCtor ??= new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			Instruction ldarg0_First = OpCodes.Ldarg_0.ToInstruction();
			Instruction ldIsOrigNull = OpCodes.Ldloc_0.ToInstruction();
			Local isOrigNull = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "isOrigNull");
			cil.Variables.Add(isOrigNull);
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldfld.ToInstruction(delegateOrig));
			cil.Instructions.Add(OpCodes.Dup.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			cil.Instructions.Add(OpCodes.Ceq.ToInstruction());
			cil.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(ldIsOrigNull)); // nb: delegateOrig is the only thing on the stack after this instruction

			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldfld.ToInstruction(isCallerInInvocation));
			cil.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ceq.ToInstruction());
			cil.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(ldIsOrigNull));
			//// {
			// Discard the delegate
			cil.Instructions.Add(OpCodes.Pop.ToInstruction());

			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			cil.Instructions.Add(OpCodes.Stfld.ToInstruction(isCallerInInvocation));

			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(OptimizedLdarg(i + 1));
			}
			OpCode callCode;
			if (!original.IsStatic) {
				callCode = OpCodes.Callvirt;
			} else {
				callCode = OpCodes.Call;
			}
			cil.Instructions.Add(callCode.ToInstruction(mbr));

			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Stfld.ToInstruction(isCallerInInvocation));

			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
			//// }
			// Reminder: delegate field thing is on the stack right now.

			cil.Instructions.Add(ldIsOrigNull);
			cil.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(ldarg0_First));
			cil.Instructions.Add(OpCodes.Pop.ToInstruction()); // Get rid of the (null) delegate from the stack, it is no longer needed.
			cil.Instructions.Add(OpCodes.Ldstr.ToInstruction("Illegal state encountered: System was instructed to fall back to the original delegate (execution of this method is already occurring, doing so would be infinitely recursive) but the original delegate was null. This should be impossible unless something bypassed the Binder."));
			cil.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			cil.Instructions.Add(OpCodes.Throw.ToInstruction());

			cil.Instructions.Add(ldarg0_First);
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(OptimizedLdarg(i + 1));
			}
			cil.Instructions.Add(OpCodes.Call.ToInstruction(bieOrigInvokeRef));
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
			mirror.Body = cil;
			#endregion

			#region Binder Implementation
			// Now this implementation needs to be moved to the binder type.
			// In this scenario, the binder has the event receivers.
			// The orig_ invoke method contains all the parameters (it has a "this" parameter which is the delegate, conveniently this can be used in the
			// static impl too, so this does indeed include all the parameters, even the delegate itself as arg0)

			#region Generate Static Hook Receiver Methods
			TypeSig[] allParamTypes = bieOrigInvoke!.Parameters.Select(paramDef => mirrorGenerator.cache.Import(paramDef.Type)).ToArray();
			TypeSig originalClassSig = originalClass.ToTypeSig();

			MethodSig receiverImplSig = MethodSig.CreateStatic(originalReturnType, allParamTypes);
			MethodDefUser receiverImpl = new MethodDefUser(original.Name, receiverImplSig);
			receiverImpl.Attributes |= MethodAttributes.Static | MirrorGenerator.PRIVATE_METHOD_TYPE;

			// _instances generic type
			GenericInstSig binderAsGenericSig = new GenericInstSig(binderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
			ITypeDefOrRef binderAsGeneric = binderAsGenericSig.ToTypeDefOrRef();

			GenericInstSig instancesFieldGenericSignature = new GenericInstSig(mirrorGenerator.CWTTypeSig, originalClassSig, new GenericVar(0));
			ITypeDefOrRef genericInstancesFieldReference = instancesFieldGenericSignature.ToTypeDefOrRef();
			MemberRefUser instancesField = new MemberRefUser(mirrorGenerator.MirrorModule, "_instances", new FieldSig(instancesFieldGenericSignature), binderAsGeneric);
			
			MethodSig tryGetValueSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0), new ByRefSig(new GenericVar(1)));
			MemberRefUser tryGetValue = new MemberRefUser(mirrorGenerator.MirrorModule, "TryGetValue", tryGetValueSig, genericInstancesFieldReference);

			CilBody body = new CilBody();
			Local target = new Local(tExtendsExtensible, "target");
			body.Variables.Add(target);
			Local returnValue = null;
			if (receiverImpl.HasReturnType) {
				returnValue = new Local(receiverImpl.ReturnType, "returnValue");
				body.Variables.Add(returnValue);
			}
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
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
			if (receiverImpl.HasReturnType) {
				// WAIT! If this is a returning method, the method pushed something onto the stack. Store it.
				body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			}

			// Unload, consumes the first ldloc_0 before the two dups
			body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			body.Instructions.Add(OpCodes.Stfld.ToInstruction(delegateOrig)); // Erase the delegate slot. Consumes initial ldloc_0

			if (receiverImpl.HasReturnType) {
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
			receiverImpl.Body = body;
			#endregion

			#region Generate Static Hook
			CilBody cctorBody = hookBinders.Body; // Expects modifications as a result of GenerateInstancesConstructor

			MethodSig delegateConstructorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object, mirrorGenerator.MirrorModule.CorLibTypes.IntPtr);
			MemberRefUser delegateConstructor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, (ITypeDefOrRef)mirrorGenerator.cache.Import(bindEvt.EventType));

			GenericInstSig genericBinder = new GenericInstSig(binderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
			MemberRefUser genericBinderMtd = new MemberRefUser(mirrorGenerator.MirrorModule, mirror.Name, receiverImpl.MethodSig, genericBinder.ToTypeDefOrRef());

			// NEW: Only hook the ones we actually use.
			nop = new Instruction(OpCodes.Nop);
			typeTypeSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Type));
			typeTypeRef ??= mirrorGenerator.cache.Import(typeof(Type));
			runtimeTypeHandleSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(RuntimeTypeHandle));
			bindingFlagsSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(BindingFlags));
			methodInfoSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(MethodInfo));
			systemReflectionBinderSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Binder));
			typeArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Type[]));
			paramModifierArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(ParameterModifier[]));

			getType ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetType", MethodSig.CreateInstance(typeTypeSig), mirrorGenerator.MirrorModule.CorLibTypes.Object.ToTypeDefOrRef());
			getTypeFromHandle ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetTypeFromHandle", MethodSig.CreateStatic(typeTypeSig, runtimeTypeHandleSig), typeTypeRef);
			getMethod ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetMethod", MethodSig.CreateInstance(methodInfoSig, mirrorGenerator.MirrorModule.CorLibTypes.String, bindingFlagsSig, systemReflectionBinderSig, typeArraySig, paramModifierArraySig), typeTypeRef);

			Local extensibleType;
			Local bindingFlags;
			if (cctorBody.Instructions.Count == 0) {
				// This is the top of the method. Store some stuff.
				extensibleType = new Local(typeTypeSig, "type");
				bindingFlags = new Local(bindingFlagsSig, "flags");
				cctorBody.Variables.Add(extensibleType);
				cctorBody.Variables.Add(bindingFlags);

				cctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				cctorBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getType));
				cctorBody.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
				cctorBody.Instructions.Add(OptimizedLdc_I4((int)(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)));
				cctorBody.Instructions.Add(OpCodes.Stloc_1.ToInstruction());

			} else {
				extensibleType = cctorBody.Variables[0];
				bindingFlags = cctorBody.Variables[1];
			}

			cctorBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // type (this.)
			cctorBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(mirror.Name)); // method name
			cctorBody.Instructions.Add(OpCodes.Ldloc_1.ToInstruction()); // BindingFlags
			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction()); // binder
			cctorBody.Instructions.Add(OptimizedLdc_I4(paramTypes.Length)); // num params...
			cctorBody.Instructions.Add(OpCodes.Newarr.ToInstruction(typeTypeRef)); // new Type[]
			if (paramTypes.Length > 0) {
				cctorBody.Instructions.Add(OpCodes.Dup.ToInstruction());
				for (int i = 0; i < paramTypes.Length; i++) {
					cctorBody.Instructions.Add(OptimizedLdc_I4(i));
					cctorBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(paramTypes[i].ToTypeDefOrRef()));
					cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
					cctorBody.Instructions.Add(OpCodes.Stelem_Ref.ToInstruction());
					if (i < paramTypes.Length - 1) {
						cctorBody.Instructions.Add(OpCodes.Dup.ToInstruction());
					}
				}
			}
			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction()); // param modifier array
			cctorBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getMethod));
			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			cctorBody.Instructions.Add(OpCodes.Ceq.ToInstruction());
			cctorBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(nop));
			//
			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			cctorBody.Instructions.Add(OpCodes.Ldftn.ToInstruction(genericBinderMtd));
			cctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(delegateConstructor));
			cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(mirrorGenerator.cache.Import(bindEvt.AddMethod)));
			//
			cctorBody.Instructions.Add(nop);

			#endregion

			#endregion

			return (mirror, delegateOrig, isCallerInInvocation, receiverImpl);
		}

		/// <summary>
		/// For use in ldarg, which (for some reason) wants this.
		/// </summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		private static Parameter ParameterIndex(int arg) => new Parameter(arg);

		/// <summary>
		/// Returns the version of Ldarg best suited for the input argument index.
		/// </summary>
		/// <param name="argN"></param>
		/// <returns></returns>
		private static Instruction OptimizedLdarg(int argN, bool asReference = false) {
			if (argN < byte.MaxValue) return new Instruction(asReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, ParameterIndex(argN));
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
		private static Instruction OptimizedLdc_I4(int value) {
			if (value >= sbyte.MinValue && value <= sbyte.MaxValue) return new Instruction(OpCodes.Ldc_I4_S, (sbyte)value);
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

		#endregion

		#region Basic Members and Initializers

		public static (MethodDefUser, MethodDefUser, MethodDefUser) GenerateBinderBindAndDestroyMethod(MirrorGenerator mirrorGenerator, TypeDefUser mirrorType, GenericInstSig binder, TypeSig originalImported, GenericInstSig instancesSig, FieldDefUser hasHooked, MethodDefUser createHooks) {
			GenericInstSig extensibleWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
			GenericInstSig originalWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, originalImported);

			MethodSig bindSig = MethodSig.CreateStatic(extensibleWeakRef, originalImported);
			MethodDefUser bind = new MethodDefUser("Bind", bindSig, MethodAttributes.Public | MethodAttributes.Static);
			bind.SetParameterName(0, "toObject");

			MethodSig bindExistingSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalImported, new GenericVar(0));
			MethodDefUser bindExisting = new MethodDefUser("BindExisting", bindExistingSig, MirrorGenerator.PRIVATE_METHOD_TYPE | MethodAttributes.Static);

			MethodSig releaseSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, originalImported);
			MethodDefUser destroy = new MethodDefUser("TryReleaseCurrentBinding", releaseSig, MethodAttributes.Public | MethodAttributes.Static);
			destroy.SetParameterName(0, "toObject");

			ITypeDefOrRef instancesGeneric = instancesSig.ToTypeDefOrRef();
			ITypeDefOrRef extensibleTypeWeakReference = extensibleWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef originalTypeWeakRef = originalWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef genericBinderType = binder.ToTypeDefOrRef();
			ITypeDefOrRef extensibleType = new GenericVar(0).ToTypeDefOrRef();

			MemberRefUser hasHookedInstance = new MemberRefUser(mirrorGenerator.MirrorModule, hasHooked.Name, hasHooked.FieldSig, genericBinderType);
			MemberRefUser createHooksInstance = new MemberRefUser(mirrorGenerator.MirrorModule, createHooks.Name, createHooks.MethodSig, genericBinderType);

			MemberRefUser instancesField = new MemberRefUser(mirrorGenerator.MirrorModule, "_instances", new FieldSig(instancesSig), genericBinderType);
			MemberRefUser originalWeakTypeField = new MemberRefUser(mirrorGenerator.MirrorModule, "<Extensible>original", new FieldSig(originalWeakRef), mirrorType);

			MethodSig tryGetValueSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0), new ByRefSig(new GenericVar(1)));
			MemberRefUser tryGetValue = new MemberRefUser(mirrorGenerator.MirrorModule, "TryGetValue", tryGetValueSig, instancesGeneric);
			
			MethodSig removeSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0));
			MemberRefUser remove = new MemberRefUser(mirrorGenerator.MirrorModule, "Remove", removeSig, instancesGeneric);

			MethodSig invalidOpExcCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			MemberRefUser invalidOpExcCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			MethodSig createInstanceSig = MethodSig.CreateStaticGeneric(1, new GenericMVar(0));
			MemberRefUser createInstanceRef = new MemberRefUser(mirrorGenerator.MirrorModule, "CreateInstance", createInstanceSig, mirrorGenerator.cache.Import(typeof(Activator)));
			MethodSpecUser createInstance = new MethodSpecUser(createInstanceRef, new GenericInstMethodSig(new GenericVar(0)));

			MethodSig setTargetSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser setTarget = new MemberRefUser(mirrorGenerator.MirrorModule, "SetTarget", setTargetSig, originalTypeWeakRef);

			MethodSig cwtAddSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0), new GenericVar(1));
			MemberRefUser cwtAdd = new MemberRefUser(mirrorGenerator.MirrorModule, "Add", cwtAddSig, instancesGeneric);

			MethodSig weakRefCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser weakRefCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", weakRefCtorSig, extensibleTypeWeakReference);

			#region Bind()
			CilBody body = new CilBody();
			Local instance = new Local(new GenericVar(0), "instance");
			body.Variables.Add(instance);

			Instruction nop = new Instruction(OpCodes.Nop);

			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ceq.ToInstruction());
			Instruction throwMsg = new Instruction(OpCodes.Ldstr, $"Duplicate binding! Only one instance of your current Extensible type can be bound to an instance of type {originalImported.GetName()} at a time. In general, you should explicitly call {destroy.Name} to free the binding immediately rather than waiting on the chance of garbage collection.");
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(throwMsg));

			body.Instructions.Add(OpCodes.Call.ToInstruction(createInstance)); // new T();
			body.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Box.ToInstruction(extensibleType));
			body.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakTypeField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(setTarget));
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtAdd));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefCtor)); // Need to remember this value here.
																			  // Time to call the Bind methods of base types.

			// Create hooks real quick.
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(hasHookedInstance));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Beq_S.ToInstruction(nop));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(createHooksInstance));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(hasHookedInstance));

			body.Instructions.Add(nop);

			TypeDef mirrorBaseType = mirrorType.BaseType.ResolveTypeDef();
			if (mirrorGenerator.IsDeclaredMirrorType(mirrorBaseType)) {
				TypeDef baseBinderType = mirrorBaseType.NestedTypes.First(tDef => tDef.Name == "Binder" && tDef is TypeDefUser);

				/*
				GenericInstSig genericBaseBinderSig = new GenericInstSig(baseBinderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
				GenericInstSig baseWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
				MethodSig baseBinderBindSig = MethodSig.CreateStatic(baseWeakRef, mirrorGenerator.GetOriginal((TypeDefUser)mirrorBaseType).ToTypeSig());
				MemberRefUser baseBinderBind = new MemberRefUser(mirrorGenerator.MirrorModule, "Bind", baseBinderBindSig, genericBaseBinderSig.ToTypeDefOrRef());
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction()); // Load the instance of the subclass. This is a valid member for the parent Bind method.
				body.Instructions.Add(OpCodes.Call.ToInstruction(baseBinderBind)); // Now call the base class's bind method.
				body.Instructions.Add(OpCodes.Pop.ToInstruction()); // Pop the reference, don't care about it.
				*/
				GenericInstSig genericBaseBinderSig = new GenericInstSig(baseBinderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
				GenericInstSig baseWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
				TypeRef baseParamType = mirrorGenerator.GetOriginal((TypeDefUser)mirrorBaseType);
				MethodSig baseBinderBindSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, baseParamType.ToTypeSig(), new GenericVar(0));
				MemberRefUser baseBinderBind = new MemberRefUser(mirrorGenerator.MirrorModule, "BindExisting", baseBinderBindSig, genericBaseBinderSig.ToTypeDefOrRef());
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction()); // Load the instance of the subclass. This is a valid member for the parent Bind method.
				body.Instructions.Add(OpCodes.Castclass.ToInstruction(baseParamType));
				body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // Load the user-defined instance
				body.Instructions.Add(OpCodes.Call.ToInstruction(baseBinderBind)); // Now call the base class's BindExisting method.
			}

			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(throwMsg);
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			body.Instructions.Add(OpCodes.Throw.ToInstruction());

			bind.Body = body;
			#endregion

			#region BindExisting()
			body = new CilBody();
			body.Variables.Add(instance);
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ceq.ToInstruction());
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(throwMsg)); // Reuse throwMsg

			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtAdd));

			if (mirrorGenerator.IsDeclaredMirrorType(mirrorBaseType)) {
				TypeDef baseBinderType = mirrorBaseType.NestedTypes.First(tDef => tDef.Name == "Binder" && tDef is TypeDefUser);
				GenericInstSig genericBaseBinderSig = new GenericInstSig(baseBinderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
				GenericInstSig baseWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
				TypeRef baseParamType = mirrorGenerator.GetOriginal((TypeDefUser)mirrorBaseType);
				MethodSig baseBinderBindSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, baseParamType.ToTypeSig(), new GenericVar(0));
				MemberRefUser baseBinderBind = new MemberRefUser(mirrorGenerator.MirrorModule, "BindExisting", baseBinderBindSig, genericBaseBinderSig.ToTypeDefOrRef());
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction()); // Load the instance of the subclass. This is a valid member for the parent Bind method.
				body.Instructions.Add(OpCodes.Castclass.ToInstruction(baseParamType));
				body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction()); // Load the existing bound type.
				body.Instructions.Add(OpCodes.Call.ToInstruction(baseBinderBind)); // Now call the base class's BindExisting method.
			}

			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(throwMsg);
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			body.Instructions.Add(OpCodes.Throw.ToInstruction());
			bindExisting.Body = body;
			#endregion


			#region Release()
			body = new CilBody();
			instance = new Local(new GenericVar(0), "instance");
			//Local result = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "result");
			body.Variables.Add(instance);
			//body.Variables.Add(result);
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			Instruction pushFalse = new Instruction(OpCodes.Ldc_I4_0);
			body.Instructions.Add(OpCodes.Beq_S.ToInstruction(pushFalse));

			// Present:
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(remove)); // This puts a bool onto the stack.
			//body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			//body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			////
			body.Instructions.Add(pushFalse);
			//body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			//body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			destroy.Body = body;
			#endregion
			return (bind, bindExisting, destroy);
		}

		/// <summary>
		/// This is used to begin the generation of the static constructor for a Binder type. It assigns the _instances field, and logs a message indicating that initialization has started.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="mirrorType"></param>
		/// <param name="binderNongeneric"></param>
		/// <param name="binder"></param>
		/// <param name="instancesSig"></param>
		public static void GenerateInstancesConstructor(MirrorGenerator mirrorGenerator, TypeDef mirrorType, TypeDef binderNongeneric, GenericInstSig binder, GenericInstSig instancesSig) {
			ITypeDefOrRef binderType = binder.ToTypeDefOrRef();
			MemberRefUser instancesField = new MemberRefUser(mirrorGenerator.MirrorModule, "_instances", new FieldSig(instancesSig), binderType);

			MethodSig ctor = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser cwtCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", ctor, instancesSig.ToTypeDefOrRef());

			MethodDef staticCtor = binderNongeneric.FindOrCreateStaticConstructor();
			CilBody body = new CilBody();
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(cwtCtor));
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(instancesField));
			// NO RET - more code will be added later.
			// EDIT FROM FUTURE XAN:
			// Yes RET!
			// I moved the hooking to the Bind method via an external static method.
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			/*
			ITypeDefOrRef console = mirrorGenerator.cache.Import(typeof(UnityEngine.Debug));
			MethodSig writeLineSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object);
			MemberRefUser writeLine = new MemberRefUser(mirrorGenerator.MirrorModule, "Log", writeLineSig, console);
			body.Instructions.Add(OpCodes.Ldstr.ToInstruction($"Initializing binder for Extensible type: {mirrorType.FullName}"));
			body.Instructions.Add(OpCodes.Call.ToInstruction(writeLine));
			*/

			staticCtor.Body = body;
		}

		/// <summary>
		/// This method is called at the end of generating the static constructor for a Binder type. It logs completion, and appends the ret instruction.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="mirrorType"></param>
		/// <param name="binderNongeneric"></param>
		[Obsolete("The hooks are generated on call to Bind now.")]
		public static void CloseInstancesConstructor(MirrorGenerator mirrorGenerator, TypeDef mirrorType, TypeDef binderNongeneric) {
			CilBody body = binderNongeneric.FindOrCreateStaticConstructor().Body;

			/*
			ITypeDefOrRef console = mirrorGenerator.cache.Import(typeof(UnityEngine.Debug));
			MethodSig writeLineSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object);
			MemberRefUser writeLine = new MemberRefUser(mirrorGenerator.MirrorModule, "Log", writeLineSig, console);
			body.Instructions.Add(OpCodes.Ldstr.ToInstruction($"Done initializing binder for Extensible type: {mirrorType.FullName}"));
			body.Instructions.Add(OpCodes.Call.ToInstruction(writeLine));
			*/
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
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

			MethodSig tryGetTargetSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new ByRefSig(new GenericVar(0)));
			MemberRefUser tryGetTargetMember = new MemberRefUser(module, "TryGetTarget", tryGetTargetSig, weakRefTypeRef);

			CilBody getter = new CilBody();
			Local local = new Local(originalRefProp.PropertySig.RetType, "storedOriginal", 0);
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc_0.ToInstruction());	                                        // store in local
			getter.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(local));                                     // Load by reference
			getter.Instructions.Add(OpCodes.Call.ToInstruction(tryGetTargetMember));                            // Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.
			getter.Variables.Add(local);

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public | MethodAttributes.CompilerControlled);
			mDef.Body = getter;

			originalRefProp.GetMethod = mDef;
		}

		/// <summary>
		/// This creates the constructor of the Extensible class.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <param name="weakRefGenericInstance"></param>
		/// <returns></returns>
		public static MethodDefUser CreateConstructor(MirrorGenerator mirrorGenerator, FieldDefUser originalWeakRefFld, GenericInstSig weakRefGenericInstance) {
			ITypeDefOrRef weakRefTypeRef = weakRefGenericInstance.ToTypeDefOrRef();

			//MethodSig constructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalRefProp.PropertySig.RetType);
			MethodSig mirrorTypeConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MethodSig weakRefConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser weakRefConstructorRef = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", weakRefConstructorSignature, weakRefTypeRef);

			MethodDefUser ctor = new MethodDefUser(".ctor", mirrorTypeConstructorSignature, MethodAttributes.Family | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			CilBody ctorBody = new CilBody();
			ctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefConstructorRef));
			ctorBody.Instructions.Add(OpCodes.Stfld.ToInstruction(originalWeakRefFld));
			ctorBody.Instructions.Add(OpCodes.Ret.ToInstruction());
			ctor.Body = ctorBody;

			return ctor;
		}

		#endregion


	}
}
