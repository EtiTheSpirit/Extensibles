using dnlib.DotNet;
using HookGenExtender.Core.Utils.MemberMutation;
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
		/// The <see cref="ExtensiblesGenerator"/> that manages this type.
		/// </summary>
		public ExtensiblesGenerator Generator { get; }

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
		/// <para/>
		/// Despite receiving the declaring type, this <strong>DOES NOT</strong> register the method with the type!
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="name"></param>
		/// <param name="signature"></param>
		/// <param name="declaringType"></param>
		/// <param name="attrs"></param>
		/// <param name="getterAttributes">The attributes of a pre-created, empty, getter method. Use null to have no getter. Note that <see cref="MethodAttributes.HideBySig"/> and <see cref="MethodAttributes.SpecialName"/> are both implicit and thus not needed.</param>
		/// <param name="setterAttributes">The attributes of a pre-created, empty, setter method. Use null to have no setter. Note that <see cref="MethodAttributes.HideBySig"/> and <see cref="MethodAttributes.SpecialName"/> are both implicit and thus not needed.</param>
		public PropertyDefAndRef(ExtensiblesGenerator main, string name, PropertySig signature, IMemberRefParent declaringType, PropertyAttributes attrs = default, MethodAttributes? getterAttributes = default, MethodAttributes? setterAttributes = default) {
			const MethodAttributes requiredAttributes = MethodAttributes.SpecialName | MethodAttributes.HideBySig;
			Generator = main;
			Definition = new PropertyDefUser(name, signature, attrs);
			Module = main.Extensibles;

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
				if ((getterAttributes.Value & requiredAttributes) != 0) {
					//throw new ArgumentException($"{nameof(getterAttributes)} has either SpecialName or HideBySig set")
					// ^ Exception is too aggressive...
					// I need a way to warn for this.
				}
				MethodSig getterSig = MethodSig.CreateStatic(signature.RetType);
				getterSig.HasThis = !isStatic;
				Getter = new MethodDefAndRef(main, $"get_{name}", getterSig, declaringType, getterAttributes.Value | requiredAttributes);
				Definition.GetMethod = Getter.Definition;
			}
			if (setterAttributes != null) {
				if ((setterAttributes.Value & requiredAttributes) != 0) {

				}
				MethodSig setterSig = MethodSig.CreateStatic(main.CorLibTypeSig(), signature.RetType);
				setterSig.HasThis = !isStatic;
				Setter = new MethodDefAndRef(main, $"set_{name}", setterSig, declaringType, setterAttributes.Value | requiredAttributes);
				Definition.SetMethod = Setter.Definition;
			}
		}

		public PropertyDefAndRef(ExtensiblesGenerator main, PropertyDef original, IMemberRefParent declaringType, bool import) {
			Generator = main;
			Module = main.Extensibles;
			PropertySig sig = original.PropertySig;
			if (import) sig = sig.CloneAndImport(main);
			Definition = new PropertyDefUser(original.Name, sig, original.Attributes);

			IMemberRefParent newParent = declaringType;
			if (import && declaringType is ITypeDefOrRef tdor && tdor.Module != Module) {
				newParent = main.Cache.Import(tdor) as ITypeDefOrRef;
			}

			if (original.GetMethod != null) {
				Getter = new MethodDefAndRef(main, original.GetMethod, newParent, import);
				Definition.GetMethod = Getter.Definition;
			}
			if (original.SetMethod != null) {
				Setter = new MethodDefAndRef(main, original.SetMethod, newParent, import);
				Definition.SetMethod = Setter.Definition;
			}
		}

		/// <summary>
		/// Create a new property from the generated getter and setter methods.
		/// </summary>
		/// <param name="inModule"></param>
		/// <param name="signature"></param>
		/// <param name="getter"></param>
		/// <param name="setter"></param>
		public PropertyDefAndRef(ModuleDef inModule, string name, PropertySig signature, MethodDefAndRef getter, MethodDefAndRef setter, PropertyAttributes attrs = default) {
			Module = inModule;
			Definition = new PropertyDefUser(name, signature, attrs);
			Getter = getter;
			Setter = setter;
			Definition.GetMethod = Getter?.Definition;
			Definition.SetMethod = Setter?.Definition;
		}

		public override string ToString() {
			return Definition.ToString();
		}

		public PropertyDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			return new PropertyDefAndRef(Generator, Definition, type.Reference, false);
		}

		IMemberDefAndRef IMemberDefAndRef.AsMemberOfType(IHasTypeDefOrRef type) => AsMemberOfType(type);
	}
}
