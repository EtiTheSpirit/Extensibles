using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {
	public interface IHasTypeDefOrRef {

		/// <summary>
		/// The defined or referenced type.
		/// </summary>
		ITypeDefOrRef Reference { get; }

		/// <summary>
		/// The signature of the defined or referenced type.
		/// </summary>
		TypeSig Signature { get; }

	}
}
