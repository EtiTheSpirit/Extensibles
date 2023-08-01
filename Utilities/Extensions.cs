using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	public static class Extensions {

		/// <summary>
		/// Returns whether or not a property is static.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		public static bool IsStatic(this PropertyDef property) => property.GetMethod?.IsStatic ?? property.SetMethod.IsStatic;
		
		/// <summary>
		/// Returns whether or not a property is abstract.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		public static bool IsAbstract(this PropertyDef property) => property.GetMethod?.IsAbstract ?? property.SetMethod.IsAbstract;

		/// <summary>
		/// Clone a Parameter List
		/// </summary>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static ParameterList Clone(this ParameterList parameters) {
			return new ParameterList(parameters.Method, parameters.Method.DeclaringType);
		}

		public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> create) where TKey : notnull {
			if (dict.TryGetValue(key, out TValue? value)) {
				return value;
			}
			value = create(key);
			dict[key] = value;
			return value;
		}

	}
}
