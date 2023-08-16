using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {
	public sealed class FieldDefAndRef : IMemberDefAndRef<FieldDefAndRef, FieldDef> {

		public CachedTypeDef Owner { get; internal set; }

		public ModuleDef Module { get; }

		/// <summary>
		/// The definition of the field.
		/// <para/>
		/// <strong>WARNING: Changes to this object will NOT be reflected back to this object! Do not modify this object!</strong>
		/// </summary>
		public FieldDef Definition { get; }

		IMemberDef IMemberDefAndRef.Definition => Definition;

		public IMemberRef Reference { get; }

		public FieldDefAndRef(ModuleDef inModule, string name, FieldSig signature, IMemberRefParent declaringType, FieldAttributes attrs) {
			Module = inModule;
			Definition = new FieldDefUser(name, signature, attrs);
			Reference = new MemberRefUser(inModule, name, signature, declaringType);
		}

		public FieldDefAndRef(ModuleDef inModule, FieldDef original, IMemberRefParent declaringType) {
			Module = inModule;
			Definition = original;
			Reference = new MemberRefUser(inModule, original.Name, original.FieldSig, declaringType);
		}

		public override string ToString() {
			return Definition.ToString();
		}

		public FieldDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			return new FieldDefAndRef(Reference.Module, Definition, type.Reference);
		}

		IMemberDefAndRef IMemberDefAndRef.AsMemberOfType(IHasTypeDefOrRef type) => AsMemberOfType(type);
	}
}
