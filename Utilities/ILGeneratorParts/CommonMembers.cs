using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Utilities.Representations;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.ILGeneratorParts {
	public static class CommonMembers {


		private static TypeSig typeTypeSig = null;
		private static TypeSig runtimeTypeHandleSig = null;
		private static TypeSig runtimeMethodHandleSig = null;
		private static ITypeDefOrRef typeTypeRef = null;
		private static TypeSig bindingFlagsSig = null;
		private static TypeSig methodInfoSig = null;
		private static TypeSig propertyInfoSig = null;
		private static TypeSig systemReflectionBinderSig = null;
		private static TypeSig typeArraySig = null;
		private static TypeSig paramModifierArraySig = null;
		private static TypeSig methodBaseSig = null;
		private static TypeSig delegateSig = null;
		private static MemberRefUser getType = null;
		private static MemberRefUser getTypeFromHandle = null;
		private static MemberRefUser getMethodFromHandle = null;
		private static MemberRefUser getMethod = null;
		private static MemberRefUser getProperty = null;

		/// <summary>
		/// Generates a member named <c>&lt;originalName&gt;IsCallerInInvocation</c>.
		/// </summary>
		/// <param name="mirrorGenerator">The mirror generator, used to get ahold of the boolean core lib type.</param>
		/// <param name="originalName">The name of the original method.</param>
		/// <returns></returns>
		public static FieldDefUser GenerateIsCallerInInvocation(MirrorGenerator mirrorGenerator, string originalName) {
			FieldDefUser isCallerInInvocation = new FieldDefUser($"<{originalName}>IsCallerInInvocation", new FieldSig(mirrorGenerator.MirrorModule.CorLibTypes.Boolean), MirrorGenerator.PRIVATE_FIELD_TYPE);
			isCallerInInvocation.IsSpecialName = true;
			isCallerInInvocation.IsRuntimeSpecialName = true;
			return isCallerInInvocation;
		}

		/// <summary>
		/// Generates a member named <c>&lt;orig_originalName&gt;ExtensibleCallback</c>.
		/// </summary>
		/// <param name="originalName">The name of the original method that the delegate represents.</param>
		/// <param name="delegateTypeSig">The type signature of the delegate.</param>
		/// <returns></returns>
		public static FieldDefUser GenerateDelegateHolder(string originalName, TypeSig delegateTypeSig) {
			FieldDefUser delegateHolder = new FieldDefUser($"<orig_{originalName}>ExtensibleCallback", new FieldSig(delegateTypeSig), MirrorGenerator.PRIVATE_FIELD_TYPE);
			delegateHolder.IsSpecialName = true;
			delegateHolder.IsRuntimeSpecialName = true;
			return delegateHolder;
		}

		/// <summary>
		/// Generates the body of a proxy.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="original"></param>
		/// <param name="paramTypes"></param>
		/// <param name="originalRef"></param>
		/// <param name="delegateOriginalHolder"></param>
		/// <param name="isCallerInInvocation"></param>
		/// <param name="hookEventInvoke"></param>
		/// <param name="originalRefProperty"></param>
		/// <returns></returns>
		public static CilBody GenerateProxyMethodBody(MirrorGenerator mirrorGenerator, MethodDef original, TypeSig[] paramTypes, MemberRef originalRef, FieldDefUser delegateOriginalHolder, FieldDefUser isCallerInInvocation, IMethodDefOrRef hookEventInvoke, PropertyDefUser originalRefProperty) {
			CilBody cil = new CilBody();

			// TO FUTURE XAN / MAINTAINERS:
			// This technique below looks weird (especially with it calling orig() *outside* of the null check, where it might throw NRE)
			// The catch is that this is exactly the path that should be taken.
			// A case where orig is null and IsInInvocation is true should be impossible, because that means it bypassed the binder somehow, which is not allowed.
			// This has been relayed by the explicit invalid op exception.

			Instruction ldarg0_First = OpCodes.Ldarg_0.ToInstruction();
			Instruction ldIsOrigNull = OpCodes.Ldloc_0.ToInstruction();
			Local isOrigNull = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "isOrigNull");
			cil.Variables.Add(isOrigNull);
			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldfld.ToInstruction(delegateOriginalHolder));
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
				cil.Instructions.Add(Hooks.OptimizedLdarg(i + 1));
			}
			OpCode callCode;
			if (!original.IsStatic) {
				callCode = OpCodes.Callvirt;
			} else {
				callCode = OpCodes.Call;
			}
			cil.Instructions.Add(callCode.ToInstruction(originalRef));

			cil.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			cil.Instructions.Add(OpCodes.Stfld.ToInstruction(isCallerInInvocation));

			cil.Instructions.Add(OpCodes.Ret.ToInstruction());
			//// }
			// Reminder: delegate field thing is on the stack right now.

			cil.Instructions.Add(ldIsOrigNull);
			cil.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(ldarg0_First));
			cil.Instructions.Add(OpCodes.Pop.ToInstruction()); // Get rid of the (null) delegate from the stack, it is no longer needed.
			cil.Instructions.Add(OpCodes.Ldstr.ToInstruction("Illegal state encountered: System detected it was in a BepInEx hook's orig() call, but the orig() method itself was null. This should not be possible unless something bypassed the Binder pipeline. Did you try to do anything manually?"));
			cil.Instructions.Add(OpCodes.Newobj.ToInstruction(ILGenerators.invalidOpExcCtor));
			cil.Instructions.Add(OpCodes.Throw.ToInstruction());

			cil.Instructions.Add(ldarg0_First);
			cil.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));
			for (int i = 0; i < paramTypes.Length; i++) {
				cil.Instructions.Add(Hooks.OptimizedLdarg(i + 1));
			}
			cil.Instructions.Add(OpCodes.Call.ToInstruction(hookEventInvoke));
			cil.Instructions.Add(OpCodes.Ret.ToInstruction());

			return cil;
		}

		/// <summary>
		/// Writes the code for a binder call manager, which is the method that gets hooked and redirects to the mirror where applicable.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="originalClassSig"></param>
		/// <param name="mirror"></param>
		/// <param name="binderType"></param>
		/// <param name="receiverImpl"></param>
		/// <param name="bieOrigStorageField"></param>
		/// <param name="hookEventInvoke"></param>
		/// <param name="tExtendsExtensible"></param>
		public static void ProgramBinderCallManager(MirrorGenerator mirrorGenerator, TypeSig originalClassSig, MethodDef mirror, TypeDefUser binderType, MethodDefUser receiverImpl, FieldDefUser bieOrigStorageField, IMethodDefOrRef hookEventInvoke, GenericVar tExtendsExtensible) {
			
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
			if (receiverImpl.HasReturnType) {
				Local returnValue = new Local(receiverImpl.ReturnType, "returnValue");
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
			body.Instructions.Add(OpCodes.Stfld.ToInstruction(bieOrigStorageField)); // Put that into the delegate slot. Consumes second dup
																							   // Call

			int nParams = receiverImpl.Parameters.Count(param => !param.IsReturnTypeParameter);
			for (int i = 2; i < nParams; i++) {
				// 0 = orig
				// 1 = self
				// 2... = args...
				body.Instructions.Add(Hooks.OptimizedLdarg(i));
			}

			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(mirror)); // Call. Consumes first dup.
			if (receiverImpl.HasReturnType) {
				// WAIT! If this is a returning method, the method pushed something onto the stack. Store it.
				body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			}

			// Unload, consumes the first ldloc_0 before the two dups
			body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			body.Instructions.Add(OpCodes.Stfld.ToInstruction(bieOrigStorageField)); // Erase the delegate slot. Consumes initial ldloc_0

			if (receiverImpl.HasReturnType) {
				// Load up that return value if we need to.
				body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
			}
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(nop);
			// Now if we make it here, it means that there was no bound type, we need to just directly call orig here and now.
			for (int i = 0; i < nParams; i++) {
				// 0... = orig, self, args...
				// To future Xan: This is not a method, there is no "this" that gets popped. Do not ldarg_0 before the call.
				body.Instructions.Add(Hooks.OptimizedLdarg(i));
			}
			body.Instructions.Add(OpCodes.Call.ToInstruction(hookEventInvoke));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			receiverImpl.Body = body;
		}

		private static void PrepareCachedSystemReflectionStuffs(MirrorGenerator mirrorGenerator) {
			typeTypeSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Type));
			typeTypeRef ??= mirrorGenerator.cache.Import(typeof(Type));
			runtimeTypeHandleSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(RuntimeTypeHandle));
			runtimeMethodHandleSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(RuntimeMethodHandle));
			bindingFlagsSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.BindingFlags));
			methodInfoSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.MethodInfo));
			propertyInfoSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.PropertyInfo));
			systemReflectionBinderSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.Binder));
			typeArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Type[]));
			paramModifierArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.ParameterModifier[]));
			methodBaseSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.MethodBase));
			delegateSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Delegate));

			getType ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetType", MethodSig.CreateInstance(typeTypeSig), mirrorGenerator.MirrorModule.CorLibTypes.Object.ToTypeDefOrRef());
			getTypeFromHandle ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetTypeFromHandle", MethodSig.CreateStatic(typeTypeSig, runtimeTypeHandleSig), typeTypeRef);
			getMethodFromHandle ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetMethodFromHandle", MethodSig.CreateStatic(methodInfoSig, runtimeMethodHandleSig), typeTypeRef);
			getMethod ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetMethod", MethodSig.CreateInstance(methodInfoSig, mirrorGenerator.MirrorModule.CorLibTypes.String, bindingFlagsSig, systemReflectionBinderSig, typeArraySig, paramModifierArraySig), typeTypeRef);
			getProperty ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetProperty", MethodSig.CreateInstance(propertyInfoSig, mirrorGenerator.MirrorModule.CorLibTypes.String, bindingFlagsSig), typeTypeRef);

		}

		// For methods
		public static void CreateConditionalBinderCodeBlock(MirrorGenerator mirrorGenerator, CilBody cctorBody, TypeDefUser binderType, MethodDefUser mirror, MethodDefUser receiverImpl, EventDef hook) {
			MethodSig delegateConstructorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object, mirrorGenerator.MirrorModule.CorLibTypes.IntPtr);
			MemberRefUser delegateConstructor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, (ITypeDefOrRef)mirrorGenerator.cache.Import(hook.EventType));

			GenericInstSig genericBinder = new GenericInstSig(binderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
			MemberRefUser genericBinderMtd = new MemberRefUser(mirrorGenerator.MirrorModule, mirror.Name, receiverImpl.MethodSig, genericBinder.ToTypeDefOrRef());

			// NEW: Only hook the ones we actually use.
			Instruction nop = new Instruction(OpCodes.Nop);
			PrepareCachedSystemReflectionStuffs(mirrorGenerator);

			if (cctorBody.Instructions.Count == 0) {
				// This is the top of the method. Store some stuff.
				Local extensibleType = new Local(typeTypeSig, "type");
				Local bindingFlags = new Local(bindingFlagsSig, "flags");
				cctorBody.Variables.Add(extensibleType);
				cctorBody.Variables.Add(bindingFlags);

				cctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				cctorBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getType));
				cctorBody.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
				cctorBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
				cctorBody.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			}

			TypeSig[] visibleParams = mirror.GetParams().Where(param => param != mirror.ReturnType).ToArray();

			cctorBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // type (this.)
			cctorBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(mirror.Name)); // method name
			cctorBody.Instructions.Add(OpCodes.Ldloc_1.ToInstruction()); // BindingFlags
			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction()); // binder
			cctorBody.Instructions.Add(Hooks.OptimizedLdc_I4(visibleParams.Length)); // num params...
			cctorBody.Instructions.Add(OpCodes.Newarr.ToInstruction(typeTypeRef)); // new Type[]
			if (visibleParams.Length > 0) {
				cctorBody.Instructions.Add(OpCodes.Dup.ToInstruction());
				for (int i = 0; i < visibleParams.Length; i++) {
					cctorBody.Instructions.Add(Hooks.OptimizedLdc_I4(i));
					cctorBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(visibleParams[i].ToTypeDefOrRef()));
					cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
					cctorBody.Instructions.Add(OpCodes.Stelem_Ref.ToInstruction());
					if (i < visibleParams.Length - 1) {
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
			cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(mirrorGenerator.cache.Import(hook.AddMethod)));
			//
			cctorBody.Instructions.Add(nop);
		}

		// For properties
		public static void CreateConditionalBinderCodeBlock(MirrorGenerator mirrorGenerator, CilBody cctorBody, TypeDefUser binderType, TypeDefUser getterDelegateType, TypeDefUser setterDelegateType, MethodDefUser getterImpl, MethodDefUser setterImpl, PropertyDefUser mirror, PropertyDef originalProperty) {
			/*
			MethodSig delegateConstructorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object, mirrorGenerator.MirrorModule.CorLibTypes.IntPtr);
			MemberRefUser genericGetter = null;
			MemberRefUser genericSetter = null;
			MemberRefUser getterDelegateCtor = null;
			MemberRefUser setterDelegateCtor = null;
			GenericInstSig genericBinder = new GenericInstSig(binderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
			
			if (getterImpl != null) {
				getterDelegateCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, getterDelegateType);
				genericGetter = new MemberRefUser(mirrorGenerator.MirrorModule, getterImpl.Name, getterImpl.MethodSig, genericBinder.ToTypeDefOrRef());
			}
			if (setterImpl != null) {
				setterDelegateCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, setterDelegateType);
				genericSetter = new MemberRefUser(mirrorGenerator.MirrorModule, setterImpl.Name, setterImpl.MethodSig, genericBinder.ToTypeDefOrRef());
			}
			*/
			GenericInstSig genericBinder = new GenericInstSig(binderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));

			Instruction nop = new Instruction(OpCodes.Nop);
			PrepareCachedSystemReflectionStuffs(mirrorGenerator);

			if (cctorBody.Instructions.Count == 0) {
				// This is the top of the method. Store some stuff.
				// Note that this shares the method with the method hook system so stuff for just the method hook system is relevant.
				Local extensibleType = new Local(typeTypeSig, "type");
				Local bindingFlags = new Local(bindingFlagsSig, "flags");
				cctorBody.Variables.Add(extensibleType);
				cctorBody.Variables.Add(bindingFlags);

				cctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				cctorBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getType));
				cctorBody.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
				cctorBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
				cctorBody.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
			}

			cctorBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // This
			cctorBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(mirror.Name)); // method name
			cctorBody.Instructions.Add(OpCodes.Ldloc_1.ToInstruction()); // BindingFlags
			cctorBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getProperty));
			cctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			cctorBody.Instructions.Add(OpCodes.Ceq.ToInstruction());
			cctorBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(nop));

			ITypeDefOrRef hookType = mirrorGenerator.cache.Import(typeof(Hook));
			MemberRefUser hookCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, methodBaseSig, delegateSig), hookType);

			// Property exists. Bind appropriate methods now.
			// RuntimeDetour has a Hook constructor that accepts: MethodBase from, Delegate to
			// It also has one for Delegate from, Delegate to
			if (getterImpl != null) {
				// Get from by referencing the member of the original property
				cctorBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalProperty.GetMethod.MakeMemberReference(mirrorGenerator)));
				cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				cctorBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(getterImpl.MakeMemberReference(mirrorGenerator, genericBinder.ToTypeDefOrRef(), false)));
				cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				cctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(hookCtor));
				cctorBody.Instructions.Add(OpCodes.Pop.ToInstruction());
			}
			if (setterImpl != null) {
				cctorBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalProperty.SetMethod.MakeMemberReference(mirrorGenerator)));
				cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				cctorBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(setterImpl.MakeMemberReference(mirrorGenerator, genericBinder.ToTypeDefOrRef(), false)));
				cctorBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				cctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(hookCtor));
				cctorBody.Instructions.Add(OpCodes.Pop.ToInstruction());
			}
			//
			cctorBody.Instructions.Add(nop);
		}
		/// <summary>
		/// Creates a new delegate type that has the provided method signature.
		/// </summary>
		/// <param name="mirrorGenerator">The mirror generator, for importing types.</param>
		/// <param name="signature">The signature of the method that the delegate represents.</param>
		/// <param name="name">The name of the delegate type.</param>
		/// <returns></returns>
		public static TypeDefUser CreateDelegateType(MirrorGenerator mirrorGenerator, MethodSig signature, string name) {
			TypeDefUser del = new TypeDefUser(name, mirrorGenerator.cache.Import(typeof(MulticastDelegate)));

			// The methods in the delegate have no body, which makes this very easy.
			MethodDefUser constructor = new MethodDefUser(
				".ctor", 
				MethodSig.CreateInstance(
					mirrorGenerator.MirrorModule.CorLibTypes.Void,
					mirrorGenerator.MirrorModule.CorLibTypes.Object,
					mirrorGenerator.MirrorModule.CorLibTypes.IntPtr
				), 
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig
			);

			List<TypeSig> types = new List<TypeSig>();
			IEnumerable<TypeSig> parameters = signature.Params;
			//if (signature.HasThis) parameters = parameters.Skip(1);
			parameters = parameters
				.Where(param => param != signature.RetType)
				.Select(param => mirrorGenerator.cache.Import(param));

			types.AddRange(parameters);
			types.Add(mirrorGenerator.cache.ImportAsTypeSig(typeof(AsyncCallback)));
			types.Add(mirrorGenerator.MirrorModule.CorLibTypes.Object);

			MethodDefUser invoke = new MethodDefUser(
				"Invoke",
				signature, 
				MethodAttributes.Public
			);

			TypeSig asyncResult = mirrorGenerator.cache.ImportAsTypeSig(typeof(IAsyncResult));
			MethodDefUser beginInvoke = new MethodDefUser(
				"BeginInvoke",
				MethodSig.CreateInstance(
					asyncResult,
					types.ToArray()
				),
				MethodAttributes.Public
			);
			
			MethodDefUser endInvoke = new MethodDefUser(
				"EndInvoke",
				MethodSig.CreateInstance(
					mirrorGenerator.MirrorModule.CorLibTypes.Object,
					asyncResult
				),
				MethodAttributes.Public
			);

			del.Methods.Add(constructor);
			del.Methods.Add(invoke);
			del.Methods.Add(beginInvoke);
			del.Methods.Add(endInvoke);
			return del;
		}

	}
}
