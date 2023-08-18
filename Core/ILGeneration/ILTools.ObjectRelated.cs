using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff;
using HookGenExtender.Core.Utils.MemberMutation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class ILTools {

		private class DelegateTypeFactory {
			private ExtensiblesGenerator _main;
			private readonly ITypeDefOrRef _multicastDelegateTypeRef = null;
			private readonly TypeSig _asyncCallbackTypeSig = null;
			private readonly TypeSig _iAsyncResultTypeSig = null;
			private readonly MethodSig _ctorSig = null;
			private readonly MethodSig _endInvokeSig = null;

			public DelegateTypeFactory(ExtensiblesGenerator main) {
				_main = main;
				_multicastDelegateTypeRef = main.Cache.Import(typeof(MulticastDelegate));
				_asyncCallbackTypeSig = main.Cache.ImportAsTypeSig(typeof(AsyncCallback));
				_iAsyncResultTypeSig = main.Cache.ImportAsTypeSig(typeof(IAsyncResult));

				_ctorSig = MethodSig.CreateInstance(main.CorLibTypeSig(), main.CorLibTypeSig<object>(), main.CorLibTypeSig<nint>());
				_endInvokeSig = MethodSig.CreateInstance(_main.CorLibTypeSig<object>(), _iAsyncResultTypeSig);
			}

			private static readonly ConditionalWeakTable<ExtensiblesGenerator, DelegateTypeFactory> _lookup = new ConditionalWeakTable<ExtensiblesGenerator, DelegateTypeFactory>();
			public static DelegateTypeFactory GetOrCreate(ExtensiblesGenerator @for) {
				if (!_lookup.TryGetValue(@for, out DelegateTypeFactory factory)) {
					factory = new DelegateTypeFactory(@for);
					_lookup.Add(@for, factory);
				}
				return factory;
			}

			/// <summary>
			/// Wraps an existing delegate type.
			/// </summary>
			/// <param name="main"></param>
			/// <param name="signature"></param>
			/// <param name="delegateType"></param>
			/// <returns></returns>
			public DelegateTypeRef ReferenceDelegateType(MethodSig signature, ITypeDefOrRef delegateType) {
				List<TypeSig> types = new List<TypeSig>(signature.Params.Count + 2);
				IEnumerable<TypeSig> parameters = signature.Params;
				parameters = parameters
					.Where(param => param != signature.RetType)
					.Select(param => _main.Cache.Import(param));

				types.AddRange(parameters);
				types.Add(_asyncCallbackTypeSig);
				types.Add(_main.CorLibTypeSig<object>());
				MethodSig beginInvokeSig = MethodSig.CreateInstance(_iAsyncResultTypeSig, types.ToArray());

				return new DelegateTypeRef(
					signature,
					new MemberRefUser(_main.Extensibles, ".ctor", _ctorSig, delegateType),
					new MemberRefUser(_main.Extensibles, "Invoke", signature, delegateType),
					new MemberRefUser(_main.Extensibles, "BeginInvoke", beginInvokeSig, delegateType),
					new MemberRefUser(_main.Extensibles, "EndInvoke", _endInvokeSig, delegateType),
					delegateType
				);
			}


			/// <summary>
			/// Creates a new delegate type that has the provided method signature, automatically creating all members with the correct metadata.
			/// <para/>
			/// The new type is <strong>automatically registered to the Extensibles module.</strong>
			/// </summary>
			/// <param name="mirrorGenerator">The mirror generator, for importing types.</param>
			/// <param name="signature">The signature of the method that the delegate represents.</param>
			/// <param name="name">The name of the delegate type.</param>
			/// <returns></returns>
			public DelegateTypeDefAndRef CreateDelegateType(MethodSig signature, string name) {
				TypeDefUser del = new TypeDefUser(name, _multicastDelegateTypeRef);
				del.IsSealed = true;

				// The methods in the delegate have no body, which makes this very easy.
				MethodDefUser constructor = new MethodDefUser(
					".ctor",
					_ctorSig,
					MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig
				);
				constructor.CodeType = MethodImplAttributes.Runtime;

				List<TypeSig> types = new List<TypeSig>(signature.Params.Count + 2);
				IEnumerable<TypeSig> parameters = signature.Params;
				parameters = parameters
					.Where(param => param != signature.RetType)
					.Select(param => _main.Cache.Import(param));

				types.AddRange(parameters);
				types.Add(_asyncCallbackTypeSig);
				types.Add(_main.CorLibTypeSig<object>());

				const MethodAttributes commonAttrs = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;

				MethodDefUser invoke = new MethodDefUser(
					"Invoke",
					signature,
					commonAttrs
				);
				invoke.CodeType = MethodImplAttributes.Runtime;

				MethodDefUser beginInvoke = new MethodDefUser(
					"BeginInvoke",
					MethodSig.CreateInstance(
						_iAsyncResultTypeSig,
						types.ToArray()
					),
					commonAttrs
				);
				beginInvoke.CodeType = MethodImplAttributes.Runtime;

				MethodDefUser endInvoke = new MethodDefUser(
					"EndInvoke",
					_endInvokeSig,
					commonAttrs
				);
				endInvoke.CodeType = MethodImplAttributes.Runtime;

				del.Methods.Add(constructor);
				del.Methods.Add(invoke);
				del.Methods.Add(beginInvoke);
				del.Methods.Add(endInvoke);

				return new DelegateTypeDefAndRef(
					_main.Extensibles,
					del,
					new MethodDefAndRef(_main.Extensibles, constructor, del),
					new MethodDefAndRef(_main.Extensibles, invoke, del),
					new MethodDefAndRef(_main.Extensibles, beginInvoke, del),
					new MethodDefAndRef(_main.Extensibles, endInvoke, del)
				);
			}
		}

		/// <summary>
		/// Wraps an existing delegate type.
		/// </summary>
		/// <param name="main">The extensibles generator, for importing types.</param>
		/// <param name="signature">The signature of the method that the delegate represents. This should be an instance signature.</param>
		/// <param name="delegateType">An existing delegate type.</param>
		/// <returns></returns>
		public static DelegateTypeRef ReferenceDelegateType(ExtensiblesGenerator main, MethodSig signature, ITypeDefOrRef delegateType) {
			return DelegateTypeFactory.GetOrCreate(main).ReferenceDelegateType(signature, delegateType);
		}

		/// <summary>
		/// Wraps an existing delegate type after importing it.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="delegateType">An original delegate type to import.</param>
		/// <returns></returns>
		public static DelegateTypeRef ReferenceDelegateType(ExtensiblesGenerator main, TypeDef delegateType) {
			MethodSig newSig = delegateType.FindMethod("Invoke").MethodSig.CloneAndImport(main);
			ITypeDefOrRef imported = main.Cache.Import(delegateType);
			return ReferenceDelegateType(main, newSig, imported);
		}

		/// <summary>
		/// Creates a new delegate type that has the provided method signature, automatically creating all members with the correct metadata.
		/// <para/>
		/// The new type is <strong>automatically registered to the Extensibles module.</strong>
		/// </summary>
		/// <param name="main">The extensibles generator, for importing types.</param>
		/// <param name="signature">The signature of the method that the delegate represents. This should be an instance signature.</param>
		/// <param name="name">The name of the delegate type.</param>
		/// <returns></returns>
		public static DelegateTypeDefAndRef CreateDelegateType(ExtensiblesGenerator main, MethodSig signature, string name) {
			return DelegateTypeFactory.GetOrCreate(main).CreateDelegateType(signature, name);
		}

		/// <summary>
		/// Sets the name of the parameter at the given index. Index 0 represents the first argument, excluding <see langword="this"/> on instance methods.
		/// </summary>
		/// <param name="onMethod">The method to modify.</param>
		/// <param name="index">The parameter index. On instance methods, 0 represents the first user-defined argument (that is, it does <em>not</em> represent the "<see langword="this"/>" keyword)</param>
		/// <param name="name">The name of this parameter.</param>
		public static void SetParameterName(this MethodDef onMethod, int index, string name) {
			Parameter param = onMethod.Parameters[index];
			if (!param.HasParamDef) param.CreateParamDef();
			param.Name = name;
		}

		/// <summary>
		/// Alias to <see cref="Parameter.CreateParamDef"/> that returns the existing or new instance.
		/// </summary>
		/// <param name="param"></param>
		/// <returns></returns>
		public static ParamDef GetOrCreateParamDef(this Parameter param) {
			param.CreateParamDef();
			return param.ParamDef;
		}

		/// <summary>
		/// Given the definition of a member in the original module, this creates a <see cref="MemberRefUser"/> to that member.
		/// </summary>
		/// <param name="original">The original member.</param>
		/// <param name="main">The mirror generator, for importing types.</param>
		/// <param name="in">The parent type, useful for generics.</param>
		/// <returns></returns>
		/// <exception cref="NotSupportedException"></exception>
		public static MemberRef MakeMemberReference(this IMemberDef original, ExtensiblesGenerator main, ITypeDefOrRef @in = null, bool import = true) {
			if (original.IsMethodDef) {
				if (import) {
					return main.Cache.Import((MethodDef)original);
				}

				MethodSig signature = ((MethodDef)original).MethodSig;
				ITypeDefOrRef declaringType = @in ?? original.DeclaringType;
				return new MemberRefUser(main.Extensibles, original.Name, signature, declaringType);

			} else if (original.IsFieldDef) {
				if (import) {
					return main.Cache.Import((FieldDef)original);
				}

				FieldSig signature = ((FieldDef)original).FieldSig;
				ITypeDefOrRef declaringType = @in ?? original.DeclaringType;
				return new MemberRefUser(main.Extensibles, original.Name, signature, declaringType);
			} else if (original.IsPropertyDef) {
				throw new NotSupportedException("To reference properties, explicitly reference their get or set method instead.");
			}
			throw new NotSupportedException("The provided member type is not supported.");
		}
	}
}
