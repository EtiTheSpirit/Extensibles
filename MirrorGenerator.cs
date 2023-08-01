using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender {

	/// <summary>
	/// This class generates mirror types for an entire assembly.
	/// <para/>
	/// A "mirror type" is a type that allows classes inheriting it to access all members of the original class via mirroring its properties, fields, and methods.
	/// The technique in which this is done relies on the behavior of properties.
	/// <para/>
	/// This type is closely inspired by Sponge's "Mixin" for Minecraft modding. Mixins are fundamentally different than traditional hooks in that mixin classes
	/// embed themselves into the class they inject into, allowing its members to be accessed almost as if the mixin class <em>is</em> the original class. This
	/// behavior is extremely powerful and allows hooks to more naturally operate in tandem with the original class, rather than as external components that are
	/// injected into the original.
	/// </summary>
	public sealed class MirrorGenerator {

		public ModuleDefMD Module { get; }

		public string NewModuleName { get; }

		private ModuleDefUser? _patch;

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="original"></param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		public MirrorGenerator(ModuleDefMD original, string? newModuleName = null) {
			Module = original;
			NewModuleName = newModuleName ?? (original.Name + "-MIXIN");

			// TODO:
			// 1: Create the new patch type (done)
			//	- The patch type needs to be abstract.
			//	- The patch type needs a constructor accepting the original type that it will mirror.
			// 2: Create a private readonly field that is an instance of WeakReference, storing the original object that the mirror represents.
			//	- The patch type's constructor sets this.
			// 3: Create a public property that references and returns the weakly stored reference. This way it can be referenced as if it were a strong reference
			//		(through the property) while still being weak behind the scenes.
			// 4: Iterate all properties, fields
			//	- Props/fields need to be mirrored. Properties should respect the presence of getters/setters.
			//	- fields can access both get/set
			//	- This mimics the behavior of @Shadow in mixin
			// 5: Iterate methods
			//	- These should be comparable to @Shadow in mixin, again.
			// TODO: Should the names be the same as their counterpart? Should they use some prefix?

		}

		public void Generate() {
			_patch = new ModuleDefUser(NewModuleName);

			foreach (TypeDef def in Module.GetTypes()) {
				if (def.IsGlobalModuleType) continue;
				if (def.IsDelegate) continue;
				if (def.IsEnum) continue;
				if (def.IsForwarder) continue;
				if (def.IsInterface) continue;
				if (def.IsPrimitive) continue;

			}
		}

		

	}
}
