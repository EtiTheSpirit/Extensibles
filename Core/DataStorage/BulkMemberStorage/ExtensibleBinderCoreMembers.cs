using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.BulkMemberStorage {
	public readonly struct ExtensibleBinderCoreMembers {

		/// <summary>
		/// The extensible type this exists for.
		/// </summary>
		public readonly ExtensibleTypeData type;

		/// <summary>
		/// A declaration and reference to the <c>&lt;Binder&gt;bindings</c> field.
		/// </summary>
		public readonly FieldDefAndRef bindingsField;

		/// <summary>
		/// A declaration and reference to <c>&lt;Binder&gt;constructorCache</c>
		/// </summary>
		public readonly FieldDefAndRef constructorCacheField;

		/// <summary>
		/// A declaration and reference to the <c>&lt;Binder&gt;hasCreatedBindings</c> field.
		/// </summary>
		public readonly FieldDefAndRef hasCreatedBindingsField;

		/// <summary>
		/// A declaration and reference to the <c>&lt;Binder&gt;CreateBindings</c> method.
		/// </summary>
		public readonly MethodDefAndRef createBindingsMethod;

		/// <summary>
		/// A declaration and reference to the <c>TryGetBinding()</c> method.
		/// </summary>
		public readonly MethodDefAndRef tryGetBindingMethod;

		/// <summary>
		/// A declaration and reference to the <c>TryReleaseBinding()</c> method.
		/// </summary>
		public readonly MethodDefAndRef tryReleaseBindingMethod;

		/// <summary>
		/// The definition of <see cref="ConditionalWeakTable{TKey, TValue}"/> for this specific binder.
		/// </summary>
		public readonly GenericInstanceTypeDef cwtInstanceDef;

		public ExtensibleBinderCoreMembers(ExtensibleTypeData type, FieldDefAndRef bindingsField, FieldDefAndRef hasCreatedBindingsField, FieldDefAndRef constructorCacheField, MethodDefAndRef createBindingsMethod, MethodDefAndRef tryGetBindingMethod, MethodDefAndRef tryReleaseBindingMethod, GenericInstanceTypeDef cwtInstanceDef) {
			this.type = type;
			this.bindingsField = bindingsField;
			this.hasCreatedBindingsField = hasCreatedBindingsField;
			this.constructorCacheField = constructorCacheField;
			this.createBindingsMethod = createBindingsMethod;
			this.tryGetBindingMethod = tryGetBindingMethod;
			this.tryReleaseBindingMethod = tryReleaseBindingMethod;
			this.cwtInstanceDef = cwtInstanceDef;
		}
	}
}
