using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Resources;
using HookGenExtender.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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

		public const string NAMESPACE = "Extensible";

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

		public ITypeDefOrRef CWTType {
			get {
				if (_cwtTypeCache == null) {
					_cwtTypeCache = cache.Import(typeof(ConditionalWeakTable<,>));
				}
				return _cwtTypeCache;
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

		public ClassOrValueTypeSig CWTTypeSig {
			get {
				if (_cwtTypeSig == null) {
					_cwtTypeSig = CWTType.ToTypeSig().ToClassOrValueTypeSig();
				}
				return _cwtTypeSig;
			}
		}

		private ITypeDefOrRef? _weakReferenceTypeCache = null;
		private ITypeDefOrRef? _cwtTypeCache = null;
		private ClassOrValueTypeSig? _weakRefTypeSig = null;
		private ClassOrValueTypeSig? _cwtTypeSig = null;

		/// <summary>
		/// This dictionary is only used if <see cref="GeneratorSettings.MirrorTypesInherit"/> is <see langword="true"/>.
		/// </summary>
		private static readonly Dictionary<ITypeDefOrRef, TypeDefUser> _mirrors = new Dictionary<ITypeDefOrRef, TypeDefUser>();

		/// <summary>
		/// The original module that is being mirrored.
		/// </summary>
		public ModuleDefMD Module { get; }

		/// <summary>
		/// The -HOOKS DLL provided by BepInEx. Optional, but this can allow generating automatic hook code.
		/// </summary>
		public ModuleDefMD? BepInExHooksModule { get; }

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
			NewModuleName = newModuleName ?? (original.Name + "-" + NAMESPACE.ToUpper());
			MirrorModule = new ModuleDefUser(NewModuleName);
			Settings = settings ?? new GeneratorSettings();
			cache = new ImportCache(MirrorModule);
			MirrorModule.EnableTypeDefFindCache = true;
		}

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="original">The module that these hooks will be generated for.</param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		/// <param name="settings">Any settings to change how the generator operates.</param>
		public MirrorGenerator(ModuleDefMD original, ModuleDefMD bieHooks, string? newModuleName = null, GeneratorSettings? settings = null) {
			Module = original;
			BepInExHooksModule = bieHooks;
			NewModuleName = newModuleName ?? (original.Name + "-" + NAMESPACE.ToUpper());
			MirrorModule = new ModuleDefUser(NewModuleName);
			Settings = settings ?? new GeneratorSettings();
			cache = new ImportCache(MirrorModule);
			MirrorModule.EnableTypeDefFindCache = true;
			BepInExHooksModule.EnableTypeDefFindCache = true;
		}

		public void Generate() {
			Dictionary<TypeDef, TypeDefUser> orgToRepl = new Dictionary<TypeDef, TypeDefUser>();

			TypeDef[] allTypes = Module.GetTypes().ToArray();
			int current = 0;
			foreach (TypeDef def in allTypes) {
				if (def.IsGlobalModuleType) continue;
				if (def.IsDelegate) continue;
				if (def.IsEnum) continue;
				if (def.IsForwarder) continue;
				if (def.IsInterface) continue;
				if (def.IsPrimitive) continue;
				if (def.IsSpecialName) continue;
				if (def.IsRuntimeSpecialName) continue;
				if (def.Name.StartsWith("<")) continue; // TODO: Better version of this.
				if (def.Namespace.StartsWith("Microsoft.CodeAnalysis")) continue;
				if (def.Namespace.StartsWith("System.")) continue;

				TypeDefUser repl = GenerateReplacementType(def, cache.Import(def));
				orgToRepl[def] = repl;
				current++;

				if (current % 100 == 0) {
					Console.WriteLine($"Processed {current} of {allTypes.Length}...");
					Console.CursorTop--;
				}
			}
			Console.WriteLine($"Processed {current} of {allTypes.Length}...");

			foreach (KeyValuePair<TypeDef, TypeDefUser> def in orgToRepl) {
				foreach (TypeDef nested in def.Key.NestedTypes) {
					if (orgToRepl.TryGetValue(nested, out TypeDefUser? nest)) {
						nest.DeclaringType2 = def.Value;
					}
				}
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
			if (Settings.MirrorTypesInherit) {
				if (baseType != null && baseType.AssemblyQualifiedName != inheritFrom.AssemblyQualifiedName) {
					// Inherits from another type.
					if (_mirrors.TryGetValue(baseType, out TypeDefUser? baseMirror)) {
						inheritFrom = baseMirror;
					} else {
						TypeDefUser repl = GenerateReplacementType(original, baseType);
						_mirrors[baseType] = repl;
					}
				}
			}

			string ns = from.Namespace;
			if (ns != null && ns.Length > 0) {
				ns = '.' + ns;
			}
			TypeDefUser replacement = new TypeDefUser($"{NAMESPACE}{ns}", from.Name, inheritFrom);
			replacement.Attributes = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class;
			MirrorModule.Types.Add(replacement);

			PropertyDefUser orgRef = BindOriginalReference(from, replacement);
			// AppendCWT(from, replacement);
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

			FieldDefUser weakRef = new FieldDefUser("<original>ExtensibleWeakReference", new FieldSig(weakRefInstance), FieldAttributes.Private | FieldAttributes.InitOnly);
			PropertyDefUser strongRef = new PropertyDefUser("Original", new PropertySig(true, originalTypeSig));
			MemberRefUser mbrRef = ILGenerators.CreateOriginalReferencer(this, strongRef, weakRef, weakRefInstance, weakRefTryGetTarget);

			to.Fields.Add(weakRef);
			to.Properties.Add(strongRef);
			to.Methods.Add(strongRef.GetMethod);
			mbrRef.Class = to;
			return strongRef;
		}

		/// <summary>
		/// Generates a static <see cref="ConditionalWeakTable{TKey, TValue}"/> storing bindings from the original class to the extensible classes.
		/// </summary>
		/// <param name="originalType"></param>
		/// <param name="to"></param>
		[Obsolete("This technique isn't very good. Implementors should instead have their own CWTs.")]
		private void AppendCWT(ITypeDefOrRef originalType, TypeDefUser to) {
			TypeSig originalTypeSig = originalType.ToTypeSig();

			TypeSig dest = to.ToTypeSig();
			GenericInstSig cwt = new GenericInstSig(CWTTypeSig, originalTypeSig, dest);
			FieldDefUser bindingsFld = ILGenerators.CreateStaticCWTInitializer(this, cwt, to, originalTypeSig, dest);
			to.Fields.Add(bindingsFld);

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

				PropertyDefUser mirror = new PropertyDefUser(orgProp.Name, PropertySig.CreateInstance(cache.Import(orgProp.PropertySig.RetType)));
				bool hasGetter = orgProp.GetMethod != null && !orgProp.GetMethod.IsAbstract;
				bool hasSetter = orgProp.SetMethod != null && !orgProp.SetMethod.IsAbstract;

				inUserType.Properties.Add(mirror);
				if (hasGetter) {
					ILGenerators.CreateGetterToProperty(this, mirror, orgProp, orgRef, Settings);
					inUserType.Methods.Add(mirror.GetMethod);
				} else {
					mirror.GetMethod = null;
				}
				if (hasSetter) {
					ILGenerators.CreateSetterToProperty(this, mirror, orgProp, orgRef, Settings);
					inUserType.Methods.Add(mirror.SetMethod);
				} else {
					mirror.SetMethod = null;
				}
			}
		}

		private void BindFieldMirrors(TypeDef source, TypeDefUser inUserType, PropertyDefUser orgRef) {
			foreach (FieldDef field in source.Fields) {
				if (field.IsStatic) continue;
				if (field.DeclaringType != source) continue;
				if (field.IsSpecialName) continue;
				if (((string)field.Name)[0] == '<') continue;

				MemberRef orgField = cache.Import(field);
				PropertyDefUser mirror = new PropertyDefUser(field.Name, PropertySig.CreateInstance(cache.Import(field.FieldType)));

				ILGenerators.CreateGetterToField(this, mirror, orgField, orgRef, Settings);
				ILGenerators.CreateSetterToField(this, mirror, orgField, orgRef, Settings);

				inUserType.Properties.Add(mirror);
				inUserType.Methods.Add(mirror.GetMethod);
				inUserType.Methods.Add(mirror.SetMethod);
			}
		}

		private void BindMethodMirrors(TypeDef source, TypeDefUser inUserType, PropertyDefUser orgRef) {
			if (BepInExHooksModule != null) {
				// BIE behavior: Use the original method delegate.
				foreach (MethodDef mtd in source.Methods) {
					if (mtd.IsStatic) continue;
					if (mtd.DeclaringType != source) continue;
					if (mtd.IsConstructor || mtd.IsStaticConstructor || mtd.Name == "Finalize") continue;
					if (mtd.IsSpecialName) continue; // properties do this.
					if (((string)mtd.Name)[0] == '<') continue;
					if (mtd.HasGenericParameters) continue;

					(MethodDefUser? mirror, FieldDefUser? origDelegateRef, FieldDefUser? origMtdInUse) = ILGenerators.TryGenerateBIEOrigCall(this, mtd, orgRef, Settings);
					if (mirror != null && origDelegateRef != null && origMtdInUse != null) {
						inUserType.Methods.Add(mirror);
						inUserType.Fields.Add(origDelegateRef);
						inUserType.Fields.Add(origMtdInUse);
					}
				}
			} else {
				// Default behavior: Just redirect to original method.
				foreach (MethodDef mtd in source.Methods) {
					if (mtd.IsStatic) continue;
					if (mtd.DeclaringType != source) continue;
					if (mtd.IsConstructor || mtd.IsStaticConstructor || mtd.Name == "Finalize") continue;
					if (mtd.IsSpecialName) continue; // properties do this.
					if (mtd.Name.Contains("<")) continue;
					if (mtd.HasGenericParameters) continue;

					MethodDefUser mirror = ILGenerators.GenerateMethodMirror(this, mtd, orgRef, Settings);
					inUserType.Methods.Add(mirror);
				}
			}
		}

	}
}
