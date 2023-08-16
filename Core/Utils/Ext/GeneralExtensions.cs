using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.Utils.Ext {
	public static class GeneralExtensions {
		/// <summary>
		/// Returns the <typeparamref name="TValue"/> associated with the provided <typeparamref name="TKey"/> in the given dictionary.
		/// If this element does not exist, <paramref name="create"/> is called with the key to create its corresponding value, then that value
		/// is returned.
		/// </summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="dict"></param>
		/// <param name="key"></param>
		/// <param name="create"></param>
		/// <returns></returns>
		public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> create) where TKey : notnull {
			if (dict.TryGetValue(key, out TValue value)) {
				return value;
			}
			value = create(key);
			dict[key] = value;
			return value;
		}
	}
}
