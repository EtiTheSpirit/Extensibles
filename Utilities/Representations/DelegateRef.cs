using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Representations {
	public class DelegateRef {

		public TypeRef Delegate { get; }

		public TypeSig DelegateSignature { get; }

		public DelegateRef(TypeRef delegateType, TypeSig delegateSig) {
			Delegate = delegateType;
			DelegateSignature = delegateSig;
		}

	}
}
