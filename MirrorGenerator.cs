using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Pdb;
using dnlib.DotNet.Pdb.Symbols;
using dnlib.DotNet.Resources;
using dnlib.DotNet.Writer;
using HookGenExtender.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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

		public const FieldAttributes PRIVATE_FIELD_TYPE = FieldAttributes.PrivateScope;
		public const MethodAttributes PRIVATE_METHOD_TYPE = (MethodAttributes)PRIVATE_FIELD_TYPE;

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

		public ITypeDefOrRef DictionaryType {
			get {
				if (_dictionaryTypeCache == null) {
					_dictionaryTypeCache = cache.Import(typeof(Dictionary<,>));
				}
				return _dictionaryTypeCache;
			}
		}

		public ITypeDefOrRef EnumerableType {
			get {
				if (_enumerableTypeCache == null) {
					_enumerableTypeCache = cache.Import(typeof(Enumerable));
				}
				return _enumerableTypeCache;
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

		public ClassOrValueTypeSig DictionaryTypeSig {
			get {
				if (_dictionaryTypeSig == null) {
					_dictionaryTypeSig = DictionaryType.ToTypeSig().ToClassOrValueTypeSig();
				}
				return _dictionaryTypeSig;
			}
		}

		public ClassOrValueTypeSig EnumerableTypeSig {
			get {
				if (_enumerableTypeSig == null) {
					_enumerableTypeSig = EnumerableType.ToTypeSig().ToClassOrValueTypeSig();
				}
				return _enumerableTypeSig;
			}
		}

		private ITypeDefOrRef _weakReferenceTypeCache = null;
		private ITypeDefOrRef _cwtTypeCache = null;
		private ITypeDefOrRef _dictionaryTypeCache = null;
		private ITypeDefOrRef _enumerableTypeCache = null;
		private ClassOrValueTypeSig _weakRefTypeSig = null;
		private ClassOrValueTypeSig _cwtTypeSig = null;
		private ClassOrValueTypeSig _dictionaryTypeSig = null;
		private ClassOrValueTypeSig _enumerableTypeSig = null;

		/// <summary>
		/// The original module that is being mirrored.
		/// </summary>
		public ModuleDefMD Module { get; }

		/// <summary>
		/// The -HOOKS DLL provided by BepInEx. Optional, but this can allow generating automatic hook code.
		/// </summary>
		public ModuleDefMD BepInExHooksModule { get; }

		/// <summary>
		/// The replacement module containing the mirror classes.
		/// </summary>
		public ModuleDefUser MirrorModule { get; }

		/// <summary>
		/// The name of the new module.
		/// </summary>
		public string NewModuleName { get; }

		private readonly AssemblyDefUser _asm;

		internal readonly ImportCache cache;

		private readonly Dictionary<TypeDef, (TypeDefUser, TypeDefUser, TypeRef)> _mirrorLookup = new Dictionary<TypeDef, (TypeDefUser, TypeDefUser, TypeRef)>();
		private readonly Dictionary<TypeDefUser, TypeRef> _originalRefs = new Dictionary<TypeDefUser, TypeRef>();
		private readonly HashSet<TypeDefUser> _validMembers = new HashSet<TypeDefUser>();

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="original">The module that these hooks will be generated for.</param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		/// <param name="settings">Any settings to change how the generator operates.</param>
		public MirrorGenerator(ModuleDefMD original, ModuleDefMD bieHooks, string newModuleName = null) {
			Module = original;
			BepInExHooksModule = bieHooks;
			NewModuleName = newModuleName ?? (original.Name + "-" + NAMESPACE.ToUpper());
			MirrorModule = new ModuleDefUser(NewModuleName);
			MirrorModule.RuntimeVersion = Module.RuntimeVersion;
			MirrorModule.Kind = ModuleKind.Dll;

			_asm = new AssemblyDefUser($"EXTENSIBLES-{original.Name}", new Version("1.0.0.0"));
			_asm.ProcessorArchitecture = AssemblyAttributes.PA_MSIL;
			_asm.Modules.Add(MirrorModule);

			ModuleRef[] refs = original.GetModuleRefs().ToArray();
			foreach (ModuleRef reference in refs) {
				_asm.Modules.Add(reference.Module);
			}

			cache = new ImportCache(MirrorModule);
			MirrorModule.EnableTypeDefFindCache = true;
			BepInExHooksModule.EnableTypeDefFindCache = true;
		}

		/// <summary>
		/// Returns whether or not the provided <see cref="TypeDef"/> is a mirror type generated by this code.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public bool IsDeclaredMirrorType(TypeDef type) {
			if (type is TypeDefUser userType) return _validMembers.Contains(userType);
			return false;
		}

		/// <summary>
		/// Returns a reference to the original type that a mirror type is mirroring.
		/// </summary>
		/// <param name="mirrorType"></param>
		/// <returns></returns>
		public TypeRef GetOriginal(TypeDefUser mirrorType) => _originalRefs[mirrorType];

		public void Generate() {
			
			//BoundAttributeCtor = ILGenerators.CreateBoundAttributeConstructor(this);

			// For the record, I know that doing this in three loops is kinda shit and wasteful.

			Console.WriteLine("Pre-generating all types...");
			Console.CursorTop--;
			TypeDef[] allTypes = Module.GetTypes().ToArray();
			int current = 0;
			int real = 0;
			foreach (TypeDef def in allTypes) {
				current++;
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

				string ns = def.Namespace;
				if (ns != null && ns.Length > 0) {
					ns = '.' + ns;
				}
				TypeDefUser replacement = new TypeDefUser($"{NAMESPACE}{ns}", def.Name, MirrorModule.CorLibTypes.Object.TypeRef);
				replacement.Attributes = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class;

				TypeDefUser binder = new TypeDefUser("Binder", MirrorModule.CorLibTypes.Object.ToTypeDefOrRef());
				binder.Attributes |= TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.NestedPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass;
				replacement.NestedTypes.Add(binder);

				TypeRef imported = cache.Import(def);
				_mirrorLookup[def] = (replacement, binder, imported);
				_validMembers.Add(replacement);
				_originalRefs[replacement] = imported;
				MirrorModule.Types.Add(replacement);
				real++;

				if (current % 100 == 0) {
					Console.WriteLine($"Pre-generating all types ({current} of {allTypes.Length}, skipped {current - real}...)");
					Console.CursorTop--;
				}
			}
			Console.WriteLine($"Pre-generating all types ({current} of {allTypes.Length}, skipped {current - real}...)");

			Console.WriteLine("Binding type inheritence...");
			Console.CursorTop--;
			current = 0;

			foreach (KeyValuePair<TypeDef, (TypeDefUser, TypeDefUser, TypeRef)> binding in _mirrorLookup) {
				(TypeDefUser replacement, TypeDefUser binder, TypeRef imported) = binding.Value;
				TypeDef defKey = binding.Key;

				foreach (TypeDef nested in defKey.NestedTypes) {
					if (_mirrorLookup.TryGetValue(nested, out (TypeDefUser, TypeDefUser, TypeRef) nestedCustomType)) {
						nestedCustomType.Item1.DeclaringType2 = replacement;
					}
				}
				if (defKey.BaseType != null && defKey.BaseType.ToTypeSig() != MirrorModule.CorLibTypes.Object) {
					TypeDef baseType = defKey.BaseType.ResolveTypeDef();
					if (baseType != null) {
						if (_mirrorLookup.TryGetValue(baseType, out (TypeDefUser, TypeDefUser, TypeRef) @base)) {
							replacement.BaseType = @base.Item1;
						}
					}
				}

				current++;
				if (current % 100 == 0) {
					Console.WriteLine($"Binding type inheritence ({current} of {real}...)");
					Console.CursorTop--;
				}
			}
			Console.WriteLine($"Binding type inheritence ({current} of {real}...)");
			Console.WriteLine("Generating type contents...");
			Console.CursorTop--;
			current = 0;

			foreach (KeyValuePair<TypeDef, (TypeDefUser, TypeDefUser, TypeRef)> binding in _mirrorLookup) {
				(TypeDefUser replacement, TypeDefUser binder, TypeRef imported) = binding.Value;
				GenerateReplacementType(binding.Key, imported, replacement, binder);
				current++;
				if (current % 100 == 0) {
					Console.WriteLine($"Generating type contents ({current} of {real}... // This step might take a while...)");
					Console.CursorTop--;
				}
			}
			Console.WriteLine($"Generating type contents ({current} of {real}... // This step might take a while...)");
			Console.WriteLine("Done processing!");
		}

		public void Save(FileInfo to) {
			Console.WriteLine("Saving to disk...");
			if (to.Exists) to.Delete();
			using FileStream stream = to.Open(FileMode.CreateNew);
			MirrorModule.CreatePdbState(PdbFileKind.PortablePDB);
			_asm.Write(stream);
			Console.WriteLine($"Done! {to.Name} has been written to {to.Directory.FullName} and is ready for use.");
		}



		/// <summary>
		/// Generates the entire type for a mirror, and registers it to the mirror module.
		/// </summary>
		/// <param name="from"></param>
		private void GenerateReplacementType(TypeDef original, TypeRef from, TypeDefUser replacement, TypeDefUser binder) {
			PropertyDefUser strongRef = BindOriginalReferenceAndCtor(from, replacement);
			(GenericVar tExtendsExtensible, TypeDefUser binderType) = CreateExtensibleBinderClass(from, replacement, binder);
			BindPropertyMirrors(original, replacement, strongRef);
			BindFieldMirrors(original, replacement, strongRef);
			BindMethodMirrors(original, from, replacement, binderType, strongRef, tExtendsExtensible);

			// Now close the binder class's static constructor
			binderType.FindOrCreateStaticConstructor().Body.Instructions.Add(dnlib.DotNet.Emit.OpCodes.Ret.ToInstruction());

		}

		/// <summary>
		/// Creates a private readonly field of type <see cref="WeakReference{T}"/>. This field weakly stores the original object that is being mirrored.
		/// This also creates a public property to access the weak reference easily.
		/// </summary>
		/// <param name="originalType"></param>
		/// <param name="to"></param>
		private PropertyDefUser BindOriginalReferenceAndCtor(ITypeDefOrRef originalType, TypeDefUser to) {
			TypeSig originalTypeSig = originalType.ToTypeSig();

			GenericInstSig weakRefInstance = new GenericInstSig(WeakReferenceTypeSig, originalTypeSig);

			FieldDefUser weakRef = new FieldDefUser("<Extensible>original", new FieldSig(weakRefInstance), PRIVATE_FIELD_TYPE | FieldAttributes.InitOnly);
			weakRef.IsSpecialName = true;
			weakRef.IsRuntimeSpecialName = true;

			string originalMemberName = "Original";
			if (originalType is TypeDef def) {
				if (def.FindProperty(originalMemberName) != null) {
					originalMemberName += "_Extensible_";
				}
				for (int i = 0; i < 100 && def.FindProperty(originalMemberName) != null; i++) {
					originalMemberName = "_" + originalMemberName;
				}
			}
			PropertyDefUser strongRef = new PropertyDefUser(originalMemberName, new PropertySig(true, originalTypeSig));
			ILGenerators.CreateOriginalReferencer(this, strongRef, weakRef, weakRefInstance);

			MethodDefUser ctorDef = ILGenerators.CreateConstructor(this, weakRef, weakRefInstance);

			to.Fields.Add(weakRef);
			to.Properties.Add(strongRef);
			to.Methods.Add(strongRef.GetMethod);
			to.Methods.Add(ctorDef);

			return strongRef;
		}

		private (GenericVar, TypeDefUser) CreateExtensibleBinderClass(TypeRef source, TypeDefUser extensible, TypeDefUser binder) {
			GenericParamUser genericParam = new GenericParamUser(0, GenericParamAttributes.DefaultConstructorConstraint, "TExtensible");
			genericParam.GenericParamConstraints.Add(new GenericParamConstraintUser(extensible));
			binder.GenericParameters.Add(genericParam);

			GenericVar tExtendsExtensible = new GenericVar(0, binder);
			TypeSig sourceType = source.ToTypeSig();

			GenericInstSig instancesCWT = new GenericInstSig(CWTTypeSig, sourceType, tExtendsExtensible);
			FieldDefUser instancesCache = new FieldDefUser("_instances", new FieldSig(instancesCWT), PRIVATE_FIELD_TYPE | FieldAttributes.Static);
			binder.Fields.Add(instancesCache);

			GenericInstSig binderInstance = new GenericInstSig(binder.ToTypeSig().ToClassOrValueTypeSig(), tExtendsExtensible);

			(MethodDefUser bind, MethodDefUser bindExisting, MethodDefUser destroy) = ILGenerators.GenerateBinderBindAndDestroyMethod(this, extensible, binder, binderInstance, sourceType, instancesCWT);
			binder.Methods.Add(bind);
			binder.Methods.Add(bindExisting);
			binder.Methods.Add(destroy);

			ILGenerators.GenerateInstancesConstructor(this, extensible, binder, binderInstance, instancesCWT);

			// REMEMBER: The function generator (where it ports the methods into the extensible type) is responsible for adding the four instructions
			// required to register the equivalent binder method. This leaves the function open (it has no ret) which is added at the end of the type generator
			// above. Do not do that here. Do not generate static constructor / event bind code here!

			return (tExtendsExtensible, binder);
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
					ILGenerators.CreateGetterToProperty(this, mirror, orgProp, orgRef);
					inUserType.Methods.Add(mirror.GetMethod);
				} else {
					mirror.GetMethod = null;
				}
				if (hasSetter) {
					ILGenerators.CreateSetterToProperty(this, mirror, orgProp, orgRef);
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

				ILGenerators.CreateGetterToField(this, mirror, orgField, orgRef);
				ILGenerators.CreateSetterToField(this, mirror, orgField, orgRef);

				inUserType.Properties.Add(mirror);
				inUserType.Methods.Add(mirror.GetMethod);
				inUserType.Methods.Add(mirror.SetMethod);
			}
		}

		private void BindMethodMirrors(TypeDef source, TypeRef importedSource, TypeDefUser inUserType, TypeDefUser binderType, PropertyDefUser orgRef, GenericVar tExtendsExtensible) {
			// BIE behavior: Use the original method delegate.
			foreach (MethodDef mtd in source.Methods) {
				if (mtd.IsStatic) continue;
				if (mtd.DeclaringType != source) continue;
				if (mtd.IsConstructor || mtd.IsStaticConstructor || mtd.Name == "Finalize") continue;
				if (mtd.IsSpecialName) continue; // properties do this.
				if (((string)mtd.Name)[0] == '<') continue;
				if (mtd.HasGenericParameters) continue;

				(MethodDefUser mirror, FieldDefUser origDelegateRef, FieldDefUser origMtdInUse, MethodDefUser binderMirror) = ILGenerators.TryGenerateBIEOrigCallAndProxies(this, mtd, orgRef, binderType, tExtendsExtensible, importedSource);
				if (mirror != null && origDelegateRef != null && origMtdInUse != null && binderMirror != null) {
					inUserType.Methods.Add(mirror);
					inUserType.Fields.Add(origDelegateRef);
					inUserType.Fields.Add(origMtdInUse);
					binderType.Methods.Add(binderMirror); // Remember to use bindertype!
				}
			}

		}

	}
}
