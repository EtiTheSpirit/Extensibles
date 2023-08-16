using dnlib.DotNet;
using dnlib.DotNet.Emit;
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

		public CachedTypeDef Owner { get; internal set; }

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
		/// </summary>
		/// <param name="inModule">The module that owns this method.</param>
		/// <param name="name">The name of the method.</param>
		/// <param name="signature">The signature of this method.</param>
		/// <param name="declaringType">The type that this method belongs to.</param>
		/// <param name="attrs">The method attributes. Notably, this has some overlap with the signature - <strong>do not</strong> set the <see cref="MethodAttributes.Static"/> flag in this value, instead, set <see cref="CallingConventionSig.HasThis"/></param>
		/// <exception cref="ArgumentException">If <see cref="MethodAttributes.Static"/> is set.</exception>
		public MethodDefAndRef(ModuleDef inModule, string name, MethodSig signature, IMemberRefParent declaringType, MethodAttributes attrs) {
			Module = inModule;
			if (attrs.HasFlag(MethodAttributes.Static)) throw new ArgumentException($"For cleanliness, do not declare MethodAttributes.Static in {nameof(attrs)}. Instead, set the HasThis property of {nameof(signature)}");
			Definition = new MethodDefUser(name, signature, attrs);
			Reference = new MemberRefUser(inModule, name, signature, declaringType);
		}

		public MethodDefAndRef(ModuleDef inModule, MethodDef original, IMemberRefParent declaringType) {
			Module = inModule;
			Definition = original;
			Reference = new MemberRefUser(inModule, original.Name, original.MethodSig, declaringType);
		}

		public override string ToString() {
			return Definition.ToString();
		}

		public MethodDefAndRef AsMemberOfType(IHasTypeDefOrRef type) {
			return new MethodDefAndRef(Reference.Module, Definition, type.Reference);
		}

		IMemberDefAndRef IMemberDefAndRef.AsMemberOfType(IHasTypeDefOrRef type) => AsMemberOfType(type);

	}
}
