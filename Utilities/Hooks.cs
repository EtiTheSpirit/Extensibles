// #define DEBUG_HELPER_ENABLED
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	public static class Hooks {

		/// <summary>
		/// Returns the delegate <c>orig_</c> provided by BepInEx hooks for the provided <paramref name="originalMethod"/>.
		/// </summary>
		/// <param name="hookType"></param>
		/// <param name="originalMethod"></param>
		/// <returns></returns>
		public static (TypeDef?, MethodDef?, EventDef?) TryGetOrigDelegateForMethod(this TypeDef hookType, MethodDef originalMethod) {
			// BIE hooks follow 3 simple rules:
			// If the method is singular (no overloads), the name is orig_MethodName
			// If the method has overloads, the name is orig_MethodName_Types_Types_Types (where Types are the human readable names i.e. int not Int32)
			// If the method is an interface implementation, it includes the name of the interface as well as the types.

			// Thankfully, the name match can be left to the first rule. Rather than trying to jank my way through the names, I'll match parameters instead.
			string methodName = originalMethod.Name.Replace(".", "_"); // The replacement of . to _ accounts for interface implementations.
			string origName = $"orig_{methodName}";
#if DEBUG_HELPER_ENABLED
			TypeDef[] types = hookType.NestedTypes.Where(type => type.IsDelegate && type.Name.StartsWith(origName)).ToArray();
#else
			IEnumerable<TypeDef> types = hookType.NestedTypes.Where(type => type.IsDelegate && type.Name.StartsWith(origName));
#endif

			TypeDef? type;
			EventDef? evt = null;
			if (types.Count() == 1) {
				type = types.First();
				evt = hookType.FindEvent(((string)type.Name)[5..]);
				return (type, type.FindMethod("Invoke"), evt);
			}

			// Match by parameters
			// This is yucky lol
#if DEBUG_HELPER_ENABLED
			TypeSig[] originalTypes = originalMethod.Parameters.Select(param => param.Type).ToArray();
			type = types.FirstOrDefault(type => {
				TypeSig[] otherTypes = type.GetParametersOfDelegate().Select(param => param.Type).Skip(1).ToArray();
				//return otherTypes.SequenceEqual(originalTypes, temp);
				if (originalTypes.Length != otherTypes.Length) return false;
				for (int i = 0; i < otherTypes.Length; i++) {
					TypeSig left = originalTypes[i];
					TypeSig right = otherTypes[i];
					if (left.FullName != right.FullName) return false;
				}
				return true;
			});
#else
			TypeSig[] originalTypes = originalMethod.Parameters.Select(param => param.Type).ToArray();
			type = types.FirstOrDefault(type => type.GetParametersOfDelegate().Skip(1).Select(param => param.Type).SequenceEqual(originalTypes));
#endif
			if (type != null) {
				evt = hookType.FindEvent(((string)type.Name)[5..]);
			}

			return (type, type?.FindMethod("Invoke"), evt);
		}

		public static IEnumerable<Parameter> GetParametersOfDelegate(this TypeDef @delegate) {
			if (!@delegate.IsDelegate) throw new ArgumentException($"The provided type ({@delegate}) is not a delegate type!");
			MethodDef invokeMtd = @delegate.FindMethod("Invoke");
			foreach (Parameter param in invokeMtd.Parameters) {
				yield return param;
			}
		}
		
	}
}
