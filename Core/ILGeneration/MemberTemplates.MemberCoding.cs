using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			ctorBody.Emit(OpCodes.Ldarg_1);						// original
			ctorBody.EmitNew(newWeakReference);					// new WeakReference<T>(original)
			ctorBody.EmitStfldAuto(storage);                    // this.<Extensible>original = ^
			ctorBody.EmitRetn();
			#endregion

			#region Original
			MemberRef tryGetTarget = weakRefType.ReferenceExistingMethod("TryGetTarget", main.Shared.WeakRefTryGetTargetSig);
			CilBody getterBody = original.Getter.Definition.Body = new CilBody();
			Local result = new Local(type.ImportedGameType.ToTypeSig(), "result");
			getterBody.OverwriteLocalsWith(result);

			getterBody.EmitLdfldAuto(storage);                  // this.<Extensible>original
			getterBody.EmitLdloc(result, true);		// out original
			getterBody.Emit(OpCodes.Callvirt, tryGetTarget);    // TryGetTarget
			getterBody.Emit(OpCodes.Pop);                       // Pop (true/false result)
			getterBody.EmitLdloc(result, false);		// original
			getterBody.EmitRetn();								// return ^
			#endregion
		}

		private static void CodeBinderCoreMembers(ExtensiblesGenerator main, in ExtensibleBinderCoreMembers coreMembers) {
			ExtensibleTypeData type = coreMembers.type;
			FieldDefAndRef bindings = coreMembers.bindingsField;
			FieldDefAndRef hasCreatedBindings = coreMembers.hasCreatedBindingsField;
			MethodDefAndRef createBindings = coreMembers.createBindingsMethod;
			MethodDefAndRef tryReleaseBinding = coreMembers.tryReleaseBindingMethod;
			MethodDefAndRef tryGetBinding = coreMembers.tryGetBindingMethod;
			GenericInstanceTypeDef bindingsFieldType = coreMembers.cwtInstanceDef;

			#region cctor
			CilBody cctor = type.ExtensibleType.StaticConstructor.Body = new CilBody();
			Local extensibleType = new Local(CommonGenericArgs.TYPE_ARG_0, "tExtensible");
			cctor.OverwriteLocalsWith(extensibleType);

			// Start by setting up the bindings field.
			cctor.EmitNew(bindingsFieldType.ReferenceExistingMethod(".ctor", main.Shared.CWTCtorSig));
			cctor.EmitStfldAuto(bindings);

			// Now some validation
			#region cctor :: Ensure provided type parameter is not abstract.
			cctor.EmitTypeof(main, CommonGenericArgs.TYPE_ARG_0_REF);
			cctor.EmitStoreThenLoad(extensibleType);
			
			#endregion

			#region cctor :: Ensure all constructors are private.

			#endregion
			#endregion

			#region CreateBindings
			// This one will be particularly complicated.
			// It must begin by validating the constructors in the extensible type (TODO: Move this elsewhere?)
			// Then, it must generate the same block of code for every possible method of the original type.
			// This block of code needs to check two things:
			// #1: Is it in the set of excluded methods (arg1)? If so, skip.
			// #2: Is the method explicitly defined by the extensible type created by the user? If so, skip.
			CilBody createBindingsBody = createBindings.GetOrCreateBody();

			#endregion

			#region TryReleaseBinding

			#endregion

			#region TryGetBinding

			#endregion

		}

	}
}
