using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.BulkMemberStorage {

	/// <summary>
	/// Packages method proxy members and their corresponding hook members on the stack for code cleanliness.
	/// </summary>
	public readonly ref struct ProxyAndHookPackage {

		/// <summary>
		/// If true, this is for a property and thus the property related members should be referenced.
		/// </summary>
		public readonly bool IsForProperty => property != null;

		/// <summary>
		/// The definition of the property, for property hooks.
		/// </summary>
		public readonly PropertyDefAndRef property;

		/// <summary>
		/// The base method members, or the getter members (this is shared).
		/// </summary>
		private readonly ExtensibleMethodProxyMembers? methodProxyMembers;

		/// <summary>
		/// The base method hook contents, or the getter hook contents (this is shared).
		/// </summary>
		private readonly BepInExHookRef? methodHook;

		/// <summary>
		/// The setter members, if present.
		/// </summary>
		private readonly ExtensibleMethodProxyMembers? setterProxyMembers;

		/// <summary>
		/// The setter hook contents, if present.
		/// </summary>
		private readonly BepInExHookRef? setterHook;

		private static InvalidOperationException InvalidMode(bool isForProp) {
			if (isForProp) {
				return new InvalidOperationException("This package is for a property. You cannot reference the method-related members.");
			} else {
				return new InvalidOperationException("This package is for a method. You cannot reference the property-related members.");
			}
		}

		public ExtensibleMethodProxyMembers MethodProxyMembers => !IsForProperty ? methodProxyMembers.Value : throw InvalidMode(true);
		public BepInExHookRef MethodHookMembers => !IsForProperty ? methodHook.Value : throw InvalidMode(true);

		public ExtensibleMethodProxyMembers? PropertyGetterProxyMembers => IsForProperty ? methodProxyMembers : throw InvalidMode(false);
		public BepInExHookRef? PropertyGetterHookMembers => IsForProperty ? methodHook : throw InvalidMode(false);

		public ExtensibleMethodProxyMembers? PropertySetterProxyMembers => IsForProperty ? setterProxyMembers : throw InvalidMode(false);
		public BepInExHookRef? PropertySetterHookMembers => IsForProperty ? setterHook : throw InvalidMode(false);

		public ProxyAndHookPackage(in ExtensibleMethodProxyMembers methodProxyMembers, in BepInExHookRef methodHook) {
			this.property = null;
			this.methodProxyMembers = methodProxyMembers;
			this.methodHook = methodHook;
			this.setterProxyMembers = default;
			this.setterHook = default;
		}

		public ProxyAndHookPackage(in ExtensibleMethodProxyMembers? getterProxyMembers, in ExtensibleMethodProxyMembers? setterProxyMembers, in BepInExHookRef? getterHookMembers, in BepInExHookRef? setterHookMembers, PropertyDefAndRef property) {
			this.property = property;
			this.methodProxyMembers = getterProxyMembers;
			this.methodHook = getterHookMembers;
			this.setterProxyMembers = setterProxyMembers;
			this.setterHook = setterHookMembers;
		}

	}
}
