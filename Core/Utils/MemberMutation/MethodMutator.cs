using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using HookGenExtender.Core.ILGeneration;
using HookGenExtender.Core.Utils.DNLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.Utils.MemberMutation {
	public static class MethodMutator {

		/// <summary>
		/// Provided with a method from the game, the extensible type binding for the type declaring that method, and the generator main system, this will
		/// create a new method definition (via <see cref="MethodDefAndRef"/>) of a method with a signature identical to that of its original counterpart.
		/// <para/>
		/// This excludes custom attributes, and does not copy the body of the method.
		/// </summary>
		/// <param name="gameOriginalMethod"></param>
		/// <param name="main"></param>
		/// <returns></returns>
		public static MethodDefAndRef CloneMethodDeclarationFromGame(this MethodDef gameOriginalMethod, ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers) {
			string name = gameOriginalMethod.Name;
			MethodSig signature = gameOriginalMethod.MethodSig.CloneAndImport(main);
			MethodAttributes attrs = gameOriginalMethod.Attributes;

			MethodDefAndRef result = new MethodDefAndRef(main, name, signature, coreMembers.type.ExtensibleType.Reference, attrs);
			int paramCount = gameOriginalMethod.GetParamCount();
			for (int i = 0; i < paramCount; i++) {
				result.Definition.SetParameterName(i, gameOriginalMethod.GetParameterName(i));
			}
			return result;
		}

		/// <summary>
		/// Clones the provided <paramref name="signature"/> such that the clone has had all of its types imported.
		/// </summary>
		/// <param name="signature"></param>
		/// <returns></returns>
		public static MethodSig CloneAndImport(this MethodSig signature, ExtensiblesGenerator main) {
			MethodSig result = signature.DeepClone();
			result.RetType = main.Cache.Import(signature.RetType);
			result.Params.Clear();
			foreach (TypeSig param in signature.Params.Select(param => main.Cache.Import(param))) {
				result.Params.Add(param);
			}
			if (result.ParamsAfterSentinel != null) {
				result.ParamsAfterSentinel.Clear();
				foreach (TypeSig param in signature.ParamsAfterSentinel?.Select(param => main.Cache.Import(param))) {
					result.ParamsAfterSentinel.Add(param);
				}
			}
			return result;
		}

		public static FieldSig CloneAndImport(this FieldSig signature, ExtensiblesGenerator main) {
			FieldSig result = signature.Clone();
			result.Type = main.Cache.Import(signature.Type);
			return result;
		}

		public static PropertySig CloneAndImport(this PropertySig signature, ExtensiblesGenerator main) {
			PropertySig result = signature.DeepClone();
			result.RetType = main.Cache.Import(signature.RetType);
			result.Params.Clear();
			foreach (TypeSig param in signature.Params.Select(param => main.Cache.Import(param))) {
				result.Params.Add(param);
			}
			if (result.ParamsAfterSentinel != null) {
				result.ParamsAfterSentinel.Clear();
				foreach (TypeSig param in signature.ParamsAfterSentinel?.Select(param => main.Cache.Import(param))) {
					result.ParamsAfterSentinel.Add(param);
				}
			}
			return result;
		}

	}
}
