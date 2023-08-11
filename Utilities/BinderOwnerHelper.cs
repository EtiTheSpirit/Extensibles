using dnlib.DotNet;
using HookGenExtender.Utilities.Representations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {

	/// <summary>
	/// This class is a utility for <see cref="MirrorGenerator"/> that keeps track of the top-most classes that declare
	/// specific members, so that binders do not generate overlapping bindings.
	/// </summary>
	public sealed class BinderOwnerHelper {
		
		public MirrorGenerator Generator { get; }

		private bool _finalized = false;

		private readonly Dictionary<TypeDefUser, HashSet<SignatureBoundMemberDef>> _knownDeclarations = new Dictionary<TypeDefUser, HashSet<SignatureBoundMemberDef>>();
		private readonly Dictionary<TypeDefUser, List<TypeDefUser>> _derivedLookup = new Dictionary<TypeDefUser, List<TypeDefUser>>();

		public BinderOwnerHelper(MirrorGenerator generator) {
			Generator = generator;
		}

		/// <summary>
		/// Stores all members that can be extended from the given type. 
		/// This assumes the type has been validated beforehand.
		/// </summary>
		/// <param name="originalType"></param>
		/// <param name="equivalentUserType"></param>
		public void InitializeMemberManifestOf(TypeDef originalType, TypeDefUser equivalentUserType) {
			if (_finalized) throw new InvalidOperationException("The declarations have already been finalized. It is not possible to add new member manifests.");
			IEnumerable<MethodDef> hookableMethods = originalType.Methods.Where(method => BepInExExtensibles.TryGetBIEHook(Generator, method, out _)); // This is so laggy.
			IEnumerable<PropertyDef> hookableProperties = originalType.Properties;

			HashSet<SignatureBoundMemberDef> set = new HashSet<SignatureBoundMemberDef>();
			set.UnionWith(hookableMethods.Select(SignatureBoundMemberDef.ctor));
			set.UnionWith(hookableProperties.Select(SignatureBoundMemberDef.ctor));
			_knownDeclarations[equivalentUserType] = set;
		}

		/// <summary>
		/// To be used after all manifests have been initialized 
		/// (see <see cref="InitializeMemberManifestOf(TypeDef, TypeDefUser)"/>),
		/// this computes all exclusive sets of members. 
		/// </summary>
		public void FinalizeHashSets() {
			if (_finalized) throw new InvalidOperationException("The declarations have already been finalized.");
			
			// TO FUTURE XAN:
			// The concept here is to build a list of every member by its mirror type (which is what _knownDeclarations is)
			// and then this method here removes less derived members from the sets belonging to more derived members.
			// This is very hard to think about and model in your mind, so this note will help to explain it.

			// It might be useful to start by building an inheritence tree.
			// Now, this has to start from the top (least-derived) and work its way down.
			// This can be done by keeping a list of all top-level types.

			List<TypeDefUser> topLevelTypes = new List<TypeDefUser>();
			foreach (KeyValuePair<TypeDefUser, HashSet<SignatureBoundMemberDef>> kvp in _knownDeclarations) {
				// If the base type of the current entry (which is completely arbitrary) is a user-defined type...
				if (kvp.Key.BaseType is TypeDefUser @base) {
					// Create a List<TypeDefUser> to store the derived types of that base type, if needed.
					// Store this current type in the list belonging to the base type.
					if (!_derivedLookup.TryGetValue(@base, out List<TypeDefUser> derived)) {
						derived = new List<TypeDefUser> {
							kvp.Key
						};
						_derivedLookup[@base] = derived;
					} else {
						derived.Add(kvp.Key);
					}
				} else {
					// The base type isn't user-defined (I didn't make it). This means its a top level type.
					topLevelTypes.Add(kvp.Key);
				}
			}

			// At this point, _derivedLookup contains a lookup where keys are types,
			// and the value is a list of every derived type.
			// Now that cascading action from top down can begin:
			foreach (TypeDefUser type in topLevelTypes) {
				// Create a set to cumulatively store all members that are invalid for each derived type
				// on the way down...
				HashSet<SignatureBoundMemberDef> currentChain = new HashSet<SignatureBoundMemberDef>();

				// And now cascade:
				Cascade(type, currentChain);
			}

			_finalized = true;
		}

		private void Cascade(TypeDefUser currentBaseType, HashSet<SignatureBoundMemberDef> currentChain) {
			// Get the list of derived types for this base type.
			if (_derivedLookup.TryGetValue(currentBaseType, out List<TypeDefUser> derivedList)) {
				foreach (TypeDefUser derived in derivedList) {
					// For each of them, get the list of members that the type believes it declares.
					HashSet<SignatureBoundMemberDef> declarations = _knownDeclarations[derived];

					// Also get the types that the base believes it declares.
					HashSet<SignatureBoundMemberDef> baseDeclarations = _knownDeclarations[currentBaseType];

					// Now, the elements found in baseDeclarations need to be removed from
					// declarations (because it is now known that the derived type did not,
					// in fact, declare that member).

					// Removing these elements works if there is only one depth of derived types,
					// but this does not work if there is more than one depth (the action of erasing
					// is destructive, and thus the entries that need to be removed are not found
					// when trying to clear them from the next type down, and so on).

					// This can be fixed with the currentChain set, which will contain all removed types:
					currentChain.UnionWith(baseDeclarations);

					// Now trim all entries from currentChain away from declarations:
					declarations.ExceptWith(currentChain);

					// Add the current stuff too, this is useful for the most derived type:
					currentChain.UnionWith(declarations);

					// Now repeat this action for the current derived type.
					Cascade(derived, currentChain);
				}
			}
		}

		/// <summary>
		/// Returns the original copy of every member that an extensible type's binder should hook.
		/// Note that the returned member definitions are <em>NOT</em> the imported variant!
		/// </summary>
		/// <param name="userType"></param>
		/// <returns></returns>
		public IEnumerable<IMemberDef> GetExtendableMembersOf(TypeDefUser userType) {
			if (!_finalized) throw new InvalidOperationException($"Cannot get the list of members for this type; call {nameof(FinalizeHashSets)} first.");

			return _knownDeclarations[userType].Select(mbr => mbr.Original);
		}

	}
}
