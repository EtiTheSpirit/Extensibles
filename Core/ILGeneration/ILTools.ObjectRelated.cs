using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class ILTools {

		private static ITypeDefOrRef _createDelegateType_multicastDelegate = null;
		private static TypeSig _createDelegateType_asyncCallbackSig = null;
		private static TypeSig _createDelegateType_iAsyncResultSig = null;
		private static MethodSig _createDelegateType_ctorSig = null;
		

		/// <summary>
		/// Creates a new delegate type that has the provided method signature, automatically creating all members with the correct metadata.
		/// </summary>
		/// <param name="mirrorGenerator">The mirror generator, for importing types.</param>
		/// <param name="signature">The signature of the method that the delegate represents.</param>
		/// <param name="name">The name of the delegate type.</param>
		/// <returns></returns>
		public static TypeDef CreateDelegateType(ExtensiblesGenerator main, MethodSig signature, string name) {
			_createDelegateType_multicastDelegate ??= main.Cache.Import(typeof(MulticastDelegate));
			_createDelegateType_ctorSig ??= MethodSig.CreateInstance(main.CorLibTypeSig(), main.CorLibTypeSig<object>(), main.CorLibTypeSig<nint>());
			_createDelegateType_asyncCallbackSig ??= main.Cache.ImportAsTypeSig(typeof(AsyncCallback));
			_createDelegateType_iAsyncResultSig ??= main.Cache.ImportAsTypeSig(typeof(IAsyncResult));

			TypeDefUser del = new TypeDefUser(name, _createDelegateType_multicastDelegate);
			del.IsSealed = true;

			// The methods in the delegate have no body, which makes this very easy.
			MethodDefUser constructor = new MethodDefUser(
				".ctor",
				_createDelegateType_ctorSig,
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig
			);
			constructor.CodeType = MethodImplAttributes.Runtime;

			List<TypeSig> types = new List<TypeSig>(signature.Params.Count + 2);
			IEnumerable<TypeSig> parameters = signature.Params;
			parameters = parameters
				.Where(param => param != signature.RetType)
				.Select(param => main.Cache.Import(param));

			types.AddRange(parameters);
			types.Add(_createDelegateType_asyncCallbackSig);
			types.Add(main.CorLibTypeSig<object>());

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
					_createDelegateType_iAsyncResultSig,
					types.ToArray()
				),
				commonAttrs
			);
			beginInvoke.CodeType = MethodImplAttributes.Runtime;

			MethodDefUser endInvoke = new MethodDefUser(
				"EndInvoke",
				MethodSig.CreateInstance(
					main.CorLibTypeSig<object>(),
					_createDelegateType_iAsyncResultSig
				),
				commonAttrs
			);
			endInvoke.CodeType = MethodImplAttributes.Runtime;

			del.Methods.Add(constructor);
			del.Methods.Add(invoke);
			del.Methods.Add(beginInvoke);
			del.Methods.Add(endInvoke);

			return del;
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
