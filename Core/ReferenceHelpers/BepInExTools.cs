using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ReferenceHelpers {

	/// <summary>
	/// Tools to assist in finding references to BepInEx hook stuffs.
	/// </summary>
	public static class BepInExTools {

		/// <summary>
		/// Returns whether or not the provided <paramref name="type"/> has an equivalent <c>On.</c> counterpart in BIE's Hooks module.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool HasBIEHookClass(this TypeDef type, ExtensiblesGenerator mirrorGenerator) {
			string fullName = type.ReflectionFullName;
			string hookFullName = "On." + fullName;
			return mirrorGenerator.BepInExHooksModule.Find(hookFullName, true) != null;
		}

		/// <summary>
		/// Attempts to find a BepInEx <c>On.</c> hook for the provided method.
		/// </summary>
		/// <param name="mirrorGenerator">The mirror generator, for importing types.</param>
		/// <param name="original">The method that should be hooked.</param>
		/// <param name="hook">Information about the BIE hook.</param>
		/// <returns>True if a hook exists, false if not.</returns>
		public static bool TryGetBIEHook(ExtensiblesGenerator main, MethodDef original, out BIEHookRef hook) {
			// Start by declaring the field storing the original delegate.
			// To do this, acquire the hook type and the corresponding delegate.
			string fullName = original.DeclaringType.ReflectionFullName;
			string hookFullName = "On." + fullName;
			TypeDef hookClassDef = main.BepInExHooksModule.Find(hookFullName, true);
			if (hookClassDef == null) {
				hook = default;
				return false;
			}
			if (!hookClassDef.TryGetOrigDelegateForMethod(main, original, out TypeDef bieOrigMethodDef, out MethodDef bieOrigInvoke, out EventDef hookEvt)) {
				hook = default;
				return false;
			}
			TypeRef hookClassRef = main.Cache.Import(hookClassDef);
			TypeRef bieOrigMethod = main.Cache.Import(bieOrigMethodDef);
			TypeSig bieOrigMethodSig = main.Cache.Import(bieOrigMethodDef.ToTypeSig());
			IMethodDefOrRef bieOrigInvokeRef = main.Cache.Import(bieOrigInvoke);

			TypeSig[] invokeParameters = bieOrigInvoke!.Parameters.Select(paramDef => main.Cache.Import(paramDef.Type)).ToArray();
			TypeSig[] originalMethodParameters = original.Parameters
				.Skip(1) // skip 'this'
				.Where(paramDef => !paramDef.IsReturnTypeParameter)
				.Select(paramDef => main.Cache.Import(paramDef.Type))
				.ToArray();

			hook = new BIEHookRef(hookClassRef, bieOrigMethod, bieOrigMethodSig, bieOrigInvokeRef, hookEvt, invokeParameters, originalMethodParameters);
			return true;
		}

		/// <summary>
		/// Returns the delegate <c>orig_</c> provided by BepInEx hooks for the provided <paramref name="originalMethod"/>.
		/// </summary>
		/// <param name="hookType"></param>
		/// <param name="originalMethod"></param>
		/// <returns></returns>
		public static bool TryGetOrigDelegateForMethod(this TypeDef hookType, ExtensiblesGenerator mirrorGenerator, MethodDef originalMethod, out TypeDef bieOrigMethodDef, out MethodDef bieOrigInvoke, out EventDef hookEvt) {
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

			TypeDef type;
			EventDef evt = null;
			if (types.Count() == 1) {
				type = types.First();
				evt = hookType.FindEvent(type.Name.Substring(5));
				bieOrigMethodDef = type;
				bieOrigInvoke = type.FindMethod("Invoke");
				hookEvt = evt;
				return bieOrigInvoke != null && hookEvt != null;
			}

			// Match by parameters
			// This is yucky lol
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

			if (type != null) {
				evt = hookType.FindEvent(type.Name.Substring(5));
			}

			bieOrigMethodDef = type;
			bieOrigInvoke = type?.FindMethod("Invoke");
			hookEvt = evt;
			return type != null && bieOrigInvoke != null && hookEvt != null;
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
