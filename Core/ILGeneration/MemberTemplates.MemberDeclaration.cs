﻿using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using HookGenExtender.Core.Utils.Ext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Void = HookGenExtender.Core.DataStorage.ExtremelySpecific.Void;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		#region Extensible Type Constructor

		/// <summary>
		/// Creates a constructor with the provided attributes and parameters. This is used by step 1.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="attributes"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		private static MethodDefAndRef CreateConstructor(ExtensiblesGenerator main, CachedTypeDef onType, MethodAttributes attributes, params NamedTypeSig[] parameters) {
			MethodSig ctorSig = MethodSig.CreateInstance(main.CorLibTypeSig(), parameters.Select(param => param.Signature).ToArray());
			attributes |= MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName;
			attributes &= ~MethodAttributes.Static;
			MethodDefUser ctor = new MethodDefUser(".ctor", ctorSig, attributes);
			int numParams = parameters.Length;
			for (int i = 0; i < numParams; i++) {
				ctor.SetParameterName(i, parameters[i].Name);
			}
			return new MethodDefAndRef(main, ctor, onType.Reference, false);
		}

		/// <summary>
		/// Creates a public constructor with the provided parameters. This is used by step 1.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="attributes"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		private static MethodDefAndRef CreateConstructor(ExtensiblesGenerator main, CachedTypeDef onType, params NamedTypeSig[] parameters) => CreateConstructor(main, onType, MethodAttributes.Public, parameters);

		#endregion

		#region Core Member Declarations

		/// <summary>
		/// <strong>Step 1 of the Extensible Type Pipeline:</strong><br/>
		/// Creates all members that are core to an extensible type (and thus common between them all), and populates their code.
		/// </summary>
		public static ExtensibleCoreMembers MakeExtensibleCoreMembers(ExtensiblesGenerator main, ExtensibleTypeData @in) {
			GenericInstanceTypeDef weakRefFldType = main.Shared.WeakReference.MakeGenericType(main, @in.ImportedGameTypeSig);
			GenericInstanceTypeDef weakRefExtensibleFldType = main.Shared.WeakReference.MakeGenericType(main, @in.ExtensibleType.Signature);

			MethodDefAndRef constructor = CreateConstructor(main, @in.ExtensibleType, new NamedTypeSig(@in.ImportedGameTypeSig, "original"));
			@in.ExtensibleType.AddMethod(constructor);

			FieldDefAndRef weakRefResult = weakRefFldType.CreateFieldOfThisType("<Extensible>originalWeakRef", CommonAttributes.SPECIAL_LOCKED_FIELD, @in.ExtensibleType.Reference);
			@in.ExtensibleType.AddField(weakRefResult);

			PropertyDefAndRef weakRefResolver = new PropertyDefAndRef(
				main,
				"<Extensible>Original",
				new PropertySig(true, @in.ImportedGameTypeSig),
				@in.ExtensibleType.Reference,
				PropertyAttributes.SpecialName | PropertyAttributes.RTSpecialName,
				MethodAttributes.PrivateScope,
				null
			);
			@in.ExtensibleType.AddProperty(weakRefResolver);

			ExtensibleCoreMembers mbrs = new ExtensibleCoreMembers(@in, constructor, weakRefResolver, weakRefResult, weakRefFldType, weakRefExtensibleFldType);
			CodeExtensibleCoreMembers(main, mbrs);

			return mbrs;
		}

		/// <summary>
		/// <strong>Step 2 of the Extensible Type Pipeline:</strong><br/>
		/// Creates all members that are core to a binder type (and thus common between them all), and populate their code where applicable.
		/// Some members do not get code complete, as it is generated by several different method calls conditionally.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="in"></param>
		public static ExtensibleBinderCoreMembers MakeBinderCoreMembers(ExtensiblesGenerator main, ExtensibleTypeData @in) {
			GenericVar tExtensible = CommonGenericArgs.TYPE_ARG_0; // Provide this under a proxy name.

			GenericInstanceTypeDef bindingsFieldType = main.Shared.CWTReference.MakeGenericType(main, @in.ImportedGameTypeSig, tExtensible);
			FieldDefAndRef bindingsField = bindingsFieldType.CreateFieldOfThisType("<Binder>bindings", CommonAttributes.SPECIAL_LOCKED_STATIC_FIELD, @in.GenericBinder.Reference);
			@in.Binder.AddField(bindingsField);

			FieldDefAndRef hasCreatedBindingsField = new FieldDefAndRef(main, "<Binder>hasCreatedBindings", new FieldSig(main.CorLibTypeSig<bool>()), @in.GenericBinder.Reference, CommonAttributes.SPECIAL_LOCKED_STATIC_FIELD);
			@in.Binder.AddField(hasCreatedBindingsField);

			FieldDefAndRef constructorCacheField = new FieldDefAndRef(main, "<Binder>constructorCache", new FieldSig(main.Shared.ConstructorInfoArraySig), @in.GenericBinder.Reference, CommonAttributes.SPECIAL_LOCKED_STATIC_FIELD);
			@in.Binder.AddField(constructorCacheField);

			MethodDefAndRef createBindingsMethod = new MethodDefAndRef(main, "<Binder>CreateBindings", MethodSig.CreateStatic(main.CorLibTypeSig<Void>(), main.Shared.HashSetStringInstanceSig), @in.GenericBinder.Reference, CommonAttributes.SPECIAL_LOCKED_METHOD);
			createBindingsMethod.SetParameterName(0, "excludeMethods");
			@in.Binder.AddMethod(createBindingsMethod);

			MethodDefAndRef tryReleaseBindingMethod = new MethodDefAndRef(main, "TryReleaseBinding", MethodSig.CreateStatic(main.CorLibTypeSig<bool>(), @in.ImportedGameTypeSig), @in.GenericBinder.Reference, MethodAttributes.Public);
			tryReleaseBindingMethod.SetParameterName(0, "fromInstance");
			@in.Binder.AddMethod(tryReleaseBindingMethod);

			MethodDefAndRef tryGetBindingMethod = new MethodDefAndRef(main, "TryGetBinding", MethodSig.CreateStatic(main.CorLibTypeSig<bool>(), @in.ImportedGameTypeSig, new ByRefSig(CommonGenericArgs.TYPE_ARG_0)), @in.GenericBinder.Reference, MethodAttributes.Public);
			tryGetBindingMethod.Definition.Parameters[1].GetOrCreateParamDef().IsOut = true;
			tryGetBindingMethod.SetParameterName(0, "toInstance");
			tryGetBindingMethod.SetParameterName(1, "binding");
			@in.Binder.AddMethod(tryGetBindingMethod);

			ExtensibleBinderCoreMembers mbrs = new ExtensibleBinderCoreMembers(@in, bindingsField, hasCreatedBindingsField, constructorCacheField, createBindingsMethod, tryGetBindingMethod, tryReleaseBindingMethod, bindingsFieldType);
			CodeBinderCoreMembers(main, mbrs);
			return mbrs;
		}

		/// <summary>
		/// <strong>Step 7 of the Extensible Type Pipeline:</strong><br/>
		/// This populates the extensible type with implicit casts to its base types up the entire hierarchy.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="currentType"></param>
		/// <param name="makeMemberWithin"></param>
		public static void MakeImplicitCasts(ExtensiblesGenerator main, ExtensibleTypeData currentType, ExtensibleTypeData makeMemberWithin) {
			MethodDefAndRef cast = new MethodDefAndRef(main, "op_Implicit", MethodSig.CreateStatic(currentType.ImportedGameTypeSig, makeMemberWithin.ExtensibleType.Signature), makeMemberWithin.ExtensibleType.Reference, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig);
			cast.SetParameterName(0, "extensibleObject");
			CilBody body = cast.GetOrCreateBody();
			PropertyDefAndRef org = currentType.ExtensibleType.RichProperties.FirstOrDefault(prop => prop.Definition.Name == "<Extensible>Original");
			body.EmitGetPropAuto(org); // This actually works (it emits "this" which is arg 0, which is conveniently the reference to the extensible type anyway)
			body.EmitRet(); // gg ez
			body.FinalizeMethodBody(main);
			makeMemberWithin.ExtensibleType.AddMethod(cast);

			if (main.TryGetParent(currentType, out ExtensibleTypeData parent)) {
				// Do it again using the parent type, if applicable.
				MakeImplicitCasts(main, parent, currentType);
			}
		}

		#endregion

	}
}