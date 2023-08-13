using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Utilities.Representations;
using HookGenExtender.Utilities.Shared;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BindingFlags = System.Reflection.BindingFlags;

namespace HookGenExtender.Utilities.ILGeneratorParts {
	public static class CommonMembers {


		internal static TypeSig typeTypeSig = null;
		internal static TypeSig runtimeTypeHandleSig = null;
		internal static TypeSig runtimeMethodHandleSig = null;
		internal static ITypeDefOrRef typeTypeRef = null;
		internal static ITypeDefOrRef methodBaseRef = null;
		internal static TypeSig bindingFlagsSig = null;
		internal static TypeSig methodAttributesSig = null;
		internal static TypeSig methodInfoSig = null;
		internal static TypeSig propertyInfoSig = null;
		internal static TypeSig constructorInfoSig = null;
		internal static TypeSig constructorInfoArraySig = null;
		internal static TypeSig systemReflectionBinderSig = null;
		internal static TypeSig typeArraySig = null;
		internal static TypeSig paramModifierArraySig = null;
		internal static TypeSig methodBaseSig = null;
		internal static TypeSig delegateSig = null;
		internal static TypeSig objectArraySig = null;
		internal static MemberRefUser getType = null;
		internal static MemberRefUser typeEquality = null;
		internal static MemberRefUser getTypeFromHandle = null;
		internal static MemberRefUser getMethodFromHandle = null;
		internal static MemberRefUser getMethod = null;
		internal static MemberRefUser getMethodSimple = null;
		internal static MemberRefUser getProperty = null;
		internal static MemberRefUser getPropertyGetter = null;
		internal static MemberRefUser getPropertySetter = null;
		internal static MemberRefUser getConstructor = null;
		internal static MemberRefUser getConstructors = null;
		internal static MemberRefUser ctorGetAttributes = null;
		internal static MemberRefUser ctorInfoInvoke = null;

		/// <summary>
		/// Generates a member named <c>&lt;originalName&gt;IsCallerInInvocation</c>.
		/// </summary>
		/// <param name="mirrorGenerator">The mirror generator, used to get ahold of the boolean core lib type.</param>
		/// <param name="originalName">The name of the original method.</param>
		/// <returns></returns>
		public static FieldDefUser GenerateIsCallerInInvocation(MirrorGenerator mirrorGenerator, string originalName) {
			// TO FUTURE XAN/MAINTAINERS: There are more instances of this string as seen below around the program. Remember to update them all if you change it.
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
			// TO FUTURE XAN/MAINTAINERS: There are more instances of this string as seen below around the program. Remember to update them all if you change it.
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
		public static CilBody GenerateProxyMethodBody(MirrorGenerator mirrorGenerator, MethodDef original, TypeSig[] paramTypes, MemberRef originalRef, IField delegateOriginalHolder, IField isCallerInInvocation, IMethodDefOrRef hookEventInvoke, PropertyDefUser originalRefProperty) {
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
		public static void ProgramBinderCallManager(MirrorGenerator mirrorGenerator, TypeSig originalClassSig, MethodDef mirror, TypeDefUser binderType, MethodDefUser receiverImpl, IField bieOrigStorageField, IMethodDefOrRef hookEventInvoke, GenericVar tExtendsExtensible) {

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

		/// <summary>
		/// This caches reflection-related fields.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		internal static void PrepareCachedSystemReflectionStuffs(MirrorGenerator mirrorGenerator) {
			typeTypeSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Type));
			typeTypeRef ??= mirrorGenerator.cache.Import(typeof(Type));
			methodBaseRef ??= mirrorGenerator.cache.Import(typeof(System.Reflection.MethodBase));
			runtimeTypeHandleSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(RuntimeTypeHandle));
			runtimeMethodHandleSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(RuntimeMethodHandle));
			bindingFlagsSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(BindingFlags));
			methodAttributesSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.MethodAttributes));
			methodInfoSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.MethodInfo));
			propertyInfoSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.PropertyInfo));
			systemReflectionBinderSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.Binder));
			typeArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Type[]));
			paramModifierArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.ParameterModifier[]));
			methodBaseSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.MethodBase));
			delegateSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(Delegate));
			constructorInfoSig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.ConstructorInfo));
			constructorInfoArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.ConstructorInfo[]));
			objectArraySig ??= mirrorGenerator.cache.ImportAsTypeSig(typeof(object[]));

			getType ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetType", MethodSig.CreateInstance(typeTypeSig), mirrorGenerator.MirrorModule.CorLibTypes.Object.ToTypeDefOrRef());
			getTypeFromHandle ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetTypeFromHandle", MethodSig.CreateStatic(typeTypeSig, runtimeTypeHandleSig), typeTypeRef);
			getMethodFromHandle ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetMethodFromHandle", MethodSig.CreateStatic(methodBaseSig, runtimeMethodHandleSig), methodBaseRef);
			getMethod ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetMethod", MethodSig.CreateInstance(methodInfoSig, mirrorGenerator.MirrorModule.CorLibTypes.String, bindingFlagsSig, systemReflectionBinderSig, typeArraySig, paramModifierArraySig), typeTypeRef);
			getMethodSimple ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetMethod", MethodSig.CreateInstance(methodInfoSig, mirrorGenerator.MirrorModule.CorLibTypes.String, bindingFlagsSig), typeTypeRef);
			getProperty ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetProperty", MethodSig.CreateInstance(propertyInfoSig, mirrorGenerator.MirrorModule.CorLibTypes.String, bindingFlagsSig), typeTypeRef);
			getPropertyGetter ??= new MemberRefUser(mirrorGenerator.MirrorModule, "get_GetMethod", MethodSig.CreateInstance(methodInfoSig), propertyInfoSig.ToTypeDefOrRef());
			getPropertySetter ??= new MemberRefUser(mirrorGenerator.MirrorModule, "get_SetMethod", MethodSig.CreateInstance(methodInfoSig), propertyInfoSig.ToTypeDefOrRef());
			getConstructor ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetConstructor", MethodSig.CreateInstance(constructorInfoSig, bindingFlagsSig, systemReflectionBinderSig, typeArraySig, paramModifierArraySig), typeTypeRef);
			getConstructors ??= new MemberRefUser(mirrorGenerator.MirrorModule, "GetConstructors", MethodSig.CreateInstance(constructorInfoArraySig, bindingFlagsSig), typeTypeRef);
			ctorGetAttributes ??= new MemberRefUser(mirrorGenerator.MirrorModule, "get_Attributes", MethodSig.CreateInstance(methodAttributesSig), constructorInfoSig.ToTypeDefOrRef());
			ctorInfoInvoke ??= new MemberRefUser(mirrorGenerator.MirrorModule, "Invoke", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Object, objectArraySig), constructorInfoSig.ToTypeDefOrRef());
			typeEquality ??= new MemberRefUser(mirrorGenerator.MirrorModule, "op_Equality", MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, typeTypeSig, typeTypeSig), typeTypeRef);
		}

		public static void PrependConstructorValidator(MirrorGenerator mirrorGenerator, CilBody createHooksBody) {
			const int constructorSearch = (int)(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
			const int hookMethodSearch = (int)(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);

			// This is the top of the method. Store some stuff.
			// These locals are used by the rest of the method body.
			Local extensibleType = new Local(typeTypeSig, "type");
			Local bindingFlags = new Local(bindingFlagsSig, "flags");
			Local constructors = new Local(constructorInfoArraySig, "constructors");
			Local index = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Int32, "index");
			createHooksBody.Variables.Add(extensibleType);
			createHooksBody.Variables.Add(bindingFlags);
			createHooksBody.Variables.Add(constructors);
			createHooksBody.Variables.Add(index);
			// STACK OFFSET:
			createHooksBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());          // -1
			createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getType));  // -1
			createHooksBody.Instructions.Add(OpCodes.Stloc_0.ToInstruction());          // -0
			createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4(hookMethodSearch));  // -1
			createHooksBody.Instructions.Add(OpCodes.Stloc_1.ToInstruction());          // -0

			// Now the ctor validator.
			// Get all constructors.
			Instruction nop = new Instruction(OpCodes.Nop);
			createHooksBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                  // -1
			createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4(constructorSearch));         // -2
			createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getConstructors));  // -1
			createHooksBody.Instructions.Add(OpCodes.Stloc_2.ToInstruction());                  // -0
			createHooksBody.Instructions.Add(OpCodes.Ldloc_2.ToInstruction());                  // -1
			createHooksBody.Instructions.Add(OpCodes.Ldlen.ToInstruction());                    // -1
			createHooksBody.Instructions.Add(OpCodes.Conv_I4.ToInstruction());                  // -1
			createHooksBody.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());                 // -2
			createHooksBody.Instructions.Add(OpCodes.Sub.ToInstruction());                      // -1
			createHooksBody.Instructions.Add(OpCodes.Stloc_S.ToInstruction(index));             // -0

			Instruction load0 = new Instruction(OpCodes.Ldc_I4_0);
			createHooksBody.Instructions.Add(load0);                                            // -1
			createHooksBody.Instructions.Add(OpCodes.Ldloc_S.ToInstruction(index));             // -2
			createHooksBody.Instructions.Add(OpCodes.Bgt_S.ToInstruction(nop));                 // -0
																								////
																								// Now a for loop. Unfortunately, I can *not* populate the constructor cache here, as the order is not guaranteed to be the same.
																								// This code assumes there is at least one constructor, hence why it is written as so.
			createHooksBody.Instructions.Add(OpCodes.Ldloc_2.ToInstruction());                                      // -1
			createHooksBody.Instructions.Add(OpCodes.Ldloc_S.ToInstruction(index));                                 // -2
			createHooksBody.Instructions.Add(OpCodes.Ldelem.ToInstruction(constructorInfoSig.ToTypeDefOrRef()));    // -1
			createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(ctorGetAttributes));                    // -1
			createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)System.Reflection.MethodAttributes.Private)); // -2
			createHooksBody.Instructions.Add(OpCodes.And.ToInstruction());                                          // -1
			createHooksBody.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());                                     // -2
			createHooksBody.Instructions.Add(OpCodes.Ceq.ToInstruction()); // If equal to 0, then it does not have private.	// -0
			Instruction loadIndex = OpCodes.Ldloc_S.ToInstruction(index);
			createHooksBody.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(loadIndex)); // So jump over the exception if it is private.
																						  ////
			createHooksBody.Instructions.Add(OpCodes.Ldstr.ToInstruction($"A constructor for your extensible type was not private. All constructors MUST be private to help enforce standards (you should never manually instantiate your extensible type).")); // +1
			createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(mirrorGenerator.CommonSignatures.General.InvalidOperationExceptionCtor));
			createHooksBody.Instructions.Add(OpCodes.Throw.ToInstruction());
			////
			// At this point the constructor is definitely valid. We can continue to the next iteration.
			createHooksBody.Instructions.Add(loadIndex);                            // -1
			createHooksBody.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());     // -2
			createHooksBody.Instructions.Add(OpCodes.Sub.ToInstruction());          // -1
			createHooksBody.Instructions.Add(OpCodes.Stloc_S.ToInstruction(index)); // -0
			createHooksBody.Instructions.Add(OpCodes.Br_S.ToInstruction(load0));    // -0
																					////
			createHooksBody.Instructions.Add(nop);
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
		public static void CreateConditionalBinderCodeBlock(MirrorGenerator mirrorGenerator, CilBody createHooksBody, TypeDefUser binderType, TypeRef originalTypeRef, TypeDefUser inUserType, TypeDefUser getterDelegate, TypeDefUser setterDelegate, MethodDefUser getterImpl, MethodDefUser setterImpl, PropertyDefUser mirror, PropertyDef originalProperty) {
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
			ITypeDefOrRef genericBinderRef = genericBinder.ToTypeDefOrRef();

			Instruction nop = new Instruction(OpCodes.Nop);
			PrepareCachedSystemReflectionStuffs(mirrorGenerator);

			createHooksBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // This
			createHooksBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(mirror.Name)); // method name
			createHooksBody.Instructions.Add(OpCodes.Ldloc_1.ToInstruction()); // BindingFlags
			createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getProperty));
			createHooksBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			createHooksBody.Instructions.Add(OpCodes.Ceq.ToInstruction());
			createHooksBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(nop));

			ITypeDefOrRef hookType = mirrorGenerator.cache.Import(typeof(Hook));
			MemberRefUser hookCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, methodBaseSig, methodInfoSig), hookType);
			//MemberRefUser hookCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, methodBaseSig, delegateSig), hookType);

			// Property exists. Bind appropriate methods now.
			// RuntimeDetour has a Hook constructor that accepts: MethodBase from, Delegate to
			// It also has one for Delegate from, Delegate to

			// BELOW: Technique is valid, but only on newer versions of C#.
			// RW (and a lot of Unity games) (probably) don't support this, so we have to do it the messy way.
			/*
			if (getterImpl != null) {
				// Get from by referencing the member of the original property
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalProperty.GetMethod.MakeMemberReference(mirrorGenerator)));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(getterImpl.MakeMemberReference(mirrorGenerator, genericBinder.ToTypeDefOrRef(), false)));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(hookCtor));
				createHooksBody.Instructions.Add(OpCodes.Pop.ToInstruction());
			}
			if (setterImpl != null) {
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalProperty.SetMethod.MakeMemberReference(mirrorGenerator)));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(setterImpl.MakeMemberReference(mirrorGenerator, genericBinder.ToTypeDefOrRef(), false)));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getMethodFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(hookCtor));
				createHooksBody.Instructions.Add(OpCodes.Pop.ToInstruction());
			}
			*/
			MethodSig delegateConstructorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.Object, mirrorGenerator.MirrorModule.CorLibTypes.IntPtr);
			
			if (getterImpl != null) {
				// Get from by referencing the member of the original property
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalTypeRef));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(originalProperty.Name));
				createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)));
				createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getProperty));
				createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getPropertyGetter));

				
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(genericBinder.ToTypeDefOrRef()));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(getterImpl.Name));
				createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)));
				createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getMethodSimple));
				
				/*
				MemberRefUser delegateConstructor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, getterDelegate);
				MemberRef genericGetterImpl = getterImpl.MakeMemberReference(mirrorGenerator, genericBinderRef, false);
				createHooksBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
				createHooksBody.Instructions.Add(OpCodes.Ldftn.ToInstruction(genericGetterImpl));
				createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(delegateConstructor));
				createHooksBody.Instructions.Add(OpCodes.Castclass.ToInstruction(delegateSig.ToTypeDefOrRef()));
				*/
				createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(hookCtor));
				createHooksBody.Instructions.Add(OpCodes.Pop.ToInstruction());
			}
			if (setterImpl != null) {
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalTypeRef));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(originalProperty.Name));
				createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)));
				createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getProperty));
				createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getPropertySetter));

				
				createHooksBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(genericBinder.ToTypeDefOrRef()));
				createHooksBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
				createHooksBody.Instructions.Add(OpCodes.Ldstr.ToInstruction(setterImpl.Name));
				createHooksBody.Instructions.Add(Hooks.OptimizedLdc_I4((int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)));
				createHooksBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(getMethodSimple));
				
				/*
				MemberRefUser delegateConstructor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", delegateConstructorSig, setterDelegate);
				MemberRef genericSetterImpl = setterImpl.MakeMemberReference(mirrorGenerator, genericBinderRef, false);
				createHooksBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
				createHooksBody.Instructions.Add(OpCodes.Ldftn.ToInstruction(genericSetterImpl));
				createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(delegateConstructor));
				createHooksBody.Instructions.Add(OpCodes.Castclass.ToInstruction(delegateSig.ToTypeDefOrRef()));
				*/
				createHooksBody.Instructions.Add(OpCodes.Newobj.ToInstruction(hookCtor));
				createHooksBody.Instructions.Add(OpCodes.Pop.ToInstruction());
			}

			//
			createHooksBody.Instructions.Add(nop);
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
			del.IsSealed = true;

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
			constructor.CodeType = MethodImplAttributes.Runtime;

			List<TypeSig> types = new List<TypeSig>();
			IEnumerable<TypeSig> parameters = signature.Params;
			//if (signature.HasThis) parameters = parameters.Skip(1);
			parameters = parameters
				.Where(param => param != signature.RetType)
				.Select(param => mirrorGenerator.cache.Import(param));

			types.AddRange(parameters);
			types.Add(mirrorGenerator.cache.ImportAsTypeSig(typeof(AsyncCallback)));
			types.Add(mirrorGenerator.MirrorModule.CorLibTypes.Object);

			const MethodAttributes commonAttrs = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;

			MethodDefUser invoke = new MethodDefUser(
				"Invoke",
				signature,
				commonAttrs
			);
			invoke.CodeType = MethodImplAttributes.Runtime;

			TypeSig asyncResult = mirrorGenerator.cache.ImportAsTypeSig(typeof(IAsyncResult));
			MethodDefUser beginInvoke = new MethodDefUser(
				"BeginInvoke",
				MethodSig.CreateInstance(
					asyncResult,
					types.ToArray()
				),
				commonAttrs
			);
			beginInvoke.CodeType = MethodImplAttributes.Runtime;

			MethodDefUser endInvoke = new MethodDefUser(
				"EndInvoke",
				MethodSig.CreateInstance(
					mirrorGenerator.MirrorModule.CorLibTypes.Object,
					asyncResult
				),
				commonAttrs
			);
			endInvoke.CodeType = MethodImplAttributes.Runtime;

			del.Methods.Add(constructor);
			del.Methods.Add(invoke);
			del.Methods.Add(beginInvoke);
			del.Methods.Add(endInvoke);
			return del;
		}

		public static MethodDefUser[] CreateBinderMethodBodies(MirrorGenerator mirrorGenerator, TypeDef originalType, TypeDef binderType, MemberRef constructorCache, TypeSig originalImported, MemberRefUser originalWeakTypeField, ITypeDefOrRef instancesGeneric, MemberRef instancesField, MemberRef hasHookedInstance, MemberRef createHooksInstance) {
			GenericInstSig originalWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, originalImported);
			GenericInstSig extensibleWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));

			// Instruction nop = new Instruction(OpCodes.Nop);
			ITypeDefOrRef extensibleType = new GenericVar(0).ToTypeDefOrRef();
			ITypeDefOrRef originalTypeWeakRef = originalWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef extensibleTypeWeakReference = extensibleWeakRef.ToTypeDefOrRef();

			SharedGeneralSignatures signatures = mirrorGenerator.CommonSignatures.General;
			MemberRefUser cwtTryGetValue = signatures.ReferenceCWTTryGetValue(instancesGeneric);
			MemberRefUser cwtRemove = signatures.ReferenceCWTRemove(instancesGeneric);
			MemberRefUser cwtAdd = signatures.ReferenceCWTAdd(instancesGeneric);
			MemberRefUser setTarget = signatures.ReferenceWeakRefSetTarget(originalTypeWeakRef);
			MemberRefUser weakRefCtor = signatures.ReferenceWeakRefCtor(extensibleTypeWeakReference);

			// Before this can start, I need to get a list of every constructor of the original type.
			List<MethodDef> ctors = originalType.FindConstructors().ToList();

			// ENFORCE THIS:
			// 0 is ALWAYS the default ctor.
			ctors.Insert(0, new MethodDefUser(".ctor", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void), MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Public));

			// Now, a Bind method must be generated for each constructor.
			// Each Bind method will search for an Extensible counterpart
			// Take, for example, the constructor signature of Oracle:
			// public Oracle(PhysicalObject ow) { }
			// The Bind method will try to find the following:
			// private ExtensibleOracle(Oracle @this, PhysicalObject ow) { }
			// (Note that the ctor must be private!)

			// These should be cached, so let's make a field to do that.

			CilBody cctor = binderType.FindOrCreateStaticConstructor().Body;


			cctor.Instructions.Add(Hooks.OptimizedLdc_I4(ctors.Count));
			cctor.Instructions.Add(OpCodes.Newarr.ToInstruction(constructorInfoSig.ToTypeDefOrRef()));
			cctor.Instructions.Add(OpCodes.Stsfld.ToInstruction(constructorCache));
			cctor.Instructions.Add(OpCodes.Ret.ToInstruction());

			int ctorIndex = 0;
			MethodDefUser[] result = new MethodDefUser[ctors.Count];
			foreach (MethodDef constructor in ctors) {
				constructor.SelectParameters(mirrorGenerator, out TypeSig[] inputParams, out _, out _, true);
				TypeSig[] allInputParams = new TypeSig[inputParams.Length + 1];
				allInputParams[0] = originalImported;
				for (int i = 0; i < inputParams.Length; i++) {
					allInputParams[i + 1] = inputParams[i];
				}

				MethodSig bindSig = MethodSig.CreateStatic(extensibleWeakRef, allInputParams);
				MethodDefUser bind = new MethodDefUser("Bind", bindSig, MethodAttributes.Public | MethodAttributes.Static);
				// MethodDefUser internalBind = new MethodDefUser("BindInternal", bindSig, MethodAttributes.PrivateScope | MethodAttributes.Static);
				bind.SetParameterName(0, "toObject");
				// internalBind.SetParameterName(0, "toObject");
				Parameter[] @params = constructor.Parameters.Where(param => !param.IsHiddenThisParameter && !param.IsReturnTypeParameter).ToArray();
				for (int i = 0; i < @params.Length; i++) {
					bind.SetParameterName(i + 1, @params[i].Name);
					// internalBind.SetParameterName(i + 1, @params[i].Name);
				}
				result[ctorIndex] = bind;

				// Before anything, idiot-proof the function (this also prevents a serious bug later down the line in the event hooks).
				CilBody body = new CilBody();
				
				//if (ctorIndex > 0) {
				Local instance = new Local(new GenericVar(0), "instance");
				Local ctor = new Local(constructorInfoSig, "constructor");
				Local instanceType = new Local(typeTypeSig, "type");
				body.Variables.Add(instance);
				body.Variables.Add(ctor);
				body.Variables.Add(instanceType);

				Instruction loadInstances = OpCodes.Ldsfld.ToInstruction(instancesField);
				body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(instance.Type.ToTypeDefOrRef()));
				body.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
				body.Instructions.Add(OpCodes.Stloc_2.ToInstruction());

				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				body.Instructions.Add(OpCodes.Callvirt.ToInstruction(getType));
				body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(originalImported.ToTypeDefOrRef()));
				body.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
				body.Instructions.Add(OpCodes.Call.ToInstruction(typeEquality));
				body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(loadInstances));
				/////
				body.Instructions.Add(OpCodes.Ldstr.ToInstruction("Invalid attempt to call Bind with an inherited type. The type passed into bind must *exactly* match the original counterpart to the type that the Extensible class extends."));
				body.Instructions.Add(OpCodes.Newobj.ToInstruction(signatures.InvalidOperationExceptionCtor));
				body.Instructions.Add(OpCodes.Throw.ToInstruction());
				/////
				body.Instructions.Add(loadInstances);
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
				body.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtTryGetValue));
				body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
				body.Instructions.Add(OpCodes.Ceq.ToInstruction());
				Instruction throwMsg = new Instruction(OpCodes.Ldstr, $"Duplicate binding! Only one instance of your current Extensible type can be bound to an instance of type {originalImported.GetName()} at a time. In general, you should explicitly call TryReleaseCurrentBinding to free the binding immediately rather than waiting on the chance of garbage collection.");
				body.Instructions.Add(OpCodes.Brfalse.ToInstruction(throwMsg));
				// Stack is empty here (and after the branch jump).

				// CTOR CALL GOES HERE //

				// Start by finding it:
				body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(constructorCache));
				body.Instructions.Add(Hooks.OptimizedLdc_I4(ctorIndex));
				// Check if it exists first.
				//Instruction preCallCtor = OpCodes.Nop.ToInstruction(); // This was loadConstructor and was ldloc1
				Instruction loadConstructor = OpCodes.Ldloc_1.ToInstruction();
				body.Instructions.Add(OpCodes.Ldelem.ToInstruction(constructorInfoSig.ToTypeDefOrRef()));
				body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
				body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
				body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
				body.Instructions.Add(OpCodes.Ceq.ToInstruction());
				body.Instructions.Add(OpCodes.Brfalse.ToInstruction(loadConstructor)); // Jump to the call, the cache already contains a value.
																							   // ^ stack has no elements here.


				// Load the type of the mirror.
				body.Instructions.Add(OpCodes.Ldloc_2.ToInstruction()); // Type

				const int ctorSearchFlags = (int)(BindingFlags.NonPublic | BindingFlags.Instance);
				int effectiveCtorSearchFlags = ctorSearchFlags;
				if (ctorIndex > 0) {
					effectiveCtorSearchFlags |= (int)BindingFlags.DeclaredOnly; // index 0 can be inherited.
				}
				body.Instructions.Add(Hooks.OptimizedLdc_I4(effectiveCtorSearchFlags)); // bindingFlags
				body.Instructions.Add(OpCodes.Ldnull.ToInstruction()); // binder

				// below: types
				body.Instructions.Add(Hooks.OptimizedLdc_I4(allInputParams.Length));
				body.Instructions.Add(OpCodes.Newarr.ToInstruction(typeTypeRef));
				if (allInputParams.Length > 0) {
					body.Instructions.Add(OpCodes.Dup.ToInstruction());
					for (int i = 0; i < allInputParams.Length; i++) {
						body.Instructions.Add(Hooks.OptimizedLdc_I4(i));
						body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(allInputParams[i].ToTypeDefOrRef()));
						body.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
						body.Instructions.Add(OpCodes.Stelem_Ref.ToInstruction());
						if (i < allInputParams.Length - 1) {
							body.Instructions.Add(OpCodes.Dup.ToInstruction());
						}
					}
				}

				body.Instructions.Add(OpCodes.Ldnull.ToInstruction()); // modifiers
				body.Instructions.Add(OpCodes.Callvirt.ToInstruction(getConstructor)); // ctorinfo on stack now, with the array and index preceeding it in that order
																							   // body.Instructions.Add(OpCodes.Box.ToInstruction(constructorInfoSig.ToTypeDefOrRef()));
				body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
				body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
				body.Instructions.Add(OpCodes.Ldnull.ToInstruction()); // Load null
				body.Instructions.Add(OpCodes.Ceq.ToInstruction()); // ctor == null?
				Instruction loadCtorInfo = OpCodes.Ldsfld.ToInstruction(constructorCache);
				body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(loadCtorInfo)); // False: store, true continue
																							  ////
				body.Instructions.Add(OpCodes.Ldstr.ToInstruction("Illegal class setup. To use any given version of Binder<T>.Bind() you must declare an equal constructor (i.e. to use Bind(original, int, float) you must declare a *private* constructor in your type YourType(original, int, float) : base(original) { ... }"));
				body.Instructions.Add(OpCodes.Newobj.ToInstruction(mirrorGenerator.CommonSignatures.General.InvalidOperationExceptionCtor));
				body.Instructions.Add(OpCodes.Throw.ToInstruction());
				////

				body.Instructions.Add(loadCtorInfo);
				body.Instructions.Add(Hooks.OptimizedLdc_I4(ctorIndex));
				body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
				body.Instructions.Add(OpCodes.Stelem.ToInstruction(constructorInfoSig.ToTypeDefOrRef()));

				/////////////////////////
				///

				// BELOW: Shame this doesn't work.
				/*
				MethodSig ctorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, allInputParams);
				MemberRefUser instanceCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", ctorSig, extensibleType);

				body.Instructions.Add(preCallCtor);
				for (int i = 0; i < allInputParams.Length; i++) {
					body.Instructions.Add(Hooks.OptimizedLdarg(i));		
				}
				body.Instructions.Add(OpCodes.Newobj.ToInstruction(instanceCtor));
				body.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
				*/

				body.Instructions.Add(loadConstructor); // ctor info.
																// ctorInfo.Invoke(params object[] args);
				body.Instructions.Add(Hooks.OptimizedLdc_I4(allInputParams.Length));
				body.Instructions.Add(OpCodes.Newarr.ToInstruction(mirrorGenerator.MirrorModule.CorLibTypes.Object));
				if (allInputParams.Length > 0) {
					body.Instructions.Add(OpCodes.Dup.ToInstruction());
					for (int i = 0; i < allInputParams.Length; i++) {
						body.Instructions.Add(Hooks.OptimizedLdc_I4(i));
						body.Instructions.Add(Hooks.OptimizedLdarg(i));
						body.Instructions.Add(OpCodes.Stelem_Ref.ToInstruction());
						if (i < allInputParams.Length - 1) {
							body.Instructions.Add(OpCodes.Dup.ToInstruction());
						}
					}
				}
				body.Instructions.Add(OpCodes.Callvirt.ToInstruction(ctorInfoInvoke));
				body.Instructions.Add(OpCodes.Castclass.ToInstruction(instance.Type.ToTypeDefOrRef()));
				body.Instructions.Add(OpCodes.Stloc_0.ToInstruction());

				body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
				body.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtAdd));
				body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
				body.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefCtor)); // Need to remember this value here.

				// Create hooks real quick, if needed.
				Instruction ret = new Instruction(OpCodes.Ret);
				//Instruction nop = new Instruction(OpCodes.Nop);
				body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(hasHookedInstance));
				body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
				body.Instructions.Add(OpCodes.Beq_S.ToInstruction(ret)); // was nop

				body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
				body.Instructions.Add(OpCodes.Call.ToInstruction(createHooksInstance));
				body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
				body.Instructions.Add(OpCodes.Stsfld.ToInstruction(hasHookedInstance));

				/*
				publicBody.Instructions.Add(nop);
				//if (binderType.DeclaringType.BaseType is TypeDefUser baseExtensible) { 
				if (originalType.BaseType is TypeDef td) {
					if (mirrorGenerator.mirrorLookup.TryGetValue(td, out (TypeDefUser, TypeDefUser, TypeRef) data)) {
						TypeDefUser superBinder = data.Item2;
						if (superBinder != null) {
							// Load all args.
							if (allInputParams.Length > 0) {
								for (int i = 0; i < allInputParams.Length; i++) {
									publicBody.Instructions.Add(Hooks.OptimizedLdarg(i));
								}
							}

							GenericInstSig parentBinder = new GenericInstSig(
									superBinder.ToTypeSig().ToClassOrValueTypeSig(),
									extensibleType.ToTypeSig()
								);
							MemberRefUser bindInternalRef = new MemberRefUser(
									mirrorGenerator.MirrorModule,
									internalBind.Name,
									MethodSig.CreateStatic(extensibleWeakRef, allInputParams),
									parentBinder.ToTypeDefOrRef()
								);
							publicBody.Instructions.Add(OpCodes.Call.ToInstruction(bindInternalRef));
						}
					}
				}
				publicBody.Instructions.Add(OpCodes.Ret.ToInstruction());
				*/
				body.Instructions.Add(ret);

				body.Instructions.Add(throwMsg);
				body.Instructions.Add(OpCodes.Newobj.ToInstruction(signatures.InvalidOperationExceptionCtor));
				body.Instructions.Add(OpCodes.Throw.ToInstruction());

				bind.Body = body;
				/*
				} else {
					// Default constructor, this has a different (simpler) body.
					CilBody internalBody = new CilBody();
					Local instance = new Local(new GenericVar(0), "instance");
					Local instanceType = new Local(typeTypeSig, "type");
					internalBody.Variables.Add(instance);
					internalBody.Variables.Add(instanceType);

					internalBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(instance.Type.ToTypeDefOrRef()));
					internalBody.Instructions.Add(OpCodes.Call.ToInstruction(getTypeFromHandle));
					internalBody.Instructions.Add(OpCodes.Stloc_1.ToInstruction());

					internalBody.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
					internalBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
					internalBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtTryGetValue));
					internalBody.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Ceq.ToInstruction());
					Instruction throwMsg = new Instruction(OpCodes.Ldstr, $"Duplicate binding! Only one instance of your current Extensible type can be bound to an instance of type {originalImported.GetName()} at a time. In general, you should explicitly call TryReleaseCurrentBinding to free the binding immediately rather than waiting on the chance of garbage collection.");
					internalBody.Instructions.Add(OpCodes.Brfalse.ToInstruction(throwMsg));

					MemberRefUser createInstance = new MemberRefUser(mirrorGenerator.MirrorModule, "CreateInstance", MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Object, typeTypeSig, mirrorGenerator.MirrorModule.CorLibTypes.Boolean), mirrorGenerator.cache.Import(typeof(Activator)));
					//Activator.CreateInstance(typeof(object), true);
					internalBody.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Call.ToInstruction(createInstance));
					internalBody.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
					
					internalBody.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
					internalBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtAdd));
					internalBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefCtor)); // Need to remember this value here.

					// Create hooks real quick, if needed.
					//Instruction ret = new Instruction(OpCodes.Ret);
					internalBody.Instructions.Add(OpCodes.Ldsfld.ToInstruction(hasHookedInstance));
					internalBody.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Beq_S.ToInstruction(nop)); // was ret

					internalBody.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Call.ToInstruction(createHooksInstance));
					internalBody.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
					internalBody.Instructions.Add(OpCodes.Stsfld.ToInstruction(hasHookedInstance));

					internalBody.Instructions.Add(nop);
					//if (binderType.DeclaringType.BaseType is TypeDefUser baseExtensible) { 
					if (originalType.BaseType is TypeDef td) {
						if (mirrorGenerator.mirrorLookup.TryGetValue(td, out (TypeDefUser, TypeDefUser, TypeRef) data)) {
							TypeDefUser superBinder = data.Item2;
							if (superBinder != null) {
								internalBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
								GenericInstSig parentBinder = new GenericInstSig(
									superBinder.ToTypeSig().ToClassOrValueTypeSig(),
									extensibleType.ToTypeSig()
								);
								MemberRefUser bindInternalRef = new MemberRefUser(
									mirrorGenerator.MirrorModule,
									internalBind.Name,
									MethodSig.CreateStatic(extensibleWeakRef, originalImported),
									parentBinder.ToTypeDefOrRef()
								);
								internalBody.Instructions.Add(OpCodes.Call.ToInstruction(bindInternalRef));
							}
						}
					}
					internalBody.Instructions.Add(OpCodes.Ret.ToInstruction());

					internalBody.Instructions.Add(throwMsg);
					internalBody.Instructions.Add(OpCodes.Newobj.ToInstruction(signatures.InvalidOperationExceptionCtor));
					internalBody.Instructions.Add(OpCodes.Throw.ToInstruction());

					internalBind.Body = internalBody;
				}
				*/
				ctorIndex++;
			}
			return result;
		}
	}
}
