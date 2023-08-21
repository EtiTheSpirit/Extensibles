using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ILGeneration {
	public static class CommonAttributes {

		public const MethodAttributes LOCKED_METHOD = MethodAttributes.PrivateScope;
		public const FieldAttributes LOCKED_FIELD = FieldAttributes.PrivateScope;
		public const MethodAttributes SPECIAL_LOCKED_METHOD = LOCKED_METHOD | MethodAttributes.SpecialName;
		public const FieldAttributes SPECIAL_LOCKED_FIELD = LOCKED_FIELD | FieldAttributes.SpecialName;

		[Obsolete("When constructing static methods, use MethodSig.CreateStatic instead of this.")]
		public const MethodAttributes LOCKED_STATIC_METHOD = LOCKED_METHOD | MethodAttributes.Static;
		public const FieldAttributes LOCKED_STATIC_FIELD = LOCKED_FIELD | FieldAttributes.Static;

		[Obsolete("When constructing static methods, use MethodSig.CreateStatic instead of this.")]
		public const MethodAttributes SPECIAL_LOCKED_STATIC_METHOD = SPECIAL_LOCKED_METHOD | MethodAttributes.Static;
		public const FieldAttributes SPECIAL_LOCKED_STATIC_FIELD = SPECIAL_LOCKED_FIELD | FieldAttributes.Static;


		public const MethodAttributes PROTECTED_METHOD = MethodAttributes.Family;
		public const MethodAttributes PRIVATE_PROTECTED_METHOD = MethodAttributes.FamANDAssem;
		public const MethodAttributes PROTECTED_INTERNAL_METHOD = MethodAttributes.FamORAssem;
		public const MethodAttributes INTERNAL_METHOD = MethodAttributes.Assembly;

		public const FieldAttributes PROTECTED_FIELD = FieldAttributes.Family;
		public const FieldAttributes PRIVATE_PROTECTED_FIELD = FieldAttributes.FamANDAssem;
		public const FieldAttributes PROTECTED_INTERNAL_FIELD = FieldAttributes.FamORAssem;
		public const FieldAttributes INTERNAL_FIELD = FieldAttributes.Assembly;


		[Obsolete("When constructing static methods, use MethodSig.CreateStatic instead of this.")]
		public const MethodAttributes PROTECTED_STATIC_METHOD = MethodAttributes.Family | MethodAttributes.Static;
		[Obsolete("When constructing static methods, use MethodSig.CreateStatic instead of this.")]
		public const MethodAttributes PRIVATE_PROTECTED_STATIC_METHOD = MethodAttributes.FamANDAssem | MethodAttributes.Static;
		[Obsolete("When constructing static methods, use MethodSig.CreateStatic instead of this.")]
		public const MethodAttributes PROTECTED_INTERNAL_STATIC_METHOD = MethodAttributes.FamORAssem | MethodAttributes.Static;
		[Obsolete("When constructing static methods, use MethodSig.CreateStatic instead of this.")]
		public const MethodAttributes INTERNAL_STATIC_METHOD = MethodAttributes.Assembly | MethodAttributes.Static;

		public const FieldAttributes PROTECTED_STATIC_FIELD = FieldAttributes.Family | FieldAttributes.Static;
		public const FieldAttributes PRIVATE_PROTECTED_STATIC_FIELD = FieldAttributes.FamANDAssem | FieldAttributes.Static;
		public const FieldAttributes PROTECTED_INTERNAL_STATIC_FIELD = FieldAttributes.FamORAssem | FieldAttributes.Static;
		public const FieldAttributes INTERNAL_STATIC_FIELD = FieldAttributes.Assembly | FieldAttributes.Static;


	}
}
