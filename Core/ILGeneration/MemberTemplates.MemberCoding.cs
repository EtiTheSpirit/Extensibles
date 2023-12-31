﻿using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		/// <summary>
		/// To be called on the second iteration of the generator, this completes the code of the Extensible type's constructor.
		/// This is late-bound because it must load the definition of the superclass's constructor for the base() call.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		public static void LateWriteConstructorOfCoreMbr(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers) {
			ExtensibleTypeData type = coreMembers.type;
			MethodDefAndRef constructor = coreMembers.constructor;
			PropertyDefAndRef original = coreMembers.originalObjectProxy;
			FieldDefAndRef storage = coreMembers.originalObjectWeakReference;
			GenericInstanceTypeDef weakRefType = coreMembers.weakReferenceType;
			MemberRef newWeakReference = weakRefType.ReferenceExistingMethod(".ctor", main.Shared.WeakReferenceCtorSig);
			CilBody ctorBody = constructor.GetOrCreateBody();
			// WAIT: Do we have a base constructor?
			if (main.TryGetParent(coreMembers.type, out ExtensibleTypeData parent)) {
				// Yes!
				MethodDefAndRef ctor = parent.ExtensibleStandardConstructor;
				ctorBody.EmitThis();
				ctorBody.EmitLdarg(1);
				ctorBody.EmitCall(ctor.Definition);
			}
			ctorBody.EmitThis();
			ctorBody.Emit(OpCodes.Ldarg_1);                     // original
			ctorBody.EmitNew(newWeakReference);                 // new WeakReference<T>(original)
			ctorBody.Emit(OpCodes.Stfld, storage.Definition);   // this.<Extensible>original = ^
			ctorBody.EmitRet();
			constructor.FinalizeMethodBody(main);
		}

		/// <summary>
		/// Initializes the common contents of an extensible type. This will write the code of its property named "Original".
		/// <para/>
		/// <strong>This will NOT write the constructor.</strong>
		/// </summary>
		private static void CodeExtensibleCoreMembers(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers) {
			ExtensibleTypeData type = coreMembers.type;
			MethodDefAndRef constructor = coreMembers.constructor;
			PropertyDefAndRef original = coreMembers.originalObjectProxy;
			FieldDefAndRef storage = coreMembers.originalObjectWeakReference;
			GenericInstanceTypeDef weakRefType = coreMembers.weakReferenceType;

			#region Original
			MemberRef tryGetTarget = weakRefType.ReferenceExistingMethod("TryGetTarget", main.Shared.WeakRefTryGetTargetSig);
			CilBody getterBody = original.Getter.Definition.Body = new CilBody();
			Local result = new Local(type.ImportedGameTypeSig, "result");
			getterBody.SetLocals(result);

			getterBody.EmitThis();
			getterBody.Emit(OpCodes.Ldfld, storage.Definition);	// this.<Extensible>original
			getterBody.EmitLdloc(result, true);		// out original
			getterBody.EmitCallvirt(tryGetTarget);				// TryGetTarget
			getterBody.Emit(OpCodes.Pop);                       // Pop (true/false result)
			getterBody.EmitLdloc(result, false);		// original
			getterBody.EmitRet();                               // return ^
			original.Getter.FinalizeMethodBody(main);
			#endregion
		}

		/// <summary>
		/// Initializes the common contents of the binder. This will write the code code of all members, with
		/// the exception of <c>CreateBindings</c> which must be coded in parts - only the initializing code to set
		/// up locals will be written, but the method will not be ended.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="binderMembers"></param>
		private static void CodeBinderCoreMembers(ExtensiblesGenerator main, in ExtensibleBinderCoreMembers binderMembers) {
			ExtensibleTypeData type = binderMembers.type;
			FieldDefAndRef bindings = binderMembers.bindingsField;
			FieldDefAndRef hasCreatedBindings = binderMembers.hasCreatedBindingsField;
			FieldDefAndRef ctorCache = binderMembers.constructorCacheField;
			MethodDefAndRef createBindings = binderMembers.createBindingsMethod;
			MethodDefAndRef tryReleaseBinding = binderMembers.tryReleaseBindingMethod;
			MethodDefAndRef tryGetBinding = binderMembers.tryGetBindingMethod;
			GenericInstanceTypeDef bindingsFieldType = binderMembers.cwtInstanceDef;

			string binderTypeName =  binderMembers.type.Binder.Reference.FullName;

			#region cctor

			#region Initialize Variables
			type.Binder.Underlying.IsBeforeFieldInit = true;
			CilBody cctor = type.Binder.StaticConstructor.Body = new CilBody();
			Local extensibleType = new Local(main.Shared.TypeSig, "tExtensible");
			Local constructors = new Local(main.Shared.ConstructorInfoArraySig, "constructors");
			Local ctorForLoopIndex = new Local(main.CorLibTypeSig<int>(), "index");
			Local ctorForLoopLength = new Local(main.CorLibTypeSig<int>(), "length");
			cctor.SetLocals(extensibleType, constructors, ctorForLoopIndex, ctorForLoopLength);
			#endregion

			cctor.EmitTypeof(main, CommonGenericArgs.TYPE_ARG_0_REF);                       // typeof(TExtensible)
			cctor.EmitStloc(extensibleType); // Used in the "Ensure all constructors are private" block

			cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, $"[Extensibles] Initializing Binder<"),
				(body, main) => {
					body.EmitLdloc(extensibleType);
					body.EmitCallvirt(main.Shared.Type_get_FullName);
				},
				(body, main) => body.Emit(OpCodes.Ldstr, ">...")
			});
			cctor.EmitUnityDbgLog(main, null);

			#region Bindings Field
			// Start by setting up the bindings field.
			cctor.EmitNew(bindingsFieldType.ReferenceExistingMethod(".ctor", main.Shared.CWTCtorSig));
			cctor.Emit(OpCodes.Stsfld, bindings.Reference);
			cctor.EmitLdc_I4(binderMembers.type._originalGameType.FindInstanceConstructors().Count() + 1);
			cctor.Emit(OpCodes.Newarr, main.Shared.ConstructorInfoReference);
			cctor.Emit(OpCodes.Stsfld, ctorCache.Reference);
			#endregion

			// Now some validation
			#region cctor :: Ensure provided type parameter is not abstract.
			// extensibleType = typeof(TExtensible), duplicate for re-use.
			cctor.EmitLdloc(extensibleType);
			cctor.EmitCallvirt(main.Shared.Type_get_IsAbstract);
			
			Instruction getIsSealed = cctor.NewBrDest();
			cctor.Emit(OpCodes.Brfalse_S, getIsSealed);

			cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, "[Extensibles] Extensible type {"),
				(body, main) => {
					body.EmitLdloc(extensibleType);
					body.EmitCallvirt(main.Shared.Type_get_FullName);
				},
				(body, main) => body.Emit(OpCodes.Ldstr, "} is abstract. This is not allowed; you can only bind to extensible types that are not abstract.")
			});
			cctor.EmitInvalidOpException(main, null);
			////////////////////////////////////////////////

			cctor.Emit(getIsSealed);
			cctor.EmitLdloc(extensibleType);
			cctor.EmitCallvirt(main.Shared.Type_get_IsSealed);
			Instruction endOfStub_LoadCtorBindingFlags = cctor.NewBrDest();
			cctor.Emit(OpCodes.Brtrue_S, endOfStub_LoadCtorBindingFlags); // Go to "Ensure all constructors are private" block.

			// CASE: Extensible type is not sealed.
#if ASSERT_EXTENSIBLE_TYPES_ARE_SEALED
			const string ctorTypeSealMessage = "This is not allowed; the behavior of inherited types is undefined.";
#else
			const string ctorTypeSealMessage = "It is STRONGLY RECOMMENDED to seal extensible types - inheritence may cause unpredictable bugs and unexpected behavior.";
#endif
			cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, "[Extensibles] Extensible type {"),
				(body, main) => {
					body.EmitLdloc(extensibleType);
					body.EmitCallvirt(main.Shared.Type_get_FullName);
				},
				(body, main) => body.Emit(OpCodes.Ldstr, $"}} is not sealed. {ctorTypeSealMessage}")
			});
#if ASSERT_EXTENSIBLE_TYPES_ARE_SEALED
			cctor.EmitInvalidOpException(main, null);
#else
			cctor.EmitUnityDbgLogWarning(main, null);
#endif
			///////////////////////////////////////

			#endregion

			#region cctor :: Ensure all constructors are private.
			cctor.Emit(endOfStub_LoadCtorBindingFlags);
			cctor.EmitLdloc(extensibleType);
			cctor.EmitLdc_I4((int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
			cctor.EmitCallvirt(main.Shared.GetConstructors);                                                    // .GetConstructors()
			cctor.EmitStoreThenLoad(constructors);
			cctor.Emit(OpCodes.Ldlen);                                                                          // constructors.Length
			cctor.Emit(OpCodes.Conv_I4);
			cctor.EmitStloc(ctorForLoopLength);
			cctor.EmitLdc_I4(0);
			cctor.EmitStloc(ctorForLoopIndex);
			cctor.EmitForLoopHead(ctorForLoopIndex, ctorForLoopLength, out Instruction @continue, out Instruction @break);
			{
				cctor.EmitLdloc(constructors);
				cctor.EmitLdloc(ctorForLoopIndex);
				cctor.Emit(OpCodes.Ldelem, main.Shared.ConstructorInfoReference);
				cctor.EmitCallvirt(main.Shared.MethodBase_get_Attributes);
				// cctor.Emit(OpCodes.Conv_I4);
				/*
				cctor.EmitLdc_I4((int)System.Reflection.MethodAttributes.Private);
				cctor.Emit(OpCodes.And);
				cctor.EmitLdc_I4((int)System.Reflection.MethodAttributes.Private);
				*/
				Instruction endOfLoop = cctor.NewBrDest();
				cctor.EmitHasFlag((int)System.Reflection.MethodAttributes.Private);
				cctor.Emit(OpCodes.Brtrue, endOfLoop);	// TO FUTURE XAN: Do not jump to @continue here, it bricks the loop.
														// Reason: You have to reach the code emitted by EmitForLoopTail
				cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
					(body, main) => body.Emit(OpCodes.Ldstr, "[Extensibles] Constructor {"),
					(body, main) => {
						cctor.EmitLdloc(constructors);
						cctor.EmitLdloc(ctorForLoopIndex);
						cctor.Emit(OpCodes.Ldelem, main.Shared.ConstructorInfoReference);
						cctor.EmitCallvirt(main.Shared.ToStringReference);
					},
					(body, main) => body.Emit(OpCodes.Ldstr, "} of type {"),
					(body, main) => {
						cctor.EmitLdloc(extensibleType);
						cctor.EmitCallvirt(main.Shared.Type_get_FullName);
					},
					(body, main) => body.Emit(OpCodes.Ldstr, "} is not private. All extensible constructors MUST be private.")
				});
				cctor.EmitInvalidOpException(main, null);
				cctor.Emit(endOfLoop);
			}
			cctor.EmitForLoopTail(ctorForLoopIndex, in @continue, in @break);
			cctor.EmitRet();
			#endregion

			type.Binder.StaticConstructor.FinalizeMethodBody(main);

			#endregion

			#region CreateBindings (Initial)
			CilBody createBindingsBody = createBindings.GetOrCreateBody();
			Local userType = new Local(main.Shared.TypeSig, "userType");
			Local propertyRef = new Local(main.Shared.PropertyInfoSig, "propertyRef");
			Local userTypeName = new Local(main.CorLibTypeSig<string>(), "userTypeName");
			Local methodRef = new Local(main.Shared.MethodInfoSig, "methodRef");
			Local methodAttributes = new Local(main.Shared.SysMethodAttributesSig, "mtdAttributes");
			createBindingsBody.SetLocals(userType, propertyRef, userTypeName, methodRef, methodAttributes);

			createBindingsBody.EmitTypeof(main, CommonGenericArgs.TYPE_ARG_0_REF);
			createBindingsBody.EmitStoreThenLoad(userType);
			createBindingsBody.EmitCallvirt(main.Shared.Type_get_FullName);
			createBindingsBody.EmitStloc(userTypeName);

			createBindingsBody.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, "[Extensibles] Searching for overridden methods and properties in Extensible user-type {"),
				(body, main) => body.EmitLdloc(userTypeName),
				(body, main) => body.Emit(OpCodes.Ldstr, $"}} that belong to vanilla class {{{type.ImportedGameType.FullName}}}...")
			}); ;
			createBindingsBody.EmitUnityDbgLog(main, null);
			#endregion

			#region TryReleaseBinding
			CilBody tryReleaseBindingBody = tryReleaseBinding.GetOrCreateBody();
			tryReleaseBindingBody.Emit(OpCodes.Ldsfld, bindings.Reference);
			tryReleaseBindingBody.EmitLdarg(0);
			tryReleaseBindingBody.EmitCallvirt(bindingsFieldType.ReferenceExistingMethod("Remove", main.Shared.CWTRemoveSig));
			tryReleaseBindingBody.EmitRet();

			tryReleaseBinding.FinalizeMethodBody(main);
			#endregion

			#region TryGetBinding
			CilBody tryGetBindingBody = tryGetBinding.GetOrCreateBody();
			// Local strongRef = new Local(CommonGenericArgs.TYPE_ARG_0, "result");
			// tryGetBindingBody.Variables.Add(strongRef);

			tryGetBindingBody.Emit(OpCodes.Ldsfld, bindings.Reference);
			tryGetBindingBody.EmitLdarg(0);
			tryGetBindingBody.EmitLdarg(1);
			tryGetBindingBody.EmitCallvirt(bindingsFieldType.ReferenceExistingMethod("TryGetValue", main.Shared.CWTTryGetValueSig));
			tryGetBindingBody.EmitRet();

			/*
			tryGetBindingBody.Emit(OpCodes.Ldsfld, bindings.Reference);
			tryGetBindingBody.EmitLdarg(0);
			// tryGetBindingBody.EmitLdarg(1, false); // Reminder: It's already a by-ref type. Don't ref the ref.
			tryGetBindingBody.EmitLdloc(strongRef, true);
			tryGetBindingBody.EmitCallvirt(bindingsFieldType.ReferenceExistingMethod("TryGetValue", main.Shared.CWTTryGetValueSig));
			
			Instruction isOK = tryGetBindingBody.NewBrDest();
			tryGetBindingBody.Emit(OpCodes.Brtrue, isOK);
			tryGetBindingBody.EmitLdarg(1);				// Load the result arg, which is by-ref
			tryGetBindingBody.EmitNull();					// Load the value (null)
			tryGetBindingBody.Emit(OpCodes.Stind_Ref);		// Store value as ref into arg.
			tryGetBindingBody.EmitValue(false);
			tryGetBindingBody.EmitRet();

			tryGetBindingBody.Emit(isOK);
			tryGetBindingBody.EmitLdarg(1);
			tryGetBindingBody.EmitLdloc(strongRef);
			tryGetBindingBody.EmitNew(main.Shared.WeakReferenceCtorGenericArg0Reference);
			tryGetBindingBody.Emit(OpCodes.Stind_Ref);
			tryGetBindingBody.EmitValue(true);
			tryGetBindingBody.EmitRet();
			*/

			tryGetBinding.FinalizeMethodBody(main);
			#endregion

		}

	}
}
