using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// A container for a type definition that makes specific tasks relating to extensibles easier to perform.
	/// <para/>
	/// <strong>WARNING: While this does carry references to dnlib types, they <em>are not linked</em> and thus any changes you make to the dnlib type will not be reflected back to this instance.</strong>
	/// </summary>
	public sealed class CachedTypeDef : IHasTypeDefOrRef {

		/// <summary>
		/// The underlying type reference.
		/// </summary>
		public TypeDef Underlying { get; }

		/// <summary>
		/// The <see cref="ExtensiblesGenerator"/> that manages this type.
		/// </summary>
		public ExtensiblesGenerator Generator { get; }

		/// <summary>
		/// Generic parameters, if this type is generic.
		/// </summary>
		public IList<GenericParam> GenericParameters => Underlying.GenericParameters;

		/// <summary>
		/// A reference to the underlying user-defined type.
		/// </summary>
		public ITypeDefOrRef Reference => Underlying;

		/// <summary>
		/// The signature of this type.
		/// </summary>
		public TypeSig Signature { get; }

		/// <summary>
		/// The module that this is declared in.
		/// </summary>
		public ModuleDef BaseModule { get; }

		/// <summary>
		/// The base type, as a rich extensible type.
		/// </summary>
		public CachedTypeDef Base { get; private set; }

		/// <summary>
		/// The static constructor of this type.
		/// </summary>
		public MethodDef StaticConstructor => Underlying.FindOrCreateStaticConstructor();

		/// <summary>
		/// All inner (nested) types.
		/// <para/>
		/// <strong>DANGER: This is NOT synchronized! Changing the dnspy <see cref="TypeDef"/> will not reflect to this array!</strong>
		/// </summary>
		public IReadOnlyList<FieldDefAndRef> RichFields => _richFieldsCache ??= _richFields.AsReadOnly();

		/// <summary>
		/// All methods, including their definitions and a reference to themselves.
		/// <para/>
		/// <strong>DANGER: This is NOT synchronized! Changing the dnspy <see cref="TypeDef"/> will not reflect to this array!</strong>
		/// </summary>
		public IReadOnlyList<MethodDefAndRef> RichMethods => _richMethodsCache ??= _richMethods.AsReadOnly();

		/// <summary>
		/// All properties, including their definitions and a reference to themselves.
		/// <para/>
		/// <strong>DANGER: This is NOT synchronized! Changing the dnspy <see cref="TypeDef"/> will not reflect to this array!</strong>
		/// </summary>
		public IReadOnlyList<PropertyDefAndRef> RichProperties => _richPropertiesCache ??= _richProperties.AsReadOnly();

		private List<FieldDefAndRef> _richFields = new List<FieldDefAndRef>();
		private IReadOnlyList<FieldDefAndRef> _richFieldsCache = null;

		private List<MethodDefAndRef> _richMethods = new List<MethodDefAndRef>();
		private IReadOnlyList<MethodDefAndRef> _richMethodsCache = null;

		private List<PropertyDefAndRef> _richProperties = new List<PropertyDefAndRef>();
		private IReadOnlyList<PropertyDefAndRef> _richPropertiesCache = null;

		/// <summary>
		/// Creates a new type and adds it to the provided module, if it is not a part of it already.
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="name"></param>
		public CachedTypeDef(ExtensiblesGenerator main, UTF8String name, TypeAttributes attrs) {
			Generator = main;
			BaseModule = main.Extensibles;
			Underlying = new TypeDefUser(name);
			Underlying.Attributes = attrs;
			//Reference = new TypeRefUser(inModule, name);
			BaseModule.Types.Add(Underlying);
		}

		public CachedTypeDef(ExtensiblesGenerator main, UTF8String @namespace, UTF8String name, TypeAttributes attrs) {
			Generator = main;
			BaseModule = main.Extensibles;
			Underlying = new TypeDefUser(@namespace, name);
			Underlying.Attributes = attrs;
			Signature = Underlying.ToTypeSig();
			//Reference = new TypeRefUser(inModule, @namespace, name);
			BaseModule.Types.Add(Underlying);
		}

		public CachedTypeDef(ExtensiblesGenerator main, UTF8String name, ITypeDefOrRef baseType, TypeAttributes attrs) {
			Generator = main;
			BaseModule = main.Extensibles;
			Underlying = new TypeDefUser(name, baseType);
			Underlying.Attributes = attrs;
			Signature = Underlying.ToTypeSig();
			//Reference = new TypeRefUser(inModule, name);
			BaseModule.Types.Add(Underlying);
		}

		public CachedTypeDef(ExtensiblesGenerator main, UTF8String @namespace, UTF8String name, ITypeDefOrRef baseType, TypeAttributes attrs) {
			Generator = main;
			BaseModule = main.Extensibles;
			Underlying = new TypeDefUser(@namespace, name, baseType);
			Underlying.Attributes = attrs;
			Signature = Underlying.ToTypeSig();
			//Reference = new TypeRefUser(inModule, @namespace, name);
			BaseModule.Types.Add(Underlying);
		}

		/// <summary>
		/// Create a <see cref="CachedTypeDef"/> from an existing defined type.
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="from"></param>
		public CachedTypeDef(ExtensiblesGenerator main, TypeDef from) {
			Generator = main;
			BaseModule = main.Extensibles;
			Underlying = from;
			Signature = Underlying.ToTypeSig();
			if (!BaseModule.Types.Contains(Underlying)) {
				BaseModule.Types.Add(Underlying);
			}
		}

		public void AddMethod(MethodDefAndRef method) {
			if (method.Owner == this) return;
			if (method.Owner is CachedTypeDef instance) instance.RemoveMethod(method);
			_richMethods.Add(method);
			_richMethodsCache = null;
			Underlying.Methods.Add(method.Definition);
			method.Owner = this;
		}

		public void RemoveMethod(MethodDefAndRef method) {
			if (method.Owner != this) throw new ArgumentException($"The provided method {method} is not a part of this type.");
			_richMethods.Remove(method);
			_richMethodsCache = null;
			Underlying.Methods.Remove(method.Definition);
			method.Owner = null;
		}

		public void AddField(FieldDefAndRef field) {
			if (field.Owner == this) return;
			if (field.Owner is CachedTypeDef instance) instance.RemoveField(field);
			_richFields.Add(field);
			_richFieldsCache = null;
			Underlying.Fields.Add(field.Definition);
			field.Owner = this;
		}

		public void RemoveField(FieldDefAndRef field) {
			if (field.Owner != this) throw new ArgumentException($"The provided field {field} is not a part of this type.");
			_richFields.Remove(field);
			_richFieldsCache = null;
			Underlying.Fields.Remove(field.Definition);
			field.Owner = null;
		}

		/// <summary>
		/// Add a property to this type. This will automatically add its getters/setters too.
		/// </summary>
		/// <param name="property"></param>
		public void AddProperty(PropertyDefAndRef property) {
			if (property.Owner == this) return;
			if (property.Owner is CachedTypeDef instance) instance.RemoveProperty(property);
			_richProperties.Add(property);
			_richPropertiesCache = null;
			Underlying.Properties.Add(property.Definition);
			property.Owner = this;

			if (property.Getter != null) AddMethod(property.Getter);
			if (property.Setter != null) AddMethod(property.Setter);
		}

		/// <summary>
		/// Remove a property from this type. This will automatically remove its getters/setters too.
		/// </summary>
		/// <param name="property"></param>
		/// <exception cref="ArgumentException"></exception>
		public void RemoveProperty(PropertyDefAndRef property) {
			if (property.Owner != this) throw new ArgumentException($"The provided property {property} is not a part of this type.");
			_richProperties.Remove(property);
			_richPropertiesCache = null;
			Underlying.Properties.Remove(property.Definition);
			property.Owner = null;
			if (property.Getter != null) RemoveMethod(property.Getter);
			if (property.Setter != null) RemoveMethod(property.Setter);
		}

		public void SetBase(CachedTypeDef newBase) {
			Base = newBase;
			Underlying.BaseType = newBase.Underlying;
		}


		/// <summary>
		/// Make a new generic type from this type and the provided parameters. You should store this result.
		/// <para/>
		/// This is a strict method; if the number and/or type(s) of <paramref name="parameters"/> does not match 
		/// that of <see cref="GenericParameters"/> (either by having too few/many parameters, or by inputing the signature
		/// of a type that does not fit any declared constraints), this will raise <see cref="ArgumentOutOfRangeException"/>.
		/// </summary>
		/// <param name="parameters"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentOutOfRangeException">A type does not fit the constraint of its corresponding generic type.</exception>
		public GenericInstanceTypeDef MakeGenericType(params TypeSig[] parameters) {
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));
			if (parameters.Length != GenericParameters.Count) throw new ArgumentException($"The number of generic parameter type signatures ({parameters.Length}) does not match the number of generic parameters this type has ({GenericParameters.Count}).");
			for (int i = 0; i < parameters.Length; i++) {
				TypeSig desiredType = parameters[i];
				GenericParam genParamDef = GenericParameters[i];
				if (genParamDef.HasReferenceTypeConstraint && desiredType.IsValueType) throw new ArgumentOutOfRangeException($"{nameof(parameters)}[{i}]", $"Generic parameter {i} ({genParamDef}) has a reference type constraint. The input TypeSig ({desiredType}) is a value type.");
				if (genParamDef.HasDefaultConstructorConstraint) ; // TODO
				if (genParamDef.HasGenericParamConstraints) ; // TODO
			}
			return new GenericInstanceTypeDef(Generator, Reference, parameters);
		}

		public override string ToString() {
			return Underlying.ToString();
		}

	}
}
