using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.ILGeneration;
using HookGenExtender.Core.ReferenceHelpers;
using HookGenExtender.Core.Utils.Ext;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		public static readonly Version CURRENT_EXTENSIBLES_VERSION = new Version(2, 0, 0, 0);

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

		private readonly AssemblyDef _asm;

		/// <summary>
		/// A cache for imported types.
		/// </summary>
		public ImportCache Cache { get; }

		/// <summary>
		/// Commonly used types that are common across many pieces of the code.
		/// </summary>
		public SharedTypes Shared { get; }

		/// <summary>
		/// Create a new generator.
		/// </summary>
		/// <param name="gameAssembly">The module that these hooks will be generated for.</param>
		/// <param name="newModuleName">The name of the replacement module, or null to use the built in name. This should usually mimic that of the normal module.</param>
		public ExtensiblesGenerator(ModuleDefMD gameAssembly, ModuleDefMD bepInExHooks, string newModuleName = null) {
			Original = gameAssembly;
			BepInExHooksModule = bepInExHooks;
			NewModuleName = newModuleName ?? ("EXTENSIBLES-" + gameAssembly.Name);
			Extensibles = new ModuleDefUser(NewModuleName);
			Extensibles.RuntimeVersion = Original.RuntimeVersion;
			Extensibles.Kind = ModuleKind.Dll;

			_asm = new AssemblyDefUser($"EXTENSIBLES-{gameAssembly.Name}", new Version("1.0.0.0"));
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

		public void Generate() {
			// For the record, I know that doing this in three loops is kinda shit and wasteful.

			Stopwatch sw = new Stopwatch();
			Console.WriteLine("Pre-generating all types and generating cache...");
			Console.CursorTop--;
			TypeDef[] allTypes = Original.GetTypes().ToArray();
			int current = 0;
			int real = 0;
			int time = 0;
			int elapsed = 0;
			sw.Start();

			Dictionary<TypeDef, ExtensibleTypeData> extensibleLookup = new Dictionary<TypeDef, ExtensibleTypeData>();
			foreach (TypeDef def in allTypes) {
				current++;
				if (!IsTypeAllowedCallback(def)) continue;
				if (def.IsGlobalModuleType) continue;
				if (def.IsDelegate) continue;
				if (def.IsEnum) continue;
				if (def.IsForwarder) continue;
				if (def.IsInterface) continue;
				if (def.IsPrimitive) continue;
				if (def.IsSpecialName) continue;
				if (def.IsRuntimeSpecialName) continue;
				if (def.IsStatic()) continue;
				//if (def.Name.StartsWith("<")) continue; // TODO: Better version of this.
				if (def.IsCompilerGenerated()) continue;
#if DEBUG
				if (def.Name.StartsWith("<")) Debugger.Break(); // This should not happen.
#endif
				if (def.Namespace.StartsWith("Microsoft.CodeAnalysis")) continue;
				if (def.Namespace.StartsWith("System.")) continue;
				if (!def.HasBIEHookClass(this)) continue;

				string ns = def.Namespace;
				if (ns != null && ns.Length > 0) {
					ns = '.' + ns;
				}
				CachedTypeDef replacement = new CachedTypeDef(Extensibles, $"Extensibles{ns}", def.Name, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Class | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass);
				
				CachedTypeDef binder = new CachedTypeDef(Extensibles, "Binder`1", TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.NestedPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass);
				GenericParam binderGeneric = new GenericParamUser(0, GenericParamAttributes.NonVariant, "TExtensible");
				binderGeneric.GenericParamConstraints.Add(new GenericParamConstraintUser(replacement.Reference));
				binder.GenericParameters.Add(binderGeneric);

				replacement.AddInnerClass(binder);

				extensibleLookup[def] = new ExtensibleTypeData(def, Cache.Import(def), replacement, binder);

				real++;

				if (current == 0) {
					Console.WriteLine("                                                ");
					Console.CursorTop--;
				}
				if (current % 100 == 0) {
					Console.WriteLine($"Pre-generating all types ({current} of {allTypes.Length}, skipped {current - real}...)");
					Console.CursorTop--;
				}
			}
			sw.Stop();
			time = (int)Math.Round(sw.Elapsed.TotalSeconds);
			elapsed = time;
			Console.WriteLine($"Pre-generating all types ({current} of {allTypes.Length}, skipped {current - real}...) took {time} seconds.");

			Console.WriteLine("Binding type inheritence...");
			Console.CursorTop--;
			current = 0;

			sw.Restart();
			foreach (KeyValuePair<TypeDef, ExtensibleTypeData> binding in extensibleLookup) {
				TypeDef gameOriginal = binding.Key;
				ExtensibleTypeData replacement = binding.Value;
				foreach (TypeDef nested in binding.Key.NestedTypes) {
					if (extensibleLookup.TryGetValue(nested, out ExtensibleTypeData nestedCustomType)) {
						replacement.ExtensibleType.AddInnerClass(nestedCustomType.ExtensibleType);
					}
				}
				if (gameOriginal.BaseType != null && !gameOriginal.BaseType.IsCorLibType()) {
					TypeDef gameOriginalBaseType = gameOriginal.BaseType.ResolveTypeDef();
					if (gameOriginalBaseType != null) {
						if (extensibleLookup.TryGetValue(gameOriginalBaseType, out ExtensibleTypeData replacementBaseType)) {
							replacementBaseType.ExtensibleType.AddChildType(replacement.ExtensibleType);
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

			Console.WriteLine("Generating type contents...");
			Console.CursorTop--;
			current = 0;

			sw.Restart();
			foreach (KeyValuePair<TypeDef, ExtensibleTypeData> binding in extensibleLookup) {
				MakeExtensibleTypeProcedure(binding.Value);
				current++;
				if (current % 100 == 0) {
					Console.WriteLine($"Generating type contents ({current} of {real}... // This step might take a while...)");
					Console.CursorTop--;
				}
			}
			sw.Stop();
			time = (int)Math.Round(sw.Elapsed.TotalSeconds);
			elapsed += time;
			Console.WriteLine($"Generating type contents ({current} of {real}... // This step might take a while...) took {time} seconds.");

			ITypeDefOrRef asmFileVerAttr = Cache.Import(typeof(System.Reflection.AssemblyVersionAttribute));
			MemberRefUser fileVerCtor = new MemberRefUser(Extensibles, ".ctor", MethodSig.CreateInstance(CorLibTypeSig(), CorLibTypeSig<string>()), asmFileVerAttr);
			CustomAttribute version = new CustomAttribute(fileVerCtor, new CAArgument[] { new CAArgument(CorLibTypeSig<string>(), CURRENT_EXTENSIBLES_VERSION.ToString()) });
			_asm.CustomAttributes.Add(version);

			ITypeDefOrRef secPermsTypeRef = Cache.Import(typeof(System.Security.Permissions.SecurityPermissionAttribute));
			List<CANamedArgument> namedArgs = new List<CANamedArgument> { new CANamedArgument(false, CorLibTypeSig<bool>(), nameof(System.Security.Permissions.SecurityPermissionAttribute.SkipVerification), new CAArgument(CorLibTypeSig<bool>(), true)) };
			_asm.DeclSecurities.Add(new DeclSecurityUser(SecurityAction.RequestMinimum, new List<SecurityAttribute>() { new SecurityAttribute(secPermsTypeRef, namedArgs) }));

			_asm.Version = CURRENT_EXTENSIBLES_VERSION;
			Extensibles.CreatePdbState(PdbFileKind.PortablePDB);

			Console.WriteLine($"Done processing! Took {elapsed} seconds.");
		}

		private void MakeExtensibleTypeProcedure(ExtensibleTypeData of) {
			MemberTemplates.MakeExtensibleCoreMembers(this, of);
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
		public CorLibTypeSig CorLibTypeSig(Type type = null) {
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
