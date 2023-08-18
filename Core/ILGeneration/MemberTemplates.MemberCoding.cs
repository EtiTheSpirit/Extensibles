using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Void = HookGenExtender.Core.DataStorage.ExtremelySpecific.Void;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		/// <summary>
		/// Initializes the common contents of an extensible type. This will write the code of its constructor and its property named "Original".
		/// </summary>
		private static void CodeExtensibleCoreMembers(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers) {
			ExtensibleTypeData type = coreMembers.type;
			MethodDefAndRef constructor = coreMembers.constructor;
			PropertyDefAndRef original = coreMembers.originalObjectProxy;
			FieldDefAndRef storage = coreMembers.originalObjectWeakReference;
			GenericInstanceTypeDef weakRefType = coreMembers.weakReferenceType;

			#region Constructor
			MemberRef newWeakReference = weakRefType.ReferenceExistingMethod(".ctor", main.Shared.WeakReferenceCtorSig);
			CilBody ctorBody = constructor.GetOrCreateBody();
			ctorBody.EmitThis();
			ctorBody.Emit(OpCodes.Ldarg_1);						// original
			ctorBody.EmitNew(newWeakReference);					// new WeakReference<T>(original)
			ctorBody.Emit(OpCodes.Stfld, storage);				// this.<Extensible>original = ^
			ctorBody.EmitRet();
			#endregion

			#region Original
			MemberRef tryGetTarget = weakRefType.ReferenceExistingMethod("TryGetTarget", main.Shared.WeakRefTryGetTargetSig);
			CilBody getterBody = original.Getter.Definition.Body = new CilBody();
			Local result = new Local(type.ImportedGameTypeSig, "result");
			getterBody.SetLocals(result);

			getterBody.EmitLdThisFldAuto(storage);                  // this.<Extensible>original
			getterBody.EmitLdloc(result, true);		// out original
			getterBody.EmitCallvirt(tryGetTarget);				// TryGetTarget
			getterBody.Emit(OpCodes.Pop);                       // Pop (true/false result)
			getterBody.EmitLdloc(result, false);		// original
			getterBody.EmitRet();								// return ^
			#endregion
		}

		/// <summary>
		/// Initializes the common contents of the binder. This will write the code code of all members, with
		/// the exception of <c>CreateBindings</c> which must be coded in parts - only the initializing code to set
		/// up locals will be written, but the method will not be ended.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		private static void CodeBinderCoreMembers(ExtensiblesGenerator main, in ExtensibleBinderCoreMembers coreMembers) {
			ExtensibleTypeData type = coreMembers.type;
			FieldDefAndRef bindings = coreMembers.bindingsField;
			FieldDefAndRef hasCreatedBindings = coreMembers.hasCreatedBindingsField;
			MethodDefAndRef createBindings = coreMembers.createBindingsMethod;
			MethodDefAndRef tryReleaseBinding = coreMembers.tryReleaseBindingMethod;
			MethodDefAndRef tryGetBinding = coreMembers.tryGetBindingMethod;
			GenericInstanceTypeDef bindingsFieldType = coreMembers.cwtInstanceDef;

			#region cctor

			#region Initialize Variables
			CilBody cctor = type.ExtensibleType.StaticConstructor.Body = new CilBody();
			Local extensibleType = new Local(CommonGenericArgs.TYPE_ARG_0, "tExtensible");
			Local constructors = new Local(main.Shared.ConstructorInfoArraySig, "constructors");
			Local ctorForLoopIndex = new Local(main.CorLibTypeSig<int>(), "index");
			Local ctorForLoopLength = new Local(main.CorLibTypeSig<int>(), "length");
			cctor.SetLocals(extensibleType, constructors, ctorForLoopIndex, ctorForLoopLength);
			#endregion

			#region Bindings Field
			// Start by setting up the bindings field.
			cctor.EmitNew(bindingsFieldType.ReferenceExistingMethod(".ctor", main.Shared.CWTCtorSig));
			cctor.Emit(OpCodes.Stfld, bindings);
			#endregion

			// Now some validation
			#region cctor :: Ensure provided type parameter is not abstract.
			// extensibleType = typeof(TExtensible), duplicate for re-use.
			cctor.EmitTypeof(main, CommonGenericArgs.TYPE_ARG_0_REF);						// typeof(TExtensible)
			cctor.EmitStoreThenLoad(extensibleType); // Used in the "Ensure all constructors are private" block
			cctor.EmitDup(); // Used in getIsSealed
			cctor.EmitDup(); // Used directly below
			cctor.EmitCallvirt(main.Shared.Type_get_IsAbstract);

			Instruction getIsSealed = new Instruction(OpCodes.Callvirt, main.Shared.Type_get_IsSealed);
			cctor.Emit(OpCodes.Brfalse_S, getIsSealed);

			// EXCEPTION CASE: Extensible type is abstract...
			cctor.Emit(OpCodes.Pop);
			cctor.Emit(OpCodes.Pop);
			cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, "Extensible type ["),
				(body, main) => {
					body.EmitLdloc(extensibleType);
					body.EmitCallvirt(main.Shared.Type_get_FullName);
				},
				(body, main) => body.Emit(OpCodes.Ldstr, "] is abstract. This is not allowed; you can only bind to extensible types that are not abstract.")
			});
			cctor.EmitInvalidOpException(main, null);
			////////////////////////////////////////////////

			cctor.Emit(getIsSealed);
			Instruction endOfStub_LoadCtorBindingFlags = ILTools.GetLdc_I4((int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
			cctor.Emit(OpCodes.Brtrue_S, endOfStub_LoadCtorBindingFlags); // Go to "Ensure all constructors are private" block.

			// CASE: Extensible type is not sealed.
			cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, "Extensible type ["),
				(body, main) => {
					body.EmitLdloc(extensibleType);
					body.EmitCallvirt(main.Shared.Type_get_FullName);
				},
				(body, main) => body.Emit(OpCodes.Ldstr, "] is not sealed. It is strongly recommended that you seal your extensible types to prevent confusion caused by odd inheritence quirks.")
			});
			cctor.EmitUnityDbgLog(main, null);
			///////////////////////////////////////

			#endregion

			#region cctor :: Ensure all constructors are private.
			// cctor.EmitLdloc(extensibleType); // Obsolete due to dup() up top.
			cctor.Emit(endOfStub_LoadCtorBindingFlags);
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
				cctor.Emit(OpCodes.Ldelem, main.Shared.ConstructorInfoSig);
				cctor.EmitLdc_I4((int)System.Reflection.MethodAttributes.Private);
				cctor.EmitDup();
				cctor.EmitCallvirt(main.Shared.MethodBase_get_Attributes);
				cctor.Emit(OpCodes.And);
				cctor.Emit(OpCodes.Beq_S, @continue);
				cctor.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
					// To future Xan: Reminder, only one string per Action!
					(body, main) => body.Emit(OpCodes.Ldstr, "Constructor ["),
					(body, main) => {
						cctor.EmitLdloc(constructors);
						cctor.EmitLdloc(ctorForLoopIndex);
						cctor.Emit(OpCodes.Ldelem, main.Shared.ConstructorInfoSig);
						cctor.EmitCallvirt(main.Shared.ToStringRef);
					},
					(body, main) => body.Emit(OpCodes.Ldstr, "] is not private. All extensible constructors MUST be private.")
				});
				cctor.EmitInvalidOpException(main, null);
			}
			cctor.EmitForLoopTail(ctorForLoopIndex, in @continue, in @break);
			cctor.EmitRet();
			#endregion

			cctor.FinalizeMethodBody();

			#endregion

			#region CreateBindings (Initial)
			CilBody createBindingsBody = createBindings.Body;
			Local userType = new Local(CommonGenericArgs.TYPE_ARG_0, "userType");
			createBindingsBody.SetLocals(userType);

			createBindingsBody.EmitTypeof(main, CommonGenericArgs.TYPE_ARG_0_REF);
			createBindingsBody.EmitStloc(userType);
			#endregion

			#region TryReleaseBinding
			CilBody tryReleaseBindingBody = tryReleaseBinding.GetOrCreateBody();
			tryReleaseBindingBody.EmitLdThisFldAuto(bindings);
			tryReleaseBindingBody.EmitLdarg(0);
			tryReleaseBindingBody.EmitCallvirt(bindingsFieldType.ReferenceExistingMethod("Remove", main.Shared.CWTRemoveSig));
			tryReleaseBindingBody.EmitRet();

			tryReleaseBindingBody.UpdateInstructionOffsets();
			#endregion

			#region TryGetBinding
			CilBody tryGetBindingBody = tryGetBinding.GetOrCreateBody();
			tryGetBindingBody.EmitLdThisFldAuto(bindings);
			tryGetBindingBody.EmitLdarg(0);
			tryGetBindingBody.EmitLdarg(1, false); // Reminder: It's already a by-ref type. Don't ref the ref.
			tryGetBindingBody.EmitCallvirt(bindingsFieldType.ReferenceExistingMethod("TryGetvalue", main.Shared.CWTTryGetValueSig));
			tryGetBindingBody.EmitRet();

			tryReleaseBindingBody.UpdateInstructionOffsets();
			#endregion

		}

	}
}
