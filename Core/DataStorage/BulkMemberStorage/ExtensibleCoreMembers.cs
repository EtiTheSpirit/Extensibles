using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.BulkMemberStorage {

	/// <summary>
	/// Represents the "core members" of an extensible type. This is its constructor, its "Original" property, and its weak reference backing said property.
	/// </summary>
	public readonly struct ExtensibleCoreMembers {

		/// <summary>
		/// The actual extensible type.
		/// </summary>
		public readonly ExtensibleTypeData type;

		/// <summary>
		/// The constructor that initializes <see cref="originalObjectWeakReference"/>.
		/// </summary>
		public readonly MethodDefAndRef constructor;

		/// <summary>
		/// The property that resolves <see cref="originalObjectWeakReference"/> and returns a nullable value.
		/// </summary>
		public readonly PropertyDefAndRef originalObjectProxy;

		/// <summary>
		/// The field of type <see cref="weakReferenceType"/>.
		/// </summary>
		public readonly FieldDefAndRef originalObjectWeakReference;

		/// <summary>
		/// A <see cref="WeakReference{T}"/> to the original game type.
		/// </summary>
		public readonly GenericInstanceTypeDef weakReferenceType;

		/// <summary>
		/// A <see cref="WeakReference{T}"/> to the Extensible type.
		/// </summary>
		public readonly GenericInstanceTypeDef weakReferenceExtensibleType;

		public ExtensibleCoreMembers(ExtensibleTypeData type, MethodDefAndRef constructor, PropertyDefAndRef originalObjectProxy, FieldDefAndRef originalObjectWeakReference, GenericInstanceTypeDef weakReferenceType, GenericInstanceTypeDef weakReferenceExtensibleType) {
			this.type = type;
			this.constructor = constructor;
			this.originalObjectProxy = originalObjectProxy;
			this.originalObjectWeakReference = originalObjectWeakReference;
			this.weakReferenceType = weakReferenceType;
			this.weakReferenceExtensibleType = weakReferenceExtensibleType;
		}

	}
}
