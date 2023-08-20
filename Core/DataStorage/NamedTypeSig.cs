using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// Represents a parameter signature with a name, for use in method arguments.
	/// <para/>
	/// This is not to be used for generic types. See <see cref="GenericParam"/> for generic usage.
	/// </summary>
	public struct NamedTypeSig {

		/// <summary>
		/// The name of this method parameter.
		/// </summary>
		public string name;

		/// <summary>
		/// The signature of this method parameter.
		/// </summary>
		public TypeSig signature;

		public NamedTypeSig(TypeSig signature, string name) {
			this.signature = signature;
			this.name = name;
		}

		public static implicit operator NamedTypeSig((TypeSig, string) tuple) => new NamedTypeSig(tuple.Item1, tuple.Item2);

		public static implicit operator NamedTypeSig(TypeSig baseSig) => new NamedTypeSig(baseSig, null);

	}
}
