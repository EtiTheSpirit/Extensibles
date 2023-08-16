using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ReferenceHelpers {
	public static class GeneralTypeTools {

		public static bool IsCorLibType(this ITypeDefOrRef type) => type.ToTypeSig().IsCorLibType;

	}
}
