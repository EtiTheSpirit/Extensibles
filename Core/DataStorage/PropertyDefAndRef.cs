using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {


	/// <summary>
	/// Simultaneously contains a property definition and a reference to it.
	/// </summary>
	public sealed class PropertyDefAndRef : IMemberDefAndRef<PropertyDefAndRef, PropertyDef> {
		public CachedTypeDef Owner { get; internal set; }

		public ModuleDef Module { get; }

		/// <summary>
		/// The definition of the property.
		/// <para/>
		/// <strong>WARNING: Changes to this object will NOT be reflected back to this object! Do not modify this object!</strong>
		/// </summary>
		public PropertyDef Definition { get; }

		IMemberDef IMemberDefAndRef.Definition => Definition;

		public IMemberRef Reference { get; }

		/// <summary>
		/// The getter of this property, if it has one. <see langword="null"/> otherwise.
		/// </summary>
		public MethodDefAndRef Getter { get; }

		/// <summary>
		/// The setter of this property, if it has one. <see langword="null"/> otherwise.
		/// </summary>
		public MethodDefAndRef Setter { get; }

		/// <summary>
		/// Creates a new <see cref="PropertyDefAndRef"/> using the provided information. <paramref name="getterAttributes"/> determines the method attributes of the getter, or <see langword="null"/> to have no getter. Same with <paramref name="setterAttributes"/>.
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="name"></param>
		/// <param name="signature"></param>
		/// <param name="declaringType"></param>
		/// <param name="attrs"></param>
		/// <param name="getterAttributes">The attributes of a pre-created, empty, getter method. Use null to have no getter.</param>
		/// <param name="setterAttributes">The attributes of a pre-created, empty, setter method. Use null to have no setter.</param>
		public PropertyDefAndRef(ModuleDef inModule, string name, PropertySig signature, IMemberRefParent declaringType, PropertyAttributes attrs, MethodAttributes? getterAttributes, MethodAttributes? setterAttributes) {
			Definition = new PropertyDefUser(name, signature, attrs);
			Module = inModule;

			bool isStatic = false;
			if (getterAttributes != null && setterAttributes != null) {
				bool isGetterStatic = getterAttributes.Value.HasFlag(MethodAttributes.Static);
				bool isSetterStatic = setterAttributes.Value.HasFlag(MethodAttributes.Static);
				if (isGetterStatic != isSetterStatic) {
					throw new InvalidOperationException($"Attempt to construct a property definition where its getter {(isGetterStatic ? "is" : "isn't")} static, but its setter {(isSetterStatic ? "is" : "isn't")}. They must both match.");
				}
			} else if (getterAttributes != null) {
				isStatic = getterAttributes.Value.HasFlag(MethodAttributes.Static);
			} else if (setterAttributes != null) {
				isStatic = setterAttributes.Value.HasFlag(MethodAttributes.Static);
			} else {
				throw new ArgumentNullException($"{nameof(getterAttributes)} and {nameof(setterAttributes)}", $"Properties cannot have no methods. Either {nameof(getterAttributes)} and/or {nameof(setterAttributes)} must be set. Both cannot be null.");
			}

			if (getterAttributes != null) {
				MethodSig getterSig = MethodSig.CreateStatic(signature.RetType);
				getterSig.HasThis = !isStatic;
				Getter = new MethodDefAndRef(inModule, name, getterSig, declaringType, getterAttributes.Value);
				Definition.GetMethod = Getter.Definition;
			}
			if (setterAttributes != null) {
				MethodSig setterSig = MethodSig.CreateStatic(inModule.CorLibTypes.Void, signature.RetType);
				setterSig.HasThis = !isStatic;
				Setter = new MethodDefAndRef(inModule, name, setterSig, declaringType, setterAttributes.Value);
				Definition.SetMethod = Setter.Definition;
			}
		}

		public PropertyDefAndRef(ModuleDef inModule, PropertyDef original, IMemberRefParent declaringType) {
			Definition = new PropertyDefUser(original.Name, original.PropertySig, original.Attributes);
			if (original.GetMethod != null) {
				Getter = new MethodDefAndRef(inModule, original.GetMethod, declaringType);
			}
			if (original.SetMethod != null) {
				Setter = new MethodDefAndRef(inModule, original.SetMethod, declaringType);
			}
		}

		public override string ToString() {
			return Definition.ToString();
		}

		public PropertyDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			return new PropertyDefAndRef(Module, Definition, type.Reference);
		}

		IMemberDefAndRef IMemberDefAndRef.AsMemberOfType(IHasTypeDefOrRef type) => AsMemberOfType(type);
	}
}
