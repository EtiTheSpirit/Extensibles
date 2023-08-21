using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using HookGenExtender.Core.ILGeneration;
using HookGenExtender.Core.ReferenceHelpers;
using HookGenExtender.Core.Utils;
using HookGenExtender.Core.Utils.Ext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Void = HookGenExtender.Core.DataStorage.ExtremelySpecific.Void;

namespace HookGenExtender.Core {
	public sealed class ExtensiblesGenerator {

		/// <summary>
		/// This is the type of private used when declaring private fields (because C# has more than one level of private internally).
		/// </summary>
		public const FieldAttributes PRIVATE_FIELD_TYPE = FieldAttributes.PrivateScope;

		/// <summary>
		/// This is the type of private used when declaring private methods (because C# has more than one level of private internally).
		/// </summary>
		public const MethodAttributes PRIVATE_METHOD_TYPE = (MethodAttributes)PRIVATE_FIELD_TYPE;

		/// <summary>
		/// The version of the current Extensibles module. This is used by the generator to determine if the existing DLL in the BepInEx folder is outdated.
		/// </summary>
		public static readonly Version CURRENT_EXTENSIBLES_VERSION = new Version(2, 1, 0, 0);

		/// <summary>
		/// The original module that is being mirrored.
		/// </summary>
		public ModuleDefMD Original { get; }

		/// <summary>
		/// The -HOOKS DLL provided by BepInEx. This is used to figure out what to actually make into an extensible in the first place
		/// (only BIE hooked classes are used)
		/// </summary>
		public ModuleDefMD BepInExHooksModule { get; }

		/// <summary>
		/// The replacement module containing the mirror classes, which is where Extensibles exists.
		/// </summary>
		public ModuleDefUser Extensibles { get; }

		/// <summary>
		/// The name of the new module. This is its assembly name, rather than the name of the DLL file. It can be null to use a default name.
		/// </summary>
		public string NewModuleName { get; }

		/// <summary>
		/// Set this callback to filter out which types have extensible variants generated. Return true to allow, false to delete.
		/// </summary>
		public Func<ITypeDefOrRef, bool> IsTypeAllowedCallback { get; set; } = _ => true;

		/// <summary>
		/// Set this callback to filter out certain fields from being proxied. Return true to allow, false to delete.
		/// </summary>
		public Func<IMemberRef, bool> IsFieldAllowedCallback { get; set; } = _ => true;
		/// <summary>
		/// Set this callback to filter out certain methods from being proxied. Return true to allow, false to delete.
		/// </summary>
		public Func<IMemberRef, bool> IsMethodAllowedCallback { get; set; } = _ => true;

		/// <summary>
		/// Set this callback to filter out certain properties from being proxied. Return true to allow, false to delete.
		/// </summary>
		public Func<IMemberRef, bool> IsPropertyAllowedCallback { get; set; } = _ => true;


		/// <summary>
		/// A cache for imported types.
		/// </summary>
		public ImportCache Cache { get; }

		/// <summary>
		/// Commonly used types that are common across many pieces of the code.
		/// </summary>
		public SharedTypes Shared { get; }


		private readonly AssemblyDef _asm;
		private readonly Dictionary<TypeDef, ExtensibleTypeData> _extensibleLookup = new Dictionary<TypeDef, ExtensibleTypeData>();
		private readonly Dictionary<CachedTypeDef, ExtensibleTypeData> _extensibleLookupByCached = new Dictionary<CachedTypeDef, ExtensibleTypeData>();
		private bool _generated = false;

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="gameAssembly">The module that these hooks will be generated for.</param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		public ExtensiblesGenerator(ModuleDefMD gameAssembly, ModuleDefMD bepInExHooks, string newModuleName = null) {
			Original = gameAssembly;
			BepInExHooksModule = bepInExHooks;
			NewModuleName = newModuleName ?? ("EXTENSIBLES-" + gameAssembly.Name);
			NewModuleName = NewModuleName.Trim();
			NewModuleName = Path.ChangeExtension(NewModuleName, null);

			Extensibles = new ModuleDefUser(NewModuleName);
			Extensibles.RuntimeVersion = Original.RuntimeVersion;
			Extensibles.Kind = ModuleKind.Dll;

			_asm = new AssemblyDefUser(NewModuleName, CURRENT_EXTENSIBLES_VERSION);
			_asm.ProcessorArchitecture = AssemblyAttributes.PA_MSIL;
			_asm.Modules.Add(Extensibles);

			ModuleRef[] refs = gameAssembly.GetModuleRefs().ToArray();
			foreach (ModuleRef reference in refs) {
				_asm.Modules.Add(reference.Module);
			}
			Extensibles.EnableTypeDefFindCache = false;
			BepInExHooksModule.EnableTypeDefFindCache = true;

			Cache = new ImportCache(Extensibles);
			Shared = new SharedTypes(this);
		}

		public IEnumerable<ExtensibleTypeData> GetAllExtensibleTypes() {
			return _extensibleLookup.Values;
		}

		/// <summary>
		/// Attempts to get the parent of the provided <see cref="ExtensibleTypeData"/>. The parent is its base type in
		/// <see cref="ExtensibleTypeData"/> form.
		/// </summary>
		/// <param name="inputType"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		internal bool TryGetParent(ExtensibleTypeData inputType, out ExtensibleTypeData parent) {
			if (inputType.ExtensibleType.Base is CachedTypeDef ctd) {
				return _extensibleLookupByCached.TryGetValue(ctd, out parent);

			}
			parent = null;
			return false;
		}

		public void Generate() {
			if (_generated) throw new InvalidOperationException("This has already been generated before.");
			_generated = true;

			Stopwatch sw = new Stopwatch();
			Console.WriteLine("Step 1: Generate types and build cache // This will take a moment. Please wait...");
			TypeDef[] allTypes = Original.GetTypes().ToArray();
			int current = 0;
			int real = 0;
			int time = 0;
			int elapsed = 0;
			sw.Start();

			foreach (TypeDef def in allTypes) {
				current++;
				if (!IsTypeAllowedCallback(def)) continue;
				if (def.IsGlobalModuleType) continue;
				if (def.IsDelegate) continue;
				if (def.IsEnum) continue;
				if (def.IsValueType) continue;
				if (def.IsForwarder) continue;
				if (def.IsInterface) continue;
				if (def.IsPrimitive) continue;
				if (def.IsSpecialName) continue;
				if (def.IsRuntimeSpecialName) continue;
				if (def.IsStatic()) continue;
				if (def.IsCompilerGenerated()) continue;
				if (def.Namespace.StartsWith("Microsoft.CodeAnalysis")) continue;
				if (def.Namespace.StartsWith("System.")) continue;
				if (!def.HasBIEHookClass(this)) continue;

				string ns = def.Namespace;
				if (ns != null && ns.Length > 0) {
					ns = '.' + ns;
				}
				CachedTypeDef replacement = new CachedTypeDef(this, $"Extensible{ns}", def.Name, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass);
				replacement.Underlying.BaseType = CorLibTypeRef<object>();

				CachedTypeDef binder = new CachedTypeDef(this, "Binder`1", TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.NestedPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass);
				GenericParam binderGeneric = new GenericParamUser(0, GenericParamAttributes.NonVariant, "TExtensible");
				binderGeneric.GenericParamConstraints.Add(new GenericParamConstraintUser(replacement.Underlying));
				binder.GenericParameters.Add(binderGeneric);
				binder.Underlying.BaseType = CorLibTypeRef<object>();

				binder.Underlying.DeclaringType2 = replacement.Underlying;

				_extensibleLookup[def] = new ExtensibleTypeData(def, Cache.Import(def), replacement, binder);
				_extensibleLookupByCached[replacement] = _extensibleLookup[def];

				real++;

				if (current % 100 == 0) {
					Console.WriteLine($"Generating types... ({current} of {allTypes.Length}, skipped {current - real}...)");
					Console.CursorTop--;
				}
			}
			sw.Stop();
			time = (int)Math.Round(sw.Elapsed.TotalSeconds);
			elapsed = time;
			Console.WriteLine($"Generating types... ({current} of {allTypes.Length}, skipped {current - real}...) took {time} seconds.");

			Console.WriteLine("Binding type inheritence...");
			Console.CursorTop--;
			current = 0;

			sw.Restart();
			foreach (KeyValuePair<TypeDef, ExtensibleTypeData> binding in _extensibleLookup) {
				TypeDef gameOriginal = binding.Key;
				ExtensibleTypeData replacement = binding.Value;
				foreach (TypeDef nested in binding.Key.NestedTypes) {
					if (_extensibleLookup.TryGetValue(nested, out ExtensibleTypeData nestedCustomType)) {
						nestedCustomType.ExtensibleType.Underlying.DeclaringType2 = replacement.ExtensibleType.Underlying;
						nestedCustomType.ExtensibleType.Underlying.Namespace = null; // Remove its namespace whilst nested.
						nestedCustomType.ExtensibleType.Underlying.Attributes |= TypeAttributes.NestedPublic; // Mark it as nested.
					}
				}
				if (gameOriginal.BaseType != null && !gameOriginal.BaseType.IsCorLibType()) {
					TypeDef gameOriginalBaseType = gameOriginal.BaseType.ResolveTypeDef();
					if (gameOriginalBaseType != null) {
						if (_extensibleLookup.TryGetValue(gameOriginalBaseType, out ExtensibleTypeData replacementBaseType)) {
							replacement.ExtensibleType.SetBase(replacementBaseType.ExtensibleType);
						}
					}
				}

				current++;
				if (current % 100 == 0) {
					Console.WriteLine($"Binding type inheritence ({current} of {real}...)");
					Console.CursorTop--;
				}
			}
			sw.Stop();
			time = (int)Math.Round(sw.Elapsed.TotalSeconds);
			elapsed += time;
			Console.WriteLine($"Binding type inheritence ({current} of {real}...) took {time} seconds.");
			Console.WriteLine("Step 2: Bind types and generate code.");

			Console.WriteLine("Generating type contents (Pass 1)...");
			Console.CursorTop--;
			current = 0;

			sw.Restart();
			Dictionary<ExtensibleTypeData, (ExtensibleCoreMembers, ExtensibleBinderCoreMembers)> lookup = new Dictionary<ExtensibleTypeData, (ExtensibleCoreMembers, ExtensibleBinderCoreMembers)>();
			foreach (KeyValuePair<TypeDef, ExtensibleTypeData> binding in _extensibleLookup) {
				MakeExtensibleTypeProcedure(binding.Value, lookup);
				current++;
				if (current % 100 == 0) {
					Console.WriteLine($"Generating type contents (Pass 1) ({current} of {real}...)             ");
					Console.CursorTop--;
				}
			}
			current = 0;
			foreach (KeyValuePair<TypeDef, ExtensibleTypeData> binding in _extensibleLookup) {
				FinalizeMakeExtensibleTypeProcedure(lookup[binding.Value], binding.Value);
				current++;
				if (current % 100 == 0) {
					Console.WriteLine($"Generating type contents (Pass 2) ({current} of {real}...)             ");
					Console.CursorTop--;
				}
			}
			sw.Stop();
			time = (int)Math.Round(sw.Elapsed.TotalSeconds);
			elapsed += time;
			Console.WriteLine();
			Console.WriteLine($"Generating type contents ({current} of {real}...) took {time} seconds total.       ");

			// TO FUTURE XAN/MAINTAINERS:
			// For *some reason*, this value here (added by the custom attribute) is used when
			// loading the assembly from file to do a version check...
			ITypeDefOrRef asmFileVerAttr = Cache.Import(typeof(System.Reflection.AssemblyVersionAttribute));
			MemberRefUser fileVerCtor = new MemberRefUser(Extensibles, ".ctor", MethodSig.CreateInstance(CorLibTypeSig<Void>(), CorLibTypeSig<string>()), asmFileVerAttr);
			CustomAttribute version = new CustomAttribute(fileVerCtor, new CAArgument[] { new CAArgument(CorLibTypeSig<string>(), CURRENT_EXTENSIBLES_VERSION.ToString()) });
			_asm.CustomAttributes.Add(version);

			ITypeDefOrRef secPermsTypeRef = Cache.Import(typeof(System.Security.Permissions.SecurityPermissionAttribute));
			List<CANamedArgument> namedArgs = new List<CANamedArgument> { new CANamedArgument(false, CorLibTypeSig<bool>(), nameof(System.Security.Permissions.SecurityPermissionAttribute.SkipVerification), new CAArgument(CorLibTypeSig<bool>(), true)) };
			_asm.DeclSecurities.Add(new DeclSecurityUser(SecurityAction.RequestMinimum, new List<SecurityAttribute>() { new SecurityAttribute(secPermsTypeRef, namedArgs) }));

			// ...yet, for *some other reason*, this is not, but without it
			// tools like DNSpy see the default 1.0.0.0 version.
			// So both it is.
			_asm.Version = CURRENT_EXTENSIBLES_VERSION;
			Extensibles.CreatePdbState(PdbFileKind.PortablePDB);

			Console.WriteLine($"Done processing! Took {elapsed} seconds.");
		}

		public void Save(FileInfo to, FileInfo documentation = null) {
			Console.WriteLine("Saving to disk. // This will take a moment. Please wait...");
			if (to.Exists) to.Delete();
			using FileStream stream = to.Open(FileMode.CreateNew);

			_asm.Write(stream);

			if (documentation != null) {
				Console.WriteLine("Saving and generating minimal docs...");
				ShittyDocumentationGenerator.GenerateDocumentation(this, documentation);
			}

			Console.WriteLine($"Done! {to.Name} has been written to {to.Directory.FullName} and is ready for use.");
		}

		private void MakeExtensibleTypeProcedure(ExtensibleTypeData of, Dictionary<ExtensibleTypeData, (ExtensibleCoreMembers, ExtensibleBinderCoreMembers)> lookup) {
			ExtensibleCoreMembers coreMembers = MemberTemplates.MakeExtensibleCoreMembers(this, of);
			ExtensibleBinderCoreMembers binderMembers = MemberTemplates.MakeBinderCoreMembers(this, of);
			MemberTemplates.InitializeCreateBindingsMethod(this, in binderMembers);

			BindFieldMirrors(of, in coreMembers);
			BindPropertyMirrors(of, in coreMembers, in binderMembers);
			BindMethodMirrors(of, in coreMembers, in binderMembers);

			MakeBinderBindMethods(of, in coreMembers, in binderMembers);

			// CAN NOT CALL HERE.
			// MemberTemplates.FinalizeCreateHooksMethod(this, in coreMembers, in binderMembers);
			lookup.Add(of, (coreMembers, binderMembers));
		}

		private void FinalizeMakeExtensibleTypeProcedure((ExtensibleCoreMembers, ExtensibleBinderCoreMembers) data, ExtensibleTypeData binding) {
			MemberTemplates.LateWriteConstructorOfCoreMbr(this, in data.Item1);
			MemberTemplates.FinalizeCreateBindingsMethod(this, in data.Item1, in data.Item2);
			MemberTemplates.MakeImplicitCasts(this, binding, binding);
		}

		private void MakeBinderBindMethods(ExtensibleTypeData extensible, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			MemberTemplates.MakeBindMethodFromCommonConstructor(this, in coreMembers, in binderMembers);
			int currentIndex = 1;
			foreach (MethodDef constructor in extensible._originalGameType.FindInstanceConstructors()) {
				MethodDefAndRef ctorRef = new MethodDefAndRef(this, constructor, extensible.ImportedGameType, true);
				MemberTemplates.MakeBindMethodFromConstructor(this, ctorRef, currentIndex++, in coreMembers, in binderMembers);
			}
		}

		private void BindFieldMirrors(ExtensibleTypeData extensible, in ExtensibleCoreMembers coreMembers) {
			foreach (FieldDef field in extensible._originalGameType.Fields) {
				if (field.IsStatic) continue;
				if (field.IsSpecialName || field.IsRuntimeSpecialName) continue;
				if (field.IsCompilerGenerated()) continue;
				if (!IsFieldAllowedCallback(field)) continue;

				MemberTemplates.MakeExtensibleFieldProxy(this, in coreMembers, extensible, new FieldDefAndRef(this, field, extensible.ImportedGameType, true));
			}
		}

		private void BindPropertyMirrors(ExtensibleTypeData extensible, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			foreach (PropertyDef property in extensible._originalGameType.Properties) {
				if (property.IsStatic()) continue;
				if (property.IsCompilerGenerated()) continue;
				if (!IsPropertyAllowedCallback(property)) continue;
				PropertyDefAndRef propertyInstance = new PropertyDefAndRef(this, property, extensible.ImportedGameType, true);

				ProxyAndHookPackage proxyAndHook = MemberTemplates.MakeExtensiblePropertyProxies(this, propertyInstance, in coreMembers);
				(MethodDefAndRef extBinderGetter, MethodDefAndRef extBinderSetter) = MemberTemplates.CodeBinderPropertyHooks(this, in coreMembers, in binderMembers, in proxyAndHook);
				string name = property.Name;
				if (extBinderGetter != null) {
					MemberTemplates.AddMemberBindToCreateHooksMethod(this, extBinderGetter, in coreMembers, in binderMembers, proxyAndHook.PropertyGetterProxyMembers.Value, proxyAndHook.PropertyGetterHookMembers.Value, name, false);
					binderMembers.type.Binder.AddMethod(extBinderGetter);
				}
				if (extBinderSetter != null) {
					MemberTemplates.AddMemberBindToCreateHooksMethod(this, extBinderSetter, in coreMembers, in binderMembers, proxyAndHook.PropertySetterProxyMembers.Value, proxyAndHook.PropertySetterHookMembers.Value, name, true);
					binderMembers.type.Binder.AddMethod(extBinderSetter);
				}

			}
		}

		private void BindMethodMirrors(ExtensibleTypeData extensible, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			foreach (MethodDef method in extensible._originalGameType.Methods) {
				if (method.IsStatic) continue;
				if (method.IsSpecialName || method.IsRuntimeSpecialName) continue; // properties do this.
				if (method.HasGenericParameters) continue;
				if (method.IsConstructor || method.IsStaticConstructor || method.Name == "Finalize") continue;
				if (method.IsCompilerGenerated()) continue;
				if (!IsMethodAllowedCallback(method)) continue;
				MethodDefAndRef methodInstance = new MethodDefAndRef(this, method, extensible.ImportedGameType, true);

				if (BepInExTools.TryGetBIEHook(this, extensible, methodInstance, out BepInExHookRef hookInfo)) {
					ProxyAndHookPackage proxyAndHook = MemberTemplates.MakeExtensibleMethodProxy(this, in coreMembers, in hookInfo);
					MethodDefAndRef extBinderMethod = MemberTemplates.CodeBinderMethodHook(this, in coreMembers, in binderMembers, in proxyAndHook);
					MemberTemplates.AddMemberBindToCreateHooksMethod(this, extBinderMethod, in coreMembers, in binderMembers, proxyAndHook.MethodProxyMembers, proxyAndHook.MethodHookMembers, null, false);

					binderMembers.type.Binder.AddMethod(extBinderMethod);
				}
			}

		}

		#region Utilities

		/// <summary>
		/// Returns the signature of the appropriate type from mscorlib based on the provided type parameter <typeparamref name="T"/>. See <see cref="ICorLibTypes"/> for valid types.
		/// <para/>
		/// To get <see langword="void"/>, call this without a generic parameter.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public CorLibTypeSig CorLibTypeSig<T>() => CorLibTypeSig(typeof(T));

		/// <summary>
		/// Returns a reference to the appropriate type from mscorlib based on the provided type parameter <typeparamref name="T"/>. See <see cref="ICorLibTypes"/> for valid types.
		/// <para/>
		/// To get <see langword="void"/>, call this without a generic parameter.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public ITypeDefOrRef CorLibTypeRef<T>() => CorLibTypeRef(typeof(T));

		/// <summary>
		/// Returns the signature of the appropriate type from mscorlib based on the provided <paramref name="type"/>. See <see cref="ICorLibTypes"/> for valid types.
		/// <para/>
		/// Use a <see langword="null"/> type for <see langword="void"/> (the default).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public CorLibTypeSig CorLibTypeSig(Type type) {
			ICorLibTypes corLibTypes = Extensibles.CorLibTypes;
			if (type is null) return corLibTypes.Void;

			if (type == typeof(bool)) {
				return corLibTypes.Boolean;
   			} else if (type == typeof(sbyte)) {
				return corLibTypes.SByte;
			} else if (type == typeof(byte)) {
				return corLibTypes.Byte;
			} else if (type == typeof(short)) {
				return corLibTypes.Int16;
			} else if (type == typeof(ushort)) {
				return corLibTypes.UInt16;
			} else if (type == typeof(int)) {
				return corLibTypes.Int32;
			} else if (type == typeof(uint)) {
				return corLibTypes.UInt32;
			} else if (type == typeof(long)) {
				return corLibTypes.Int64;
			} else if (type == typeof(ulong)) {
				return corLibTypes.UInt64;
			} else if (type == typeof(object)) {
				return corLibTypes.Object;
			} else if (type == typeof(string)) {
				return corLibTypes.String;
			} else if (type == typeof(nint)) {
				return corLibTypes.IntPtr;
			} else if (type == typeof(nuint)) {
				return corLibTypes.UIntPtr;
			} else if (type == typeof(float)) {
				return corLibTypes.Single;
			} else if (type == typeof(double)) {
				return corLibTypes.Double;
			} else if (type == typeof(TypedReference)) {
				return corLibTypes.TypedReference;
			} else if (type == typeof(void) || type == typeof(Void)) {
				return corLibTypes.Void;
			} else {
				throw new ArgumentException($"The provided generic type {type} is not a type provided in {nameof(ICorLibTypes)}.");
			}
		}

		/// <summary>
		/// Returns a reference to the appropriate type from mscorlib based on the provided <paramref name="type"/>. See <see cref="ICorLibTypes"/> for valid types.
		/// <para/>
		/// Use a <see langword="null"/> type for <see langword="void"/> (the default).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public ITypeDefOrRef CorLibTypeRef(Type type = null) => CorLibTypeSig(type).TypeDefOrRef;

		#endregion
	}
}
