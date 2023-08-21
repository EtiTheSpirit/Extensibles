using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using HookGenExtender.Core.Utils.MemberMutation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Void = HookGenExtender.Core.DataStorage.ExtremelySpecific.Void;
using BindingFlags = System.Reflection.BindingFlags;
using dnlib.DotNet.Writer;
using HookGenExtender.Core.Utils.Debugging;
using HookGenExtender.Core.Utils.DNLib;
using System.Runtime.CompilerServices;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		/// <summary>
		/// <strong>Step 5.1 of the Extensible Type Pipeline:</strong><br/>
		/// Creates the default constructor of an Extensible type. This constructor receives only one parameter, which is an instance of the original class.
		/// By default, this constructor has already been created, so only the corresponding <c>Bind()</c> method needs to be made. This method does that.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="binderMembers"></param>
		public static void MakeBindMethodFromCommonConstructor(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			MethodSig bindSignature = MethodSig.CreateStatic(main.Shared.WeakReferenceGenericArg0.Signature, coreMembers.type.ImportedGameTypeSig);
			MethodDefAndRef bind = new MethodDefAndRef(main, "Bind", bindSignature, coreMembers.type.GenericBinder.Reference, MethodAttributes.Public);
			bind.SetParameterName(0, "toObject");
			coreMembers.type.Binder.AddMethod(bind);

			CommonGenerateBindMethodFromCtor(main, bind, bindSignature, 0, in coreMembers, in binderMembers);
		}

		/// <summary>
		/// <strong>Step 5.2 of the Extensible Type Pipeline:</strong><br/>
		/// Receives a constructor from the game and creates a static <c>Bind()</c> method in the binder that corresponds to that constructor.
		/// This will do nothing if the signature matches the default extensible constructor (see <see cref="MakeBindMethodFromCommonConstructor(ExtensiblesGenerator, in ExtensibleCoreMembers, in ExtensibleBinderCoreMembers)"/>)
		/// Additionally, this should start from 1, because 0 is for that default extensible constructor.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="binderMembers"></param>
		public static void MakeBindMethodFromConstructor(ExtensiblesGenerator main, MethodDefAndRef originalCtor, int constructorIndex, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			MethodSig originalCtorSignatureImported = ((MemberRef)originalCtor.Reference).MethodSig.CloneAndImport(main);
			MethodSig bindSignature = originalCtorSignatureImported.Clone();
			bindSignature.Params.Insert(0, coreMembers.type.ImportedGameTypeSig);
			bindSignature.RetType = main.Shared.WeakReferenceGenericArg0.Signature; //coreMembers.weakReferenceExtensibleType.Signature;
			bindSignature.HasThis = false;

			// The extensible default constructor is a constructor that takes in the original type and nothing else.
			// It will always exist beforehand, and thus should not be created as a new ConstructorInfo (only the Bind method).
			bool isExtensibleDefaultConstructor = bindSignature.Params.Count == 1;
			if (isExtensibleDefaultConstructor) return;
			MethodDefAndRef bind = new MethodDefAndRef(main, "Bind", bindSignature, coreMembers.type.GenericBinder.Reference, MethodAttributes.Public);
			bind.SetParameterName(0, "toObject");
			for (int i = 0; i < originalCtor.Definition.MethodSig.Params.Count; i++) {
				bind.SetParameterName(i + 1, originalCtor.Definition.GetParameterName(i));
			}
			coreMembers.type.Binder.AddMethod(bind);

			CommonGenerateBindMethodFromCtor(main, bind, bindSignature, constructorIndex, in coreMembers, in binderMembers);
		}

		private static void CommonGenerateBindMethodFromCtor(ExtensiblesGenerator main, MethodDefAndRef bind, MethodSig bindSignature, int constructorIndex, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			CilBody bindBody = bind.GetOrCreateBody();
			Local tExtensibleType = new Local(main.Shared.TypeSig, "tExtensibleType");
			Local inputObjectType = new Local(main.Shared.TypeSig, "gameObjectType");
			Local extensibleInstance = new Local(CommonGenericArgs.TYPE_ARG_0, "tExtensible");
			Local constructor = new Local(main.Shared.ConstructorInfoSig, "constructor");

			ExtensibleTypeData type = coreMembers.type;

			// TryGetBinding wrong

			#region Ensure Original is not null
			Instruction argNotNull = bindBody.NewBrDest();
			bindBody.EmitLdarg(0);
			bindBody.EmitNull();
			bindBody.Emit(OpCodes.Ceq);
			bindBody.Emit(OpCodes.Brfalse_S, argNotNull);
			////
			bindBody.EmitException<ArgumentNullException>(main, bind.Definition.GetParameterName(0)); // In this case the string param is an argument name
			#endregion

			// Store typeof(TExtensible)
			bindBody.Emit(argNotNull);
			bindBody.EmitTypeof(main, CommonGenericArgs.TYPE_ARG_0_REF);
			bindBody.EmitStloc(tExtensibleType);

			#region Ensure Original is an exact type match
			// Start by ensuring that the input object (always param 0) is, verbatim, the type that the extensible one is based on.
			bindBody.EmitLdarg(0);
			bindBody.EmitGetType(main);
			bindBody.EmitStoreThenLoad(inputObjectType);
			bindBody.EmitTypeof(main, coreMembers.type.ImportedGameType);
			bindBody.EmitCall(main.Shared.TypesNotEqual);
			Instruction typeIsEqual = bindBody.NewBrDest();
			bindBody.Emit(OpCodes.Brfalse_S, typeIsEqual);
			////
			bindBody.EmitStringConcat(main, true, new Action<CilBody, ExtensiblesGenerator>[] {
				(body, main) => body.Emit(OpCodes.Ldstr, $"[Extensible] Illegal attempt to call Bind with a derived type! Expected an instance of type {{{type._originalGameType.FullName}}}, but got an instance of type {{"),
				(body, main) => {
					body.EmitLdloc(inputObjectType);
					body.EmitCallvirt(main.Shared.Type_get_FullName);
				},
				(body, main) => body.Emit(OpCodes.Ldstr, "}. The first parameter of Bind requires an exact type match; derived types are not valid. Use the Binder for that derived type instead.")
			});
			bindBody.EmitInvalidOpException(main, null);
			////
			bindBody.Emit(typeIsEqual);
			#endregion

			#region Ensure binding is not a duplicate
			bindBody.EmitLdarg(0);
			bindBody.EmitLdloc(extensibleInstance, true);
			bindBody.EmitCallvirt(binderMembers.tryGetBindingMethod.Reference);
			Instruction bindingIsNotAlreadyPresent = bindBody.NewBrDest();
			bindBody.Emit(OpCodes.Brfalse, bindingIsNotAlreadyPresent);
			////
			bindBody.EmitInvalidOpException(main, $"[Extensible] Duplicate binding! Only one instance of your current Extensible type can be bound to an instance of type {{{type.ImportedGameType.FullName}}} at a time.");
			////
			bindBody.Emit(bindingIsNotAlreadyPresent);
			#endregion

			#region Final Instance Creation

			#region Read and Populate Cache (if needed)
			bindBody.Emit(OpCodes.Ldsfld, binderMembers.constructorCacheField.Reference);
			bindBody.EmitLdc_I4(constructorIndex);
			bindBody.Emit(OpCodes.Ldelem, main.Shared.ConstructorInfoReference);
			bindBody.EmitStoreThenLoad(constructor);
			bindBody.EmitNull();
			bindBody.EmitCall(main.Shared.ConstructorInfosEqual);
			Instruction ctorAlreadyPresent = bindBody.NewBrDest();
			bindBody.Emit(OpCodes.Brfalse, ctorAlreadyPresent);
			////
			ITypeDefOrRef[] ctorArgTypes = bindSignature.Params.Select(typeSig => typeSig.ToTypeDefOrRef()).ToArray();
			bindBody.EmitLdloc(tExtensibleType);
			bindBody.EmitGetConstructor(main, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic, ctorArgTypes);
			bindBody.EmitStoreThenLoad(constructor);
			bindBody.EmitNull();
			bindBody.EmitCall(main.Shared.ConstructorInfosEqual);
			Instruction ctorWasFound = bindBody.NewBrDest();
			bindBody.Emit(OpCodes.Brfalse, ctorWasFound);
			////////
			bindBody.EmitException<MissingMethodException>(main, $"To call this variant of Binder<TExtensible>.Bind(), you must declare a private constructor with identical arguments in your Extensible type.");
			////////
			bindBody.Emit(ctorWasFound);
			bindBody.Emit(OpCodes.Ldsfld, binderMembers.constructorCacheField.Reference);
			bindBody.EmitLdc_I4(constructorIndex);
			bindBody.EmitLdloc(constructor);
			bindBody.Emit(OpCodes.Stelem, main.Shared.ConstructorInfoReference);
			////
			bindBody.Emit(ctorAlreadyPresent);
			bindBody.EmitLdloc(constructor);
			#endregion

			// Note: ConstructorInfo is currently the only element on the stack at this point.

			#region Construct
			bindBody.EmitArrayOfArgs(main, bindSignature.Params.Count, assumeArrayAlreadyOnStack: false, shouldBoxFunc: (index) => (bindSignature.Params[index].IsValueType, bindSignature.Params[index].ToTypeDefOrRef()));
			bindBody.EmitCallvirt(main.Shared.ConstructorInfoInvoke);
			bindBody.Emit(OpCodes.Castclass, CommonGenericArgs.TYPE_ARG_0_REF);
			bindBody.EmitStloc(extensibleInstance);
			#endregion

			// Note: Instance of TExtensible is currently on the stack.

			#region Build result, store in bindings lookup, create hooks if needed
			// Store in cache...
			bindBody.Emit(OpCodes.Ldsfld, binderMembers.type.GenericBinder.ReferenceExistingField(binderMembers.bindingsField));
			bindBody.EmitLdarg(0);
			bindBody.EmitLdloc(extensibleInstance);
			bindBody.Emit(OpCodes.Callvirt, binderMembers.cwtInstanceDef.ReferenceExistingMethod("Add", main.Shared.CWTAddSig));

			// Construct WeakReference<TExtensible>...
			bindBody.EmitLdloc(extensibleInstance);
			bindBody.EmitNew(coreMembers.weakReferenceExtensibleType.ReferenceExistingMethod(".ctor", main.Shared.WeakReferenceCtorSig));
			bindBody.Emit(OpCodes.Ldsfld, binderMembers.hasCreatedBindingsField.Reference);
			Instruction alreadyCreatedHooks = bindBody.NewBrDest();
			bindBody.Emit(OpCodes.Brtrue, alreadyCreatedHooks);
			////
			// Create hooks, as it is needed...
			bindBody.EmitNew(main.Shared.HashSetStringCtor);
			bindBody.Emit(OpCodes.Call, binderMembers.createBindingsMethod.Reference);
			bindBody.EmitValue(true);
			bindBody.Emit(OpCodes.Stsfld, binderMembers.hasCreatedBindingsField.Reference);
			////
			bindBody.Emit(alreadyCreatedHooks);
			bindBody.EmitRet();
			#endregion

			#endregion

			bindBody.FinalizeMethodBody(main);
		}

	}
}
