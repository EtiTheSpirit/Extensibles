using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// A minimal storage container containing a definition and a reference to that definition.
	/// </summary>
	public sealed class DefinitionAndReference<TDefinition, TReference> {
		public TDefinition Definition { get; }
		public TReference Reference { get; }

		public DefinitionAndReference(TDefinition definition, TReference reference) {
			Definition = definition;
			Reference = reference;
		}

		public IMemberDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			throw new NotSupportedException();
		}
	}
}
