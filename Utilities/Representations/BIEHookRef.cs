using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Representations {

	/// <summary>
	/// A container representing all data in a BepInEx <c>On.</c> hook.
	/// </summary>
	public readonly ref struct BIEHookRef {

		/// <summary>
		/// The actual type containing the hook, like <c>On.Player</c>
		/// </summary>
		public readonly TypeRef hookContainerType;

		/// <summary>
		/// The type of the event delegate itself, <c><see langword="typeof"/>(On.Player.hook_Die)</c> (note that this is the <strong>hook_</strong> delegate, not the <strong>orig_</strong> delegate.
		/// </summary>
		public readonly TypeRef delegateType;
		
		/// <summary>
		/// The signature of the event delegate.
		/// </summary>
		public readonly TypeSig delegateSig;

		/// <summary>
		/// The invoke method of the event. This is the imported method, and is ready for use.
		/// </summary>
		public readonly IMethodDefOrRef invoke;

		/// <summary>
		/// The event itself. This is the original type and cannot be used.
		/// </summary>
		public readonly EventDef hook;

		/// <summary>
		/// All parameters of <see cref="invoke"/>. These types have been imported and are ready for use.
		/// </summary>
		public readonly TypeSig[] invokeParameters;

		/// <summary>
		/// All parameters of the method that the hook is actually hooking into. These types are imported.
		/// Notably, this skips the return parameter and the <see langword="this"/> parameter.
		/// </summary>
		public readonly TypeSig[] methodParameters;

		public BIEHookRef(TypeRef hookContainerType, TypeRef delegateType, TypeSig delegateSig, IMethodDefOrRef invoke, EventDef hook, TypeSig[] invokeParameters, TypeSig[] methodParameters) {
			this.hookContainerType = hookContainerType;
			this.delegateType = delegateType;
			this.delegateSig = delegateSig;
			this.invoke = invoke;
			this.hook = hook;
			this.invokeParameters = invokeParameters;
			this.methodParameters = methodParameters;
		}

	}
}
