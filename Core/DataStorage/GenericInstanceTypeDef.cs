using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// A soft-instance of a generic type with an assigned generic type signature.
	/// </summary>
	public sealed class GenericInstanceTypeDef : IHasTypeDefOrRef {

		/// <summary>
		/// The <see cref="ExtensiblesGenerator"/> that manages this type.
		/// </summary>
		public ExtensiblesGenerator Generator { get; }

		/// <summary>
		/// The underlying type without its generic parameters set.
		/// </summary>
		public ITypeDefOrRef NonGeneric { get; }

		/// <summary>
		/// The generic parameters of this definition.
		/// </summary>
		public IReadOnlyList<TypeSig> GenericParameterTypes { get; }

		/// <summary>
		/// A reference to the type that this represents, including its generic type.
		/// </summary>
		public ITypeDefOrRef Reference { get; }

		/// <summary>
		/// The signature of this generic type.
		/// </summary>
		public GenericInstSig Signature { get; }

		TypeSig IHasTypeDefOrRef.Signature => Signature;

		/// <summary>
		/// <see cref="Signature"/> wrapped in a new <see cref="FieldSig"/>.
		/// </summary>
		public FieldSig FieldSignature => new FieldSig(Signature);

		public GenericInstanceTypeDef(ExtensiblesGenerator main, ITypeDefOrRef definition, params TypeSig[] types) {
			Generator = main;
			NonGeneric = definition;
			GenericParameterTypes = types.ToList().AsReadOnly();
			Signature = new GenericInstSig(definition.ToTypeSig().ToClassOrValueTypeSig(), types.ToArray());
			Reference = Signature.ToTypeDefOrRef();
		}

		/// <summary>
		/// Makes a new <see cref="FieldDefAndRef"/> that represents a named field with this generic type as the field's type.
		/// </summary>
		/// <param name="name">The name of the new field.</param>
		/// <param name="attributes">The attributes of this field.</param>
		/// <param name="owner">The type that owns this field, or null to use this type.</param>
		/// <returns></returns>
		public FieldDefAndRef CreateFieldOfThisType(string name, FieldAttributes attributes, IMemberRefParent owner) {
			FieldDef definition = new FieldDefUser(name, FieldSignature, attributes);
			return new FieldDefAndRef(Generator, definition, owner ?? Reference, false);
		}

		/// <summary>
		/// Returns a new reference to an existing method on this type.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public MemberRef ReferenceExistingMethod(string name, MethodSig method) {
			return new MemberRefUser(NonGeneric.Module, name, method, Reference);
			// TO FUTURE XAN: Can't cache, MethodSig doesn't have an equality operator.
			// It might not be worth it to do this yourself.
		}

		/// <summary>
		/// Returns a new reference to an existing method on this type.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public MemberRef ReferenceExistingMethod(MethodDefAndRef existing) {
			return new MemberRefUser(NonGeneric.Module, existing.Definition.Name, existing.Definition.MethodSig, Reference);
		}

		/// <summary>
		/// Returns a new reference to an existing field on this type.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="field"></param>
		/// <returns></returns>
		public MemberRef ReferenceExistingField(string name, FieldSig field) {
			return new MemberRefUser(NonGeneric.Module, name, field, Reference);
		}

		/// <summary>
		/// Returns a new reference to an existing field on this type.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="field"></param>
		/// <returns></returns>
		public MemberRef ReferenceExistingField(FieldDefAndRef existing) {
			return new MemberRefUser(NonGeneric.Module, existing.Definition.Name, existing.Definition.FieldSig, Reference);
		}
	}
}
