using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff;
using HookGenExtender.Core.ILGeneration;
using HookGenExtender.Core.Utils.Ext;
using HookGenExtender.Core.Utils.MemberMutation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
		/// <param name="main"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool HasBIEHookClass(this TypeDef type, ExtensiblesGenerator main) {
			string fullName = type.ReflectionFullName;
			string hookFullName = "On." + fullName;
			return main.BepInExHooksModule.Find(hookFullName, true) != null;
		}

		/// <summary>
		/// Attempts to find a BepInEx <c>On.</c> hook for the provided method.
		/// </summary>
		/// <param name="main">The extensibles generator, for importing types.</param>
		/// <param name="gameType">The extensible type that the hook pertains to.</param>
		/// <param name="original">The method that should be hooked.</param>
		/// <param name="del">Delegate information and references to all members of the delegates.</param>
		/// <returns>True if a hook exists, false if not.</returns>
		public static bool TryGetBIEHook(ExtensiblesGenerator main, ExtensibleTypeData gameType, MethodDefAndRef original, out BepInExHookRef del) {
			// Start by declaring the field storing the original delegate.
			// To do this, acquire the hook type and the corresponding delegate.
			string fullName = original.Definition.DeclaringType.ReflectionFullName;
			string hookFullName = "On." + fullName;
			TypeDef hookClassDef = main.BepInExHooksModule.Find(hookFullName, true);
			if (hookClassDef == null) {
				del = default;
				return false;
			}
			if (!hookClassDef.TryGetOrigDelegateForMethod(main, gameType, original, out del)) {
				del = default;
				return false;
			}
			return true;
		}

		private static string BuildSignatureName(ExtensiblesGenerator main, MethodSig methodSignature) {
			string result = "";
			int count = methodSignature.Params.Count;
			for (int i = 0; i < count; i++) {
				result += "_";
				TypeSig sig = methodSignature.Params[i];
				result += NameConversion.NameOfType(sig);
			}
			return result;
		}

		/// <summary>
		/// Returns the delegate <c>orig_</c> provided by BepInEx hooks for the provided <paramref name="originalMethod"/>.
		/// The returned delegate is imported, however the event is not.
		/// </summary>
		/// <param name="hookType"></param>
		/// <param name="main"></param>
		/// <param name="gameType">The type storing the method that the hook pertains to.</param>
		/// <param name="originalMethod"></param>
		/// <returns></returns>
		public static bool TryGetOrigDelegateForMethod(this TypeDef hookType, ExtensiblesGenerator main, ExtensibleTypeData gameType, MethodDefAndRef originalMethod, out BepInExHookRef data) {
			// BIE hooks follow 3 simple rules:
			// If the method is singular (no overloads), the name is orig_MethodName
			// If the method has overloads, the name is orig_MethodName_Types_Types_Types (where Types are the human readable names i.e. int not Int32)
			// If the method is an interface implementation, it includes the name of the interface as well as the types.

			// Thankfully, the name match can be left to the first rule. Rather than trying to jank my way through the names, I'll match parameters instead.
			string methodName = originalMethod.Name.Replace(".", "_"); // The replacement of . to _ accounts for interface implementations.
			string origName = $"orig_{methodName}";
			string hookName = $"hook_{methodName}";

			string nameSig = BuildSignatureName(main, originalMethod.Definition.MethodSig);
			string methodNameWithSig = methodName + nameSig;
			string origNameWithSig = origName + nameSig;
			string hookNameWithSig = hookName + nameSig;

			TypeDef origDelType = GetFirstApplicableType(hookType.NestedTypes, origNameWithSig, origName);
			TypeDef hookDelType = GetFirstApplicableType(hookType.NestedTypes, hookNameWithSig, hookName);
			if (origDelType == null || hookDelType == null) {
				data = default;
				return false;
			}

			EventDef hookEvt = hookType.FindEvent(methodNameWithSig) ?? hookType.FindEvent(methodName);
			if (hookEvt == null) {
				Debugger.Break();
				data = default;
				return false;
			}

			data = new BepInExHookRef(
				gameType,
				originalMethod,
				ILTools.ReferenceDelegateType(main, origDelType),
				ILTools.ReferenceDelegateType(main, hookDelType),
				originalMethod.Definition.MethodSig.CloneAndImport(main),
				hookEvt,
				new MethodDefAndRef(main, hookEvt.AddMethod, hookType, true),
				new MethodDefAndRef(main, hookEvt.RemoveMethod, hookType, true)
			);
			return true;
		}

		private static TypeDef GetFirstApplicableType(IEnumerable<TypeDef> types, string preciseName, string generalName) {
			return types.FirstOrDefault(t => t.Name == preciseName) ?? types.FirstOrDefault(t => t.Name == generalName);
		}

		private class TypeSignatureComparer : IEqualityComparer<TypeSig> {

			public static IEqualityComparer<TypeSig> Instance { get; } = new TypeSignatureComparer();

			public bool Equals(TypeSig x, TypeSig y) => x.FullName == y.FullName;

			public int GetHashCode(TypeSig obj) => obj.FullName.GetHashCode();
		}

	}
}
