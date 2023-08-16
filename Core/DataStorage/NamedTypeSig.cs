using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// Represents a parameter signature with a name. In general this should be cached.
	/// <para/>
	/// This is not to be used for generic types. See <see cref="GenericParam"/> for generic usage.
	/// </summary>
	public sealed class NamedTypeSig {

		/// <summary>
		/// The name of this generic parameter.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The signature of this generic parameter.
		/// </summary>
		public TypeSig Signature { get; }

		public NamedTypeSig(TypeSig type, string name) {
			Signature = type;
			Name = name;
		}

		public static implicit operator NamedTypeSig((TypeSig, string) tuple) => new NamedTypeSig(tuple.Item1, tuple.Item2);

		public static implicit operator NamedTypeSig(TypeSig baseSig) => new NamedTypeSig(baseSig, null);

	}
}
