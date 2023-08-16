using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Representations {

	/// <summary>
	/// A container storing all data associated with the generated Extensible method and all of its related components.
	/// This is for use with method proxies, and not property proxies.
	/// </summary>
	public readonly ref struct CompleteHookWrapper {

		/// <summary>
		/// The new virtual method created for the Extensible class, which mimics that of its real counterpart.
		/// </summary>
		public readonly MethodDefUser extensibleVirtualMethod;

		/// <summary>
		/// A field storing a reference to BIE's <c>orig</c> that is provided with a hook.
		/// </summary>
		public readonly FieldDefUser delegateOriginalHolder;

		/// <summary>
		/// A field storing whether or not the caller is currently in a manual invocation.
		/// </summary>
		public readonly FieldDefUser isCallerInInvocation;

		/// <summary>
		/// The method that actually gets added to the respective BIE hook, which itself is responsible for invoking <see cref="extensibleVirtualMethod"/>.
		/// </summary>
		public readonly MethodDefUser hookedImplementation;

		public CompleteHookWrapper(MethodDefUser extensibleVirtualMethod, FieldDefUser delegateOriginalHolder, FieldDefUser isCallerInInvocation, MethodDefUser hookedImplementation) {
			this.extensibleVirtualMethod = extensibleVirtualMethod;
			this.delegateOriginalHolder = delegateOriginalHolder;
			this.isCallerInInvocation = isCallerInInvocation;
			this.hookedImplementation = hookedImplementation;
		}

	}
}
