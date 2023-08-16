using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// This represents a complete extensible type and its members. It is a proxy to a <see cref="TypeDef"/> and two <see cref="CachedTypeDef"/>s.
	/// </summary>
	public sealed class ExtensibleTypeData {

		/// <summary>
		/// A reference to the original game type.
		/// </summary>
		internal readonly TypeDef _originalGameType;

		/// <summary>
		/// The game type that this extensible type represents.
		/// </summary>
		public TypeRef ImportedGameType { get; }

		/// <summary>
		/// The signature of the imported game type.
		/// </summary>
		public TypeSig ImportedGameTypeSig { get; }

		/// <summary>
		/// The actual extensible type itself.
		/// </summary>
		public CachedTypeDef ExtensibleType { get; }

		/// <summary>
		/// The binder for the <see cref="ExtensibleType"/>.
		/// </summary>
		public CachedTypeDef Binder { get; }

		public ExtensibleTypeData(TypeDef original, TypeRef originalImported, CachedTypeDef replacement, CachedTypeDef binder) {
			_originalGameType = original;
			ImportedGameType = originalImported;
			ImportedGameTypeSig = originalImported.ToTypeSig();
			ExtensibleType = replacement;
			Binder = binder;
		}
	}
}
