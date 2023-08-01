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
				
	}
}
