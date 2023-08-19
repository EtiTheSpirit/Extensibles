using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.Utils.MemberMutation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// Simultaneously contains a method definition and a reference to it.
	/// </summary>
	public sealed class MethodDefAndRef : IMemberDefAndRef<MethodDefAndRef, MethodDef> {

		/// <summary>
		/// The type that owns this method. This is set by <see cref="CachedTypeDef.AddMethod(MethodDefAndRef)"/>.
		/// </summary>
		public CachedTypeDef Owner { get; internal set; }

		/// <summary>
		/// The name of the method.
		/// </summary>
		public string Name => Definition.Name;

		/// <summary>
		/// The <see cref="ExtensiblesGenerator"/> that manages this type.
		/// </summary>
		public ExtensiblesGenerator Generator { get; }

		/// <summary>
		/// The module this method exists in.
		/// </summary>
		public ModuleDef Module { get; }

		/// <summary>
		/// The definition of the method.
		/// </summary>
		public MethodDef Definition { get; }

		IMemberDef IMemberDefAndRef.Definition => Definition;

		public IMemberRef Reference { get; }


		/// <summary>
		/// A reference to the method body.
		/// </summary>
		public CilBody Body {
			get {
				return Definition.Body;
			}
			set {
				Definition.Body = value;
			}
		}

		/// <summary>
		/// Gets a reference to the method body, creating it and storing it if it does not already exist.
		/// </summary>
		/// <returns></returns>
		public CilBody GetOrCreateBody() {
			CilBody body = Body;
			if (body == null) {
				body = new CilBody();
				Body = body;
			}
			return body;
		}

		/// <summary>
		/// Create a new method, and then make the method reference itself.
		/// <para/>
		/// Despite receiving the declaring type, this <strong>DOES NOT</strong> register the method with the type!
		/// </summary>
		/// <param name="inModule">The module that owns this method.</param>
		/// <param name="name">The name of the method.</param>
		/// <param name="signature">The signature of this method.</param>
		/// <param name="declaringType">The type that this method belongs to.</param>
		/// <param name="attrs">The method attributes. Notably, this has some overlap with the signature - <strong>do not</strong> set the <see cref="MethodAttributes.Static"/> flag in this value, instead, set <see cref="CallingConventionSig.HasThis"/></param>
		/// <exception cref="ArgumentException">If <see cref="MethodAttributes.Static"/> is set.</exception>
		public MethodDefAndRef(ExtensiblesGenerator main, string name, MethodSig signature, IMemberRefParent declaringType, MethodAttributes attrs) {
			Generator = main;
			Module = main.Extensibles;
			if (attrs.HasFlag(MethodAttributes.Static)) throw new ArgumentException($"For cleanliness, do not declare MethodAttributes.Static in {nameof(attrs)}. Instead, set the HasThis property of {nameof(signature)}");
			if (!signature.HasThis) attrs |= MethodAttributes.Static;
			Definition = new MethodDefUser(name, signature, attrs);
			Definition.IsNoOptimization = true; // TODO: Is this necessary?
			Definition.IsNoInlining = true; // This is definitely necessary as code flow is *extremely* important to Extensibles.
			Reference = new MemberRefUser(Module, name, signature, declaringType);
		}
		/// <summary>
		/// Load an existing method, and then make a reference to that method.
		/// <para/>
		/// Despite receiving the declaring type, this <strong>DOES NOT</strong> register the method with the type!
		/// This allows this constructor to be used on methods owned by other modules, to bulk the definition and reference together.
		/// </summary>
		/// <param name="inModule">The module that owns this method.</param>
		/// <param name="original">The original method to store and reference.</param>
		/// <param name="declaringType">The type that this method belongs to.</param>
		/// <exception cref="ArgumentException">If <see cref="MethodAttributes.Static"/> is set.</exception>
		public MethodDefAndRef(ExtensiblesGenerator main, MethodDef original, IMemberRefParent declaringType, bool import) {
			Generator = main;
			Module = main.Extensibles;
			Definition = original;
			MethodSig sig = original.MethodSig;
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

		public MethodDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			return new MethodDefAndRef(Generator, Definition, type.Reference, false);
		}

		public MethodDefAndRef AsMemberOfType(ITypeDefOrRef type) {
			return new MethodDefAndRef(Generator, Definition, type, false);
		}

		IMemberDefAndRef IMemberDefAndRef.AsMemberOfType(IHasTypeDefOrRef type) => AsMemberOfType(type);

	}
}
