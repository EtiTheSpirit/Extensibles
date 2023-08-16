﻿using dnlib.DotNet;
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

		/// <summary>
		/// Creates a constructor with the provided attributes and parameters.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="attributes"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static MethodDefAndRef CreateConstructor(ExtensiblesGenerator main, CachedTypeDef onType, MethodAttributes attributes, params NamedTypeSig[] parameters) {
			MethodSig ctorSig = MethodSig.CreateInstance(main.CorLibTypeSig(), parameters.Select(param => param.Signature).ToArray());
			attributes |= MethodAttributes.SpecialName | MethodAttributes.HideBySig;
			attributes &= ~MethodAttributes.Static;
			MethodDefUser ctor = new MethodDefUser(".ctor", ctorSig, attributes);
			for (int i = 0; i < parameters.Length; i++) {
				ctor.SetParameterName(i, parameters[i].Name);
			}
			return new MethodDefAndRef(main.Extensibles, ctor, onType.Reference);
		}

		/// <summary>
		/// Creates a public constructor with the provided parameters.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="attributes"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static MethodDefAndRef CreateConstructor(ExtensiblesGenerator main, CachedTypeDef onType, params NamedTypeSig[] parameters) => CreateConstructor(main, onType, MethodAttributes.Public, parameters);

		/// <summary>
		/// Creates all members that are core to an extensible type (and thus common between them all), and populates their code.
		/// </summary>
		public static ExtensibleCoreMembers MakeExtensibleCoreMembers(ExtensiblesGenerator main, ExtensibleTypeData @in) {
			GenericInstanceTypeDef weakRefFldType = main.Shared.WeakReference.MakeGenericType(@in.ImportedGameTypeSig);

			MethodDefAndRef constructor = CreateConstructor(main, @in.ExtensibleType, new NamedTypeSig(@in.ImportedGameTypeSig, "original"));
			@in.ExtensibleType.AddMethod(constructor);

			FieldDefAndRef weakRefResult = weakRefFldType.CreateFieldOfThisType("<Extensible>originalWeakRef", CommonAttributes.SPECIAL_LOCKED_FIELD, @in.ExtensibleType.Reference);
			@in.ExtensibleType.AddField(weakRefResult);

			PropertyDefAndRef weakRefResolver = new PropertyDefAndRef(
				main.Extensibles,
				"Original",
				new PropertySig(true, @in.ImportedGameTypeSig),
				@in.ExtensibleType.Reference,
				default,
				MethodAttributes.Public,
				null
			);
			@in.ExtensibleType.AddProperty(weakRefResolver);

			ExtensibleCoreMembers mbrs = new ExtensibleCoreMembers(@in, constructor, weakRefResolver, weakRefResult, weakRefFldType);
			CodeExtensibleCoreMembers(main, mbrs);
			return mbrs;
		}

		/// <summary>
		/// Creates all members that are core to a binder type (and thus common between them all), and populate their code where applicable.
		/// Some members do not get code complete, as it is generated by several different method calls conditionally.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="in"></param>
		public static ExtensibleBinderCoreMembers MakeBinderCoreMembers(ExtensiblesGenerator main, ExtensibleTypeData @in) {
			GenericVar tExtensible = CommonGenericArgs.TYPE_ARG_0; // Provide this under a proxy name.

			GenericInstanceTypeDef bindingsFieldType = main.Shared.CWTReference.MakeGenericType(@in.ImportedGameTypeSig, tExtensible);
			FieldDefAndRef bindingsField = bindingsFieldType.CreateFieldOfThisType("<Binder>bindings", CommonAttributes.SPECIAL_LOCKED_STATIC_FIELD, @in.Binder.Reference);
			@in.Binder.AddField(bindingsField);

			FieldDefAndRef hasCreatedBindingsField = new FieldDefAndRef(main.Extensibles, "<Binder>hasCreatedBindings", new FieldSig(main.CorLibTypeSig<bool>()), @in.Binder.Reference, CommonAttributes.SPECIAL_LOCKED_STATIC_FIELD);
			@in.Binder.AddField(hasCreatedBindingsField);
			
			MethodDefAndRef createBindingsMethod = new MethodDefAndRef(main.Extensibles, "<Binder>CreateBindings", MethodSig.CreateStatic(main.CorLibTypeSig<Void>(), main.Shared.TypeSig, main.Shared.HashSetStringInstanceSig), @in.Binder.Reference, CommonAttributes.SPECIAL_LOCKED_METHOD);
			@in.Binder.AddMethod(createBindingsMethod);

			MethodDefAndRef tryReleaseBindingMethod = new MethodDefAndRef(main.Extensibles, "TryReleaseBinding", MethodSig.CreateStatic(main.CorLibTypeSig<bool>(), @in.ImportedGameTypeSig), @in.Binder.Reference, MethodAttributes.Public);
			@in.Binder.AddMethod(tryReleaseBindingMethod);

			MethodDefAndRef tryGetBindingMethod = new MethodDefAndRef(main.Extensibles, "TryGetBinding", MethodSig.CreateStatic(main.CorLibTypeSig<bool>(), @in.ImportedGameTypeSig, new ByRefSig(@in.ExtensibleType.Signature)), @in.Binder.Reference, MethodAttributes.Public);
			@in.Binder.AddMethod(tryGetBindingMethod);

			ExtensibleBinderCoreMembers mbrs = new ExtensibleBinderCoreMembers(@in, bindingsField, hasCreatedBindingsField, createBindingsMethod, tryGetBindingMethod, tryReleaseBindingMethod, bindingsFieldType);
			CodeBinderCoreMembers(main, mbrs);
			return mbrs;
		}

	}
}
