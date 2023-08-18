using dnlib.DotNet;
using HookGenExtender.Core.ILGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff {

	/// <summary>
	/// A declared delegate type including all of its members. See <see cref="ILTools.CreateDelegateType(ExtensiblesGenerator, MethodSig, string)"/>.
	/// </summary>
	public sealed class DelegateTypeDefAndRef : IDelegateTypeWrapper {

		/// <summary>
		/// A reference to the original delegate type.
		/// </summary>
		public TypeDef DelegateType { get; }

		/// <summary>
		/// <see cref="DelegateType"/> as a <see cref="CachedTypeDef"/>.
		/// </summary>
		public CachedTypeDef CachedDelegateType { get; }

		/// <summary>
		/// The constructor of this delegate type. Its signature is always <c>(<see cref="object"/>, <see cref="nint"/>)</c>.
		/// </summary>
		public MethodDefAndRef Constructor { get; }

		/// <summary>
		/// The <c>Invoke()</c> method of this delegate.
		/// </summary>
		public MethodDefAndRef Invoke { get; }

		/// <summary>
		/// The <c>BeginInvoke()</c> method of this delegate.
		/// </summary>
		public MethodDefAndRef BeginInvoke { get; }

		/// <summary>
		/// The <c>EndInvoke()</c> method of this delegate.
		/// </summary>
		public MethodDefAndRef EndInvoke { get; }

		/// <summary>
		/// Create a new delegate type reference. Consider using <see cref="ILTools.CreateDelegateType(ExtensiblesGenerator, MethodSig, string)"/> or one of its variants.
		/// <para/>
		/// <strong>This will automatically register the type to the module.</strong>
		/// </summary>
		/// <param name="delegateType"></param>
		/// <param name="delegateConstructor"></param>
		/// <param name="invoke"></param>
		/// <param name="beginInvoke"></param>
		/// <param name="endInvoke"></param>
		public DelegateTypeDefAndRef(ModuleDef inModule, TypeDef delegateType, MethodDefAndRef delegateConstructor, MethodDefAndRef invoke, MethodDefAndRef beginInvoke, MethodDefAndRef endInvoke) {
			DelegateType = delegateType;
			CachedDelegateType = new CachedTypeDef(inModule, delegateType);
			Signature = delegateType.ToTypeSig();
			DelegateSignature = invoke.Definition.MethodSig;
			Constructor = delegateConstructor;
			Invoke = invoke;
			BeginInvoke = beginInvoke;
			EndInvoke = endInvoke;
		}

		IMethodDefOrRef IDelegateTypeWrapper.Constructor => Constructor.Definition;
		IMethodDefOrRef IDelegateTypeWrapper.Invoke => Invoke.Definition;
		IMethodDefOrRef IDelegateTypeWrapper.BeginInvoke => BeginInvoke.Definition;
		IMethodDefOrRef IDelegateTypeWrapper.EndInvoke => EndInvoke.Definition;
		ITypeDefOrRef IHasTypeDefOrRef.Reference => DelegateType;
		public MethodSig DelegateSignature { get; }
		public TypeSig Signature { get; }
	}
}
