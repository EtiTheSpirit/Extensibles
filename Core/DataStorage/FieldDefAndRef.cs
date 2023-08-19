using dnlib.DotNet;
using HookGenExtender.Core.Utils.MemberMutation;
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
		/// The <see cref="ExtensiblesGenerator"/> that manages this type.
		/// </summary>
		public ExtensiblesGenerator Generator { get; }

		/// <summary>
		/// The definition of the field.
		/// <para/>
		/// <strong>WARNING: Changes to this object will NOT be reflected back to this object! Do not modify this object!</strong>
		/// </summary>
		public FieldDef Definition { get; }

		IMemberDef IMemberDefAndRef.Definition => Definition;

		public IMemberRef Reference { get; }

		/// <summary>
		/// Create a new field, and also a reference to itself.
		/// <para/>
		/// Despite receiving the declaring type, this <strong>DOES NOT</strong> register the method with the type!
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="name"></param>
		/// <param name="signature"></param>
		/// <param name="declaringType"></param>
		/// <param name="attrs"></param>
		public FieldDefAndRef(ExtensiblesGenerator main, string name, FieldSig signature, IMemberRefParent declaringType, FieldAttributes attrs) {
			Generator = main;
			Module = main.Extensibles;
			Definition = new FieldDefUser(name, signature, attrs);
			Reference = new MemberRefUser(Module, name, signature, declaringType);
		}

		/// <summary>
		/// Reference an existing field declaration.
		/// <para/>
		/// Despite receiving the declaring type, this <strong>DOES NOT</strong> register the method with the type!
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="name"></param>
		/// <param name="signature"></param>
		/// <param name="declaringType"></param>
		/// <param name="attrs"></param>
		public FieldDefAndRef(ExtensiblesGenerator main, FieldDef original, IMemberRefParent declaringType, bool import) {
			Generator = main;
			Module = main.Extensibles;
			Definition = original;
			FieldSig sig = original.FieldSig;
			if (import) sig = sig.CloneAndImport(main);

			IMemberRefParent newParent = declaringType;
			if (import && declaringType is ITypeDefOrRef tdor && tdor.Module != Module) {
				newParent = main.Cache.Import(tdor) as ITypeDefOrRef;
			}

			Reference = new MemberRefUser(Module, original.Name, sig, newParent);
		}

		public override string ToString() {
			return Definition.ToString();
		}

		public FieldDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			return new FieldDefAndRef(Generator, Definition, type.Reference, false);
		}

		IMemberDefAndRef IMemberDefAndRef.AsMemberOfType(IHasTypeDefOrRef type) => AsMemberOfType(type);
	}
}
