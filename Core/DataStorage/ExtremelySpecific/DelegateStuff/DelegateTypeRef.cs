using dnlib.DotNet;
using HookGenExtender.Core.ILGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff {

	/// <summary>
	/// A reference to a delegate type's members.
	/// </summary>
	public sealed class DelegateTypeRef : IDelegateTypeWrapper {

		public IMethodDefOrRef Constructor { get; }
		public IMethodDefOrRef Invoke { get; }
		public IMethodDefOrRef BeginInvoke { get; }
		public IMethodDefOrRef EndInvoke { get; }
		public ITypeDefOrRef Reference { get; }
		public TypeSig Signature { get; }
		public MethodSig DelegateSignature { get; }

		/// <summary>
		/// Create a new delegate type reference. Consider using <see cref="ILTools.ReferenceDelegateType(ExtensiblesGenerator, MethodSig, ITypeDefOrRef)"/> or one of its variants.
		/// </summary>
		/// <param name="signature"></param>
		/// <param name="constructor"></param>
		/// <param name="invoke"></param>
		/// <param name="beginInvoke"></param>
		/// <param name="endInvoke"></param>
		/// <param name="declaringType"></param>
		public DelegateTypeRef(MethodSig signature, IMethodDefOrRef constructor, IMethodDefOrRef invoke, IMethodDefOrRef beginInvoke, IMethodDefOrRef endInvoke, ITypeDefOrRef declaringType) {
			Constructor = constructor;
			Signature = declaringType.ToTypeSig();
			DelegateSignature = signature;
			Invoke = invoke;
			BeginInvoke = beginInvoke;
			EndInvoke = endInvoke;
			Reference = declaringType;
			
		}
	}
}
