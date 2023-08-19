using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace HookGenExtender.Core.Utils.Ext {
	public static class NameConversion {

		public static string NameOfType(TypeSig type) => NameOfType(type, false);

		private static string NameOfType(TypeSig type, bool isArray) {
			if (!isArray) {
				if (type.FullName == "System.Boolean") return "bool";
				if (type.FullName == "System.SByte") return "sbyte";
				if (type.FullName == "System.Byte") return "byte";
				if (type.FullName == "System.Int16") return "short";
				if (type.FullName == "System.UInt16") return "ushort";
				if (type.FullName == "System.Int32") return "int";
				if (type.FullName == "System.UInt32") return "uint";
				if (type.FullName == "System.Int64") return "long";
				if (type.FullName == "System.UInt64") return "ulong";
				if (type.FullName == "System.IntPtr") return "nint";
				if (type.FullName == "System.UIntPtr") return "nuint";
				if (type.FullName == "System.Single") return "float";
				if (type.FullName == "System.Double") return "double";
				if (type.FullName == "System.Void") return "void";
				if (type.FullName == "System.String") return "string";
				if (type.FullName == "System.Object") return "object";
			} else {
				if (type.FullName == "System.Boolean") return type.GetName();
				if (type.FullName == "System.SByte") return type.GetName();
				if (type.FullName == "System.Byte") return type.GetName();
				if (type.FullName == "System.Int16") return type.GetName();
				if (type.FullName == "System.UInt16") return type.GetName();
				if (type.FullName == "System.Int32") return type.GetName();
				if (type.FullName == "System.UInt32") return type.GetName();
				if (type.FullName == "System.Int64") return type.GetName();
				if (type.FullName == "System.UInt64") return type.GetName();
				if (type.FullName == "System.IntPtr") return type.GetName();
				if (type.FullName == "System.UIntPtr") return type.GetName();
				if (type.FullName == "System.Single") return type.GetName();
				if (type.FullName == "System.Double") return type.GetName();
				if (type.FullName == "System.Void") return type.GetName();
				if (type.FullName == "System.String") return type.GetName();
				if (type.FullName == "System.Object") return type.GetName();
			}
			if (type.IsArray) {
				string result;
				switch (type.ElementType) {
					case ElementType.Void:
					case ElementType.Boolean:
					case ElementType.Char:
					case ElementType.I1:
					case ElementType.U1:
					case ElementType.I2:
					case ElementType.U2:
					case ElementType.I4:
					case ElementType.U4:
					case ElementType.I8:
					case ElementType.U8:
					case ElementType.R4:
					case ElementType.R8:
					case ElementType.String:
					case ElementType.TypedByRef:
					case ElementType.I:
					case ElementType.U:
					case ElementType.Object:
					case ElementType.ValueType:
					case ElementType.Class:
						result = type.GetName();
						break;
					default:
						result = NameOfType(type.Next, true);
						break;
				}
				return result + "Array";
			}
			string name = type.GetName();
			name = name.Replace("`", string.Empty);
			if (name.EndsWith("&")) {
				name = "ref" + name.Substring(0, name.Length - 1);
			}
			if (name.EndsWith("[]")) {
				name = name.Substring(0, name.Length - 2) + "Array";
			}
			return name;
		}
	}
}
