#define USE_INHERITED_MIRRORS

using dnlib.DotNet;
using dnlib.DotNet.Resources;
using HookGenExtender.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
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

		public static TypeDef WeakReferenceType {
			get {
				if (_weakReferenceTypeCache == null) {
					// Reference: https://github.com/0xd4d/dnlib/blob/master/Examples/Example1.cs
					// The original code uses typeof(void) to get ahold of mscorlib.dll
					ModuleDefMD mod = ModuleDefMD.Load(typeof(WeakReference<>).Module.FullyQualifiedName);
					_weakReferenceTypeCache = mod.Find(typeof(WeakReference<>).FullName, true);
				}
			
				return _weakReferenceTypeCache;
			}
		}

		public static ClassOrValueTypeSig WeakReferenceTypeSig {
			get {
				if (_weakRefTypeSig == null) {
					_weakRefTypeSig = (ClassOrValueTypeSig)WeakReferenceType.ToTypeSig();
				}
				return _weakRefTypeSig;
			}
		}

		private static TypeDef? _weakReferenceTypeCache = null;
		private static ClassOrValueTypeSig? _weakRefTypeSig = null;


#if USE_INHERITED_MIRRORS
		private static readonly Dictionary<ITypeDefOrRef, TypeDefUser> _mirrors = new Dictionary<ITypeDefOrRef, TypeDefUser>();
#endif

		/// <summary>
		/// The original module that is being mirrored.
		/// </summary>
		public ModuleDefMD Module { get; }

		/// <summary>
		/// The replacement module containing the mirror classes.
		/// </summary>
		public ModuleDefUser MirrorModule { get; }

		/// <summary>
		/// The name of the new module.
		/// </summary>
		public string NewModuleName { get; }

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="original">The module that these hooks will be generated for.</param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		public MirrorGenerator(ModuleDefMD original, string? newModuleName = null) {
			Module = original;
			NewModuleName = newModuleName ?? (original.Name + "-MIXIN");
			MirrorModule = new ModuleDefUser(NewModuleName);

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
			foreach (TypeDef def in Module.GetTypes()) {
				if (def.IsGlobalModuleType) continue;
				if (def.IsDelegate) continue;
				if (def.IsEnum) continue;
				if (def.IsForwarder) continue;
				if (def.IsInterface) continue;
				if (def.IsPrimitive) continue;

				
			}
		}

		/// <summary>
		/// Generates the entire type for a mirror, and registers it to the mirror module.
		/// </summary>
		/// <param name="from"></param>
		private TypeDefUser GenerateReplacementType(TypeDef from) {
			// TODO: Generate or use the base type of the original? No base type? Object?
			// For now I will use object.

			// TO FUTURE SELF: It is a perfectly valid option to use these mirror classes as bases for eachother
			// The only hurdle is the Original property would need to be a shadow.

			TypeDef inheritFrom = Module.CorLibTypes.Object.TypeDef;
#if USE_INHERITED_MIRRORS
			if (from.BaseType != null && from.BaseType != inheritFrom) {
				// Inherits from another type.
				if (_mirrors.TryGetValue(from.BaseType, out TypeDefUser? baseMirror)) {
					inheritFrom = baseMirror;
				} else {
					TypeDefUser repl = GenerateReplacementType((TypeDef)from.BaseType);
					inheritFrom = repl;
					_mirrors[from.BaseType] = repl;
				}
			}
#endif

			TypeDefUser replacement = new TypeDefUser($"Mixin.{from.Namespace}", from.Name, Module.CorLibTypes.Object.TypeDef);
			replacement.Attributes = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class;

			// Add WeakReference<T> _original
			// Add T Original { get { _original.TryGetTarget(t); return t; } }
			BindOriginalReference(from, replacement);
			BindPropertyMirrors(from, replacement);
		}

		/// <summary>
		/// Creates a private readonly field of type <see cref="WeakReference{T}"/>. This field weakly stores the original object that is being mirrored.
		/// This also creates a public property to access the weak reference easily.
		/// </summary>
		/// <param name="originalType"></param>
		/// <param name="to"></param>
		private void BindOriginalReference(TypeDef originalType, TypeDefUser to) {
			// System.Runtime.dll
			TypeSig originalTypeSig = originalType.ToTypeSig();
			GenericInstSig weakRefInstance = new GenericInstSig(WeakReferenceTypeSig, originalTypeSig);

			FieldDefUser weakRef = new FieldDefUser("_original", new FieldSig(weakRefInstance), FieldAttributes.Private | FieldAttributes.InitOnly);
			PropertyDefUser strongRef = new PropertyDefUser("Original", new PropertySig(true, originalTypeSig));
			ILGenerators.CreateOriginalReferencer(strongRef, weakRef, weakRefInstance.TryGetTypeDef());

			to.Fields.Add(weakRef);
			to.Properties.Add(strongRef);
		}

		/// <summary>
		/// Creates the get/set mirrors for all properties of the provided type, except for static properties.
		/// </summary>
		/// <param name="props"></param>
		/// <param name="inUserType"></param>
		private static void BindPropertyMirrors(TypeDef source, TypeDefUser inUserType) {
			foreach (PropertyDef property in source.Properties) {
				if (property.IsStatic()) continue;
				if (property.DeclaringType != source) continue;

				// Duplicate the property
				PropertyDefUser mirror = new PropertyDefUser(property.Name, property.PropertySig, property.Attributes);
				
			}
		}

		

	}
}
