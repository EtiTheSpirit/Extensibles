using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	public static class Extensions {
#if NET6_0_OR_GREATER
		private static Dictionary<Type, Delegate> _defaultProviders = new Dictionary<Type, Delegate>();
#endif

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
			if (dict.TryGetValue(key, out TValue value)) {
				return value;
			}
			value = create(key);
			dict[key] = value;
			return value;
		}


		/// <summary>
		/// Returns <see langword="true"/> if the given <paramref name="value"/> is equal to <c><see langword="default"/>(<typeparamref name="T"/>)</c>. Respects the implementation of <see cref="IEquatable{T}"/>, where applicable.
		/// </summary>
		/// <typeparam name="T">The type to compare to.</typeparam>
		/// <param name="value">The value to check.</param>
		/// <returns><see langword="true"/> if value is equal to <c><see langword="default"/>(<typeparamref name="T"/>)</c>, <see langword="false"/> if not.</returns>
		public static bool IsDefault<T>(this T value) => EqualityComparer<T>.Default.Equals(value, default);

		/// <summary>
		/// Returns the <see langword="default"/> value of the provided <see cref="Type"/>, for use in a context where the <see langword="default"/> operator is not available (such as outside of a generic context).
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Default(this Type type) {
			if (type == null) throw new ArgumentNullException(nameof(type));
			if (type.IsValueType) {
#if NET6_0_OR_GREATER // C# 10 was introduced with .NET 6
				/*
				 * > In C# 10 and later, a structure type (which is a value type) may have an explicit parameterless constructor 
				 * > that may produce a non-default value of the type. Thus, we recommend using the default operator or the default 
				 * > literal to produce the default value of a type.
				 * 
				 * SRC: C# documentation, https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/default-values
				 */
				if (_defaultProviders.TryGetValue(type, out Delegate? @default)) {
					return @default.DynamicInvoke();
				}
				@default = Expression.Lambda(Expression.Convert(Expression.Default(type), typeof(object))).Compile();
				_defaultProviders[type] = @default;
				return @default.DynamicInvoke();
#else
				return Activator.CreateInstance(type);
#endif
			}
			return null;
		}

		/// <summary>
		/// Sets the name of the parameter at the given index. Index 0 represents the first argument, excluding <see langword="this"/> on instance methods.
		/// </summary>
		/// <param name="onMethod">The method to modify.</param>
		/// <param name="index">The parameter index. On instance methods, 0 represents the first user-defined argument (that is, it does <em>not</em> represent <see langword="this"/>)</param>
		/// <param name="name">The name of this parameter.</param>
		public static void SetParameterName(this MethodDef onMethod, int index, string name) {
			Parameter param = onMethod.Parameters[index];
			if (!param.HasParamDef) param.CreateParamDef();
			param.Name = name;
		}

	}
}
