using dnlib.DotNet;
using HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.BulkMemberStorage {

	/// <summary>
	/// Contains all of the members used for a method proxy.
	/// </summary>
	public readonly struct ExtensibleMethodProxyMembers {

		/// <summary>
		/// The type that this exists in.
		/// </summary>
		public readonly ExtensibleTypeData type;

		/// <summary>
		/// The method proxy itself.
		/// </summary>
		public readonly MethodDefAndRef proxyMethod;

		/// <summary>
		/// The <c>isCallerInInvocation</c> field, which is used to prevent re-entry on manual calls.
		/// </summary>
		public readonly FieldDefAndRef isCallerInInvocation;

		/// <summary>
		/// The <c>origMethod</c> field, which stores the currently executing orig delegate from BepInEx
		/// </summary>
		public readonly FieldDefAndRef origDelegateReference;

		/// <summary>
		/// A reference to the delegate type.
		/// </summary>
		public readonly IDelegateTypeWrapper delegateType;

		public ExtensibleMethodProxyMembers(ExtensibleTypeData type, MethodDefAndRef proxyMethod, FieldDefAndRef isCallerInInvocation, FieldDefAndRef origDelegateReference, IDelegateTypeWrapper delegateType) {
			this.type = type;
			this.proxyMethod = proxyMethod;
			this.isCallerInInvocation = isCallerInInvocation;
			this.origDelegateReference = origDelegateReference;
			this.delegateType = delegateType;
		}
	}
}
