using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.Utils.Ext {
	public static class TypeExtensions {


		/// <summary>
		/// Make a new generic type from this type and the provided parameters. You should store this result.
		/// </summary>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static GenericInstanceTypeDef MakeGenericType(this ITypeDefOrRef tdor, params TypeSig[] parameters) {
			if (tdor == null) throw new NullReferenceException();
			return new GenericInstanceTypeDef(tdor, parameters);
		}

		/// <summary>
		/// Make a new generic type from this type and the provided parameters. You should store this result.
		/// </summary>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static GenericInstanceTypeDef MakeGenericType(this IHasTypeDefOrRef tdor, params TypeSig[] parameters) {
			if (tdor == null) throw new NullReferenceException();
			return new GenericInstanceTypeDef(tdor.Reference, parameters);
		}

		/// <summary>
		/// Returns whether or not the type is compiler generated (via being decorated with <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/>)
		/// </summary>
		/// <param name="hasCustomAttribute"></param>
		/// <returns></returns>
		public static bool IsCompilerGenerated(this IHasCustomAttribute hasCustomAttribute) {
			return hasCustomAttribute.CustomAttributes.FirstOrDefault(attr => attr.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute") != null;
		}

		

		/// <summary>
		/// Returns whether or not a property is static.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		public static bool IsStatic(this PropertyDef property) => property.GetMethod?.IsStatic ?? property.SetMethod?.IsStatic ?? throw new InvalidOperationException("The property has no getter nor setter.");

		/// <summary>
		/// Returns whether or not a type is static.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool IsStatic(this TypeDef type) => type.IsAbstract && type.IsSealed;

		/// <summary>
		/// Returns whether or not a property is abstract.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		public static bool IsAbstract(this PropertyDef property) => property.GetMethod?.IsAbstract ?? property.SetMethod?.IsAbstract ?? throw new InvalidOperationException("The property has no getter nor setter.");

	}
}
