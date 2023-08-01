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

		public ITypeDefOrRef WeakReferenceType {
			get {
				if (_weakReferenceTypeCache == null) {
					// Reference: https://github.com/0xd4d/dnlib/blob/master/Examples/Example1.cs
					// The original code uses typeof(void) to get ahold of mscorlib.dll
					_weakReferenceTypeCache = cache.Import(typeof(WeakReference<>));
				}

				return _weakReferenceTypeCache;
			}
		}

		public IMethod WeakReferenceTryGetTarget {
			get {
				if (_weakReferenceTryGetTarget == null) {
					_weakReferenceTryGetTarget = cache.Import(typeof(WeakReference<>).GetMethod("TryGetTarget"));
				}
				return _weakReferenceTryGetTarget;
			}
		}

		public ClassOrValueTypeSig WeakReferenceTypeSig {
			get {
				if (_weakRefTypeSig == null) {
					_weakRefTypeSig = WeakReferenceType.ToTypeSig().ToClassOrValueTypeSig();
				}
				return _weakRefTypeSig;
			}
		}

		private ITypeDefOrRef? _weakReferenceTypeCache = null;
		private IMethod? _weakReferenceTryGetTarget = null;
		private ClassOrValueTypeSig? _weakRefTypeSig = null;

		/// <summary>
		/// This dictionary is only used if <see cref="GeneratorSettings.mirrorTypesInherit"/> is <see langword="true"/>.
		/// </summary>
		private static readonly Dictionary<ITypeDefOrRef, TypeDefUser> _mirrors = new Dictionary<ITypeDefOrRef, TypeDefUser>();

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
		/// The settings for this generator
		/// </summary>
		public GeneratorSettings Settings { get; }

		internal readonly ImportCache cache;

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="original">The module that these hooks will be generated for.</param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		/// <param name="settings">Any settings to change how the generator operates.</param>
		public MirrorGenerator(ModuleDefMD original, string? newModuleName = null, GeneratorSettings? settings = null) {
			Module = original;
			NewModuleName = newModuleName ?? (original.Name + "-MIXIN");
			MirrorModule = new ModuleDefUser(NewModuleName);
			Settings = settings ?? new GeneratorSettings();
			cache = new ImportCache(MirrorModule);

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
				if (def.IsSpecialName) continue;
				if (def.IsRuntimeSpecialName) continue;
				if (def.Name.StartsWith("<>")) continue;
				if (def.Namespace.StartsWith("Microsoft.CodeAnalysis")) continue;
				if (def.Namespace.StartsWith("System.")) continue;

				TypeDefUser repl = GenerateReplacementType(def, cache.Import(def));
				MirrorModule.Types.Add(repl);
			}
		}

		public void Save(FileInfo to) {
			if (to.Exists) to.Delete();
			using FileStream stream = to.Open(FileMode.CreateNew);
			MirrorModule.Write(stream);
		}

		/// <summary>
		/// Generates the entire type for a mirror, and registers it to the mirror module.
		/// </summary>
		/// <param name="from"></param>
		private TypeDefUser GenerateReplacementType(TypeDef original, ITypeDefOrRef from) {
			// TODO: Generate or use the base type of the original? No base type? Object?
			// For now, the base type is the mirror.

			ITypeDefOrRef baseType = from.GetBaseType();
			ITypeDefOrRef inheritFrom = Module.CorLibTypes.Object.TypeDefOrRef;
			if (Settings.mirrorTypesInherit) {
				if (baseType != null && baseType.AssemblyQualifiedName != inheritFrom.AssemblyQualifiedName) {
					// Inherits from another type.
					if (_mirrors.TryGetValue(baseType, out TypeDefUser? baseMirror)) {
						inheritFrom = baseMirror;
					} else {
						TypeDefUser repl = GenerateReplacementType(original, baseType);
						inheritFrom = repl;
						_mirrors[baseType] = repl;
					}
				}
			}

			string ns = from.Namespace;
			if (ns != null && ns.Length > 0) {
				ns = '.' + ns;
			}
			TypeDefUser replacement = new TypeDefUser($"Mixin{ns}", from.Name, inheritFrom);
			replacement.Attributes = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class;

			PropertyDefUser orgRef = BindOriginalReference(from, replacement);
			BindPropertyMirrors(original, replacement, orgRef);
			BindFieldMirrors(original, replacement, orgRef);
			BindMethodMirrors(original, replacement, orgRef);

			return replacement;
		}

		/// <summary>
		/// Creates a private readonly field of type <see cref="WeakReference{T}"/>. This field weakly stores the original object that is being mirrored.
		/// This also creates a public property to access the weak reference easily.
		/// </summary>
		/// <param name="originalType"></param>
		/// <param name="to"></param>
		private PropertyDefUser BindOriginalReference(ITypeDefOrRef originalType, TypeDefUser to) {
			TypeSig originalTypeSig = originalType.ToTypeSig();
			
			GenericInstSig weakRefInstance = new GenericInstSig(WeakReferenceTypeSig, originalTypeSig);
			GenericInstMethodSig weakRefTryGetTarget = new GenericInstMethodSig(originalTypeSig);

			FieldDefUser weakRef = new FieldDefUser("_original", new FieldSig(weakRefInstance), FieldAttributes.Private | FieldAttributes.InitOnly);
			PropertyDefUser strongRef = new PropertyDefUser("Original", new PropertySig(true, originalTypeSig));
			MemberRefUser mbrRef = ILGenerators.CreateOriginalReferencer(this, strongRef, weakRef, weakRefInstance, weakRefTryGetTarget);

			to.Fields.Add(weakRef);
			to.Properties.Add(strongRef);
			to.Methods.Add(strongRef.GetMethod);
			mbrRef.Class = to;
			return strongRef;
		}

		/// <summary>
		/// Creates the get/set mirrors for all properties of the provided type, except for static properties.
		/// </summary>
		/// <param name="props"></param>
		/// <param name="inUserType"></param>
		private void BindPropertyMirrors(TypeDef source, TypeDefUser inUserType, PropertyDefUser orgRef) {
			foreach (PropertyDef orgProp in source.Properties) {
				if (orgProp.IsStatic()) continue;
				if (orgProp.DeclaringType != source) continue;

				// Duplicate the property

				PropertyDefUser mirror = new PropertyDefUser(orgProp.Name, PropertySig.CreateInstance(cache.Import(orgProp.PropertySig.RetType)), orgProp.Attributes);
				bool hasGetter = orgProp.GetMethod != null && !orgProp.GetMethod.IsAbstract;
				bool hasSetter = orgProp.SetMethod != null && !orgProp.SetMethod.IsAbstract;
				
				if (hasGetter) {
					ILGenerators.CreateGetterToProperty(this, mirror, orgRef, Settings);
					inUserType.Methods.Add(mirror.GetMethod);
				} else {
					mirror.GetMethod = null;
				}
				if (hasSetter) {
					ILGenerators.CreateSetterToProperty(this, mirror, orgRef, Settings);
					inUserType.Methods.Add(mirror.SetMethod);
				} else {
					mirror.SetMethod = null;
				}

				inUserType.Properties.Add(mirror);
			}
		}

		private void BindFieldMirrors(TypeDef source, TypeDefUser inUserType, PropertyDefUser orgRef) {
			foreach (FieldDef field in source.Fields) {
				if (field.IsStatic) continue;
				if (field.DeclaringType != source) continue;

				MemberRef orgField = cache.Import(field);
				PropertyDefUser mirror = new PropertyDefUser(field.Name, new PropertySig(true, cache.Import(field.FieldType)));

				mirror.GetMethod = ILGenerators.CreateGetterToField(this, mirror, orgField, orgRef, Settings);
				mirror.SetMethod = ILGenerators.CreateSetterToField(this, mirror, orgField, orgRef, Settings);

				inUserType.Properties.Add(mirror);
				inUserType.Methods.Add(mirror.GetMethod);
				inUserType.Methods.Add(mirror.SetMethod);
			}
		}

		private void BindMethodMirrors(TypeDef source, TypeDefUser inUserType, PropertyDefUser orgRef) {
			foreach (MethodDef mtd in source.Methods) {
				if (mtd.IsStatic) continue;
				if (mtd.DeclaringType != source) continue;
				if (mtd.IsConstructor || mtd.IsStaticConstructor || mtd.Name == "Finalize") continue;

				MethodDefUser mirror = ILGenerators.GenerateMethodMirror(this, mtd, orgRef, Settings);
				inUserType.Methods.Add(mirror);
			}
		}

	}
}
