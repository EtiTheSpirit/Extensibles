using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ReferenceHelpers {
	public static class GeneralTypeTools {

		public static bool IsCorLibType(this ITypeDefOrRef type) => type.ToTypeSig().IsCorLibType;


		/// <summary>
		/// Selects the parameters from a method and sorts them into three output members.
		/// </summary>
		/// <param name="fromMethod"></param>
		/// <param name="main">A mirror generator to import types. If <paramref name="import"/> is <see langword="false"/>, this can be null.</param>
		/// <param name="inputParameters">The ordered parameters that are passed into the method in C# source code (excluding this and the return parameter)</param>
		/// <param name="returnParameter">This will never be <see langword="null"/>, unless <paramref name="main"/> is <see langword="null"/> in which case this might be null. The return type, or <see cref="CorLibTypes.Void"/></param>
		/// <param name="thisParameter">The type of the <see langword="this"/> parameter, or null if the method is static.</param>
		/// <param name="import">If true, types will be imported to ensure they can be exported in the extensibles DLL.</param>
		public static void SelectParameters(this MethodDef fromMethod, ExtensiblesGenerator main, out TypeSig[] inputParameters, out TypeSig returnParameter, out TypeSig thisParameter, bool import = true) {
			if (main == null && import) throw new ArgumentException($"{nameof(import)} cannot be true if {nameof(main)} is null!");

			returnParameter = fromMethod.ReturnType ?? main?.Extensibles.CorLibTypes.Void;
			thisParameter = fromMethod.HasThis ? fromMethod.DeclaringType.ToTypeSig() : null;
			inputParameters = fromMethod.Parameters.Where(param => param.IsNormalMethodParameter).Select(param => {
				if (import) return main.Cache.Import(param.Type);
				return param.Type;
			}).ToArray();

			if (import) {
				if (thisParameter != null) thisParameter = main.Cache.Import(thisParameter);
				returnParameter = main.Cache.Import(returnParameter);
			}
		}

	}
}
