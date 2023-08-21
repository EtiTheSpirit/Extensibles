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
		public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> create) where TKey : notnull {
			if (dict.TryGetValue(key, out TValue value)) {
				return value;
			}
			value = create(key);
			dict[key] = value;
			return value;
		}

		/// <summary>
		/// Attempts to resolve the element directly following the provided item in the list. If the provided item is at the end of the list,
		/// this method will return false, and the <paramref name="result"/> will be set to <paramref name="item"/>.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list"></param>
		/// <param name="item"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		/// <exception cref="KeyNotFoundException">If the provided item is not in the list.</exception>
		public static bool TryGetElementAfter<T>(this IList<T> list, T item, out T result) {
			int i = list.IndexOf(item);
			if (i == -1) throw new KeyNotFoundException("The provided item does not exist in this list.");

			if (i == list.Count - 1) {
				result = item;
				return false;
			}

			result = list[i + 1];
			return true;
		}
	}
}
