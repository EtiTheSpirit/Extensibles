using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff {

	/// <summary>
	/// Provides a definition of, or reference to, a <see langword="delegate"/> type.
	/// </summary>
	public interface IDelegateTypeWrapper : IHasTypeDefOrRef {

		/// <summary>
		/// The signature of the delegate, which is also the signature of its <c>Invoke</c> method.
		/// </summary>
		MethodSig DelegateSignature { get; }

		/// <summary>
		/// The definition of, or the reference to, the delegate constructor.
		/// </summary>
		IMethodDefOrRef Constructor { get; }

		/// <summary>
		/// The definition of, or the reference to, the delegate's <c>Invoke</c> method.
		/// </summary>
		IMethodDefOrRef Invoke { get; }

		/// <summary>
		/// The definition of, or the reference to, the delegate's <c>BeginInvoke</c> method.
		/// </summary>
		IMethodDefOrRef BeginInvoke { get; }

		/// <summary>
		/// The definition of, or the reference to, the delegate's <c>EndInvoke</c> method.
		/// </summary>
		IMethodDefOrRef EndInvoke { get; }

	}
}
