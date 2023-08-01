using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	public static class ILGenerators {

		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a getter for the mirror property that calls the getter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="original"></param>
		/// <param name="mirrorClassOrgRefName">The name of the property storing a reference to the mirror's original type.</param>
		/// <returns></returns>
		public static MethodInfo CreateGetter(PropertyDefUser mirror, PropertyDef original, string mirrorClassOrgRefName) {
			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(original.PropertySig.RetType), original.GetMethod.Attributes);
			CilBody methodBody = new CilBody();
			
		}

	}
}
