using dnlib.DotNet;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Void = HookGenExtender.Core.DataStorage.ExtremelySpecific.Void;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// Commonly used or otherwise shared types.
	/// </summary>
	public class SharedTypes {

		#region Storage Types and Members

		#region WeakReference<T>

		/// <summary>
		/// The type reference to <see cref="WeakReference{T}"/>.
		/// </summary>
		public ITypeDefOrRef WeakReference { get; }

		/// <summary>
		/// The type signature of <see cref="WeakReference{T}"/>
		/// </summary>
		public TypeSig WeakReferenceSig { get; }

		/// <summary>
		/// The signature of <see langword="new"/> <see cref="WeakReference{T}(T)"/>
		/// </summary>
		public MethodSig WeakReferenceCtorSig { get; }

		/// <summary>
		/// The signature of <see cref="WeakReference{T}.TryGetTarget(out T)"/>
		/// </summary>
		public MethodSig WeakRefTryGetTargetSig { get; }

		#endregion

		#region ConditionalWeakTable<TKey, TValue>

		/// <summary>
		/// The type reference to <see cref="ConditionalWeakTable{TKey, TValue}"/>
		/// </summary>
		public ITypeDefOrRef CWTReference { get; }

		/// <summary>
		/// The type signature of <see cref="ConditionalWeakTable{TKey, TValue}"/>
		/// </summary>
		public TypeSig CWTSig { get; }

		/// <summary>
		/// The signature of <see langword="new"/> <see cref="ConditionalWeakTable{TKey, TValue}"/>()
		/// </summary>
		public MethodSig CWTCtorSig { get; }

		/// <summary>
		/// The signature of <see cref="ConditionalWeakTable{TKey, TValue}.Add(TKey, TValue)"/>
		/// </summary>
		public MethodSig CWTAddSig { get; }

		/// <summary>
		/// The signature of <see cref="ConditionalWeakTable{TKey, TValue}.Remove(TKey)"/>
		/// </summary>
		public MethodSig CWTRemoveSig { get; }

		/// <summary>
		/// The signature of <see cref="ConditionalWeakTable{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
		/// </summary>
		public MethodSig CWTTryGetValueSig { get; }

		#endregion

		#region HashSet<T>

		/// <summary>
		/// The type reference to <see cref="HashSet{T}"/>.
		/// </summary>
		public ITypeDefOrRef HashSetReference { get; }

		/// <summary>
		/// The type signature of <see cref="HashSet{T}"/>
		/// </summary>
		public TypeSig HashSetSig { get; }

		/// <summary>
		/// The signature of <see langword="new"/> <see cref="HashSet{T}"/>().
		/// </summary>
		public MethodSig HashSetCtorSig { get; }

		/// <summary>
		/// The signature of <see cref="HashSet{T}.Add(T)"/>.
		/// </summary>
		public MethodSig HashSetAddSig { get; }

		/// <summary>
		/// The signature of <see cref="HashSet{T}.Remove(T)"/>.
		/// </summary>
		public MethodSig HashSetRemoveSig { get; }

		/// <summary>
		/// The signature of <see cref="HashSet{T}.Contains(T)"/>.
		/// </summary>
		public MethodSig HashSetContainsSig { get; }

		/// <summary>
		/// The signature of a predefined generic instance of <see cref="HashSet{T}"/> where <c>T</c> is <see cref="string"/>.
		/// </summary>
		public GenericInstSig HashSetStringInstanceSig { get; }

		/// <summary>
		/// A predefined generic instance of <see cref="HashSet{T}"/> where <c>T</c> is <see cref="string"/>.
		/// </summary>
		public ITypeDefOrRef HashSetStringInstanceReference { get; }

		/// <summary>
		/// An alias to <see cref="HashSetAddSig"/> on <see cref="HashSet{T}"/> where <c>T</c> is <see cref="string"/>.
		/// </summary>
		public MemberRef HashSetStringAdd { get; }

		/// <summary>
		/// An alias to <see cref="HashSetContainsSig"/> on <see cref="HashSet{T}"/> where <c>T</c> is <see cref="string"/>.
		/// </summary>
		public MemberRef HashSetStringContains { get; }

		#endregion

		#endregion

		#region Exception Types

		/// <summary>
		/// The signature of any exception constructor that accepts a single string parameter.
		/// </summary>
		public MethodSig ExceptionStringCtorSig { get; }

		/// <summary>
		/// The type reference to <see cref="InvalidOperationException"/>.
		/// </summary>
		public ITypeDefOrRef InvalidOperationExceptionType { get; }

		/// <summary>
		/// A reference to <see langword="new"/> <see cref="InvalidOperationException(string)"/>
		/// </summary>
		public MemberRef InvalidOperationExceptionCtor { get; }

		/// <summary>
		/// The type reference to <see cref="MissingMemberException"/>
		/// </summary>
		public ITypeDefOrRef MissingMemberExceptionType { get; }

		#endregion

		#region .NET Types

		#region Reflection

		/// <summary>
		/// A reference to <see cref="BindingFlags"/>
		/// </summary>
		public ITypeDefOrRef BindingFlagsReference { get; }

		/// <summary>
		/// The signature of <see cref="BindingFlags"/>
		/// </summary>
		public TypeSig BindingFlagsSig { get; }

		/// <summary>
		/// The signature of <see cref="Type"/>.
		/// </summary>
		public TypeSig TypeSig { get; }

		/// <summary>
		/// The signature of <see cref="Type"/>[].
		/// </summary>
		public TypeSig TypeArraySig { get; }

		/// <summary>
		/// The type of <see cref="Type"/>. That's like 8 whole layers of types.
		/// </summary>
		public ITypeDefOrRef TypeReference { get; }

		/// <summary>
		/// The signature of <see cref="MethodInfo"/>
		/// </summary>
		public TypeSig MethodInfoSig { get; }

		/// <summary>
		/// The type of <see cref="MethodInfo"/>.
		/// </summary>
		public ITypeDefOrRef MethodInfoReference { get; }

		/// <summary>
		/// The signature of <see cref="MethodBase"/>
		/// </summary>
		public TypeSig MethodBaseSig { get; }

		/// <summary>
		/// The type of <see cref="MethodBase"/>.
		/// </summary>
		public ITypeDefOrRef MethodBaseReference { get; }

		/// <summary>
		/// The signature of <see cref="PropertyInfo"/>
		/// </summary>
		public TypeSig PropertyInfoSig { get; }

		/// <summary>
		/// The type of <see cref="PropertyInfo"/>.
		/// </summary>
		public ITypeDefOrRef PropertyInfoReference { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetTypeFromHandle(RuntimeTypeHandle)"/>
		/// </summary>
		public MemberRef GetTypeFromHandle { get; }

		/// <summary>
		/// A reference to <see cref="MethodBase.GetMethodFromHandle(RuntimeMethodHandle)"/>
		/// </summary>
		public MemberRef GetMethodFromHandle { get; }

		/// <summary>
		/// A reference to <see cref="ConstructorInfo"/>.
		/// </summary>
		public ITypeDefOrRef ConstructorInfoReference { get; }

		/// <summary>
		/// The signature of <see cref="ConstructorInfo"/>
		/// </summary>
		public TypeSig ConstructorInfoSig { get; }

		/// <summary>
		/// The signature of <see cref="ConstructorInfo"/>[]
		/// </summary>
		public TypeSig ConstructorInfoArraySig { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetConstructors(BindingFlags)"/>
		/// </summary>
		public MemberRef GetConstructors { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetMethod(string, BindingFlags, Binder, Type[], ParameterModifier[])"/>
		/// </summary>
		public MemberRef GetMethod { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetProperty(string, BindingFlags)"/>
		/// </summary>
		public MemberRef GetProperty { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetConstructor(BindingFlags, Binder, Type[], ParameterModifier[])"/>
		/// </summary>
		public MemberRef GetConstructor { get; }

		/// <summary>
		/// A reference to the getter of <see cref="MethodBase.Attributes"/>.
		/// </summary>
		public MemberRef MethodBase_get_Attributes { get; }

		/// <summary>
		/// A reference to the getter of <see cref="Type.FullName"/>
		/// </summary>
		public MemberRef Type_get_FullName { get; }

		/// <summary>
		/// A reference to the getter of <see cref="Type.IsAbstract"/>
		/// </summary>
		public MemberRef Type_get_IsAbstract { get; }

		/// <summary>
		/// A reference to the getter of <see cref="Type.IsSealed"/>
		/// </summary>
		public MemberRef Type_get_IsSealed { get; }

		/// <summary>
		/// A reference to the equality operator of <see cref="Type"/>
		/// </summary>
		public MemberRef TypesEqual { get; }

		/// <summary>
		/// A reference to the inequality operator of <see cref="Type"/>
		/// </summary>
		public MemberRef TypesNotEqual { get; }

		/// <summary>
		/// A reference to the equality operator of <see cref="MethodBase"/>
		/// </summary>
		public MemberRef MethodBasesEqual { get; }

		/// <summary>
		/// A reference to the inequality operator of <see cref="MethodBase"/>
		/// </summary>
		public MemberRef MethodBasesNotEqual { get; }
		/// <summary>
		/// A reference to the equality operator of <see cref="MethodInfo"/>
		/// </summary>
		public MemberRef MethodInfosEqual { get; }

		/// <summary>
		/// A reference to the inequality operator of <see cref="MethodInfo"/>
		/// </summary>
		public MemberRef MethodInfosNotEqual { get; }

		/// <summary>
		/// A reference to the equality operator of <see cref="ConstructorInfo"/>
		/// </summary>
		public MemberRef ConstructorInfosEqual { get; }

		/// <summary>
		/// A reference to the inequality operator of <see cref="ConstructorInfo"/>
		/// </summary>
		public MemberRef ConstructorInfosNotEqual { get; }

		#endregion

		#region Object Members and String Members

		/// <summary>
		/// The <see cref="object.ToString"/> method.
		/// </summary>
		public MemberRef ToStringRef { get; }

		/// <summary>
		/// The variants of string.concat that accept 1 to 4 <see cref="object"/> arguments (as indices [0] to [3]). Index [4] is the one that accepts an array of objects.
		/// </summary>
		public MemberRef[] StringConcatObjects { get; }

		/// <summary>
		/// The variants of string.concat that accept 1 to 4 <see cref="string"/> arguments (as indices [0] to [3]). Index [4] is the one that accepts an array of objects.
		/// </summary>
		public MemberRef[] StringConcatStrings { get; }

		public TypeSig StringArray { get; }

		public TypeSig ObjectArray { get; }

		#endregion


		#endregion

		#region BepInEx and Unity

		/// <summary>
		/// A reference to the type of <see cref="Hook"/>.
		/// </summary>
		public ITypeDefOrRef HookReference { get; }

		/// <summary>
		/// The signature of <see cref="Hook"/>.
		/// </summary>
		public TypeSig HookSig { get; }

		/// <summary>
		/// A reference to <see langword="new"/> <see cref="Hook(MethodBase, MethodInfo)"/>
		/// </summary>
		public MemberRef HookCtor { get; }

		/// <summary>
		/// A reference to <see cref="UnityEngine.Debug.Log(object)"/>
		/// </summary>
		public MemberRef UnityDebugLog { get; }

		#endregion


		public SharedTypes(ExtensiblesGenerator main) {
			#region Local Methods
			void GetEqualityMethod(ITypeDefOrRef type, TypeSig sig, out MemberRef eq, out MemberRef neq) {
				MethodSig equality = MethodSig.CreateStatic(main.CorLibTypeSig<bool>(), sig, sig);
				eq = new MemberRefUser(main.Extensibles, "op_Equality", equality, type);
				neq = new MemberRefUser(main.Extensibles, "op_Inequality", equality, type);
			}
			#endregion

			WeakReference = main.Cache.Import(typeof(WeakReference<>));
			WeakReferenceSig = WeakReference.ToTypeSig();
			WeakReferenceCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig<Void>(), CommonGenericArgs.TYPE_ARG_0);
			WeakRefTryGetTargetSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.REF_TYPE_ARG_0);

			main.Cache.ImportForReferenceAndSignature(typeof(ConditionalWeakTable<,>), out ITypeDefOrRef cwtRef, out TypeSig cwtSig);
			CWTReference = cwtRef;
			CWTSig = cwtSig;
			CWTCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig<Void>());
			CWTAddSig = MethodSig.CreateInstance(main.CorLibTypeSig<Void>(), CommonGenericArgs.TYPE_ARG_0, CommonGenericArgs.TYPE_ARG_1);
			CWTRemoveSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.TYPE_ARG_0);
			CWTTryGetValueSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.TYPE_ARG_0, CommonGenericArgs.REF_TYPE_ARG_1);

			main.Cache.ImportForReferenceAndSignature(typeof(HashSet<>), out ITypeDefOrRef hashSetRef, out TypeSig hashSetSig);
			HashSetReference = hashSetRef;
			HashSetSig = hashSetSig;
			HashSetCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig<Void>());
			HashSetAddSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.TYPE_ARG_0);
			HashSetRemoveSig = HashSetAddSig.Clone(); // Remove and Contains both have the same exact signature as Add so just clone them instead of rewriting this.
			HashSetContainsSig = HashSetAddSig.Clone();
			HashSetStringInstanceSig = new GenericInstSig(HashSetSig.ToClassOrValueTypeSig(), main.CorLibTypeSig<string>());
			HashSetStringInstanceReference = HashSetStringInstanceSig.ToTypeDefOrRef();
			HashSetStringAdd = new MemberRefUser(main.Extensibles, "Add", HashSetAddSig, HashSetStringInstanceReference);
			HashSetStringContains = new MemberRefUser(main.Extensibles, "Contains", HashSetContainsSig, HashSetStringInstanceReference);

			main.Cache.ImportForReferenceAndSignature(typeof(BindingFlags), out ITypeDefOrRef bindingFlagsRef, out TypeSig bindingFlagsSig);
			BindingFlagsReference = bindingFlagsRef;
			BindingFlagsSig = bindingFlagsSig;

			main.Cache.ImportForReferenceAndSignature(typeof(Type), out ITypeDefOrRef typeReference, out TypeSig typeSignature);
			TypeReference = typeReference;
			TypeSig = typeSignature;
			TypeArraySig = main.Cache.ImportAsTypeSig(typeof(Type[]));

			main.Cache.ImportForReferenceAndSignature(typeof(MethodInfo), out ITypeDefOrRef methodInfoReference, out TypeSig methodInfoSig);
			MethodInfoReference = methodInfoReference;
			MethodInfoSig = methodInfoSig;

			main.Cache.ImportForReferenceAndSignature(typeof(MethodBase), out ITypeDefOrRef methodBaseReference, out TypeSig methodBaseSig);
			MethodBaseReference = methodBaseReference;
			MethodBaseSig = methodBaseSig;
			GetTypeFromHandle = new MemberRefUser(main.Extensibles, "GetTypeFromhandle", MethodSig.CreateStatic(TypeSig, main.Cache.ImportAsTypeSig(typeof(RuntimeTypeHandle))), TypeReference);
			GetMethodFromHandle = new MemberRefUser(main.Extensibles, "GetMethodFromHandle", MethodSig.CreateStatic(MethodBaseSig, main.Cache.ImportAsTypeSig(typeof(RuntimeMethodHandle))), MethodBaseReference);

			main.Cache.ImportForReferenceAndSignature(typeof(ConstructorInfo), out ITypeDefOrRef ctorInfoRef, out TypeSig ctorInfoSig);
			ConstructorInfoReference = ctorInfoRef;
			ConstructorInfoSig = ctorInfoSig;
			ConstructorInfoArraySig = main.Cache.ImportAsTypeSig(typeof(ConstructorInfo[]));
			GetConstructors = new MemberRefUser(main.Extensibles, "GetConstructors", MethodSig.CreateInstance(main.Cache.ImportAsTypeSig(typeof(ConstructorInfo[])), BindingFlagsSig), TypeReference);

			MethodBase_get_Attributes = new MemberRefUser(main.Extensibles, "get_Attributes", MethodSig.CreateInstance(main.Cache.ImportAsTypeSig(typeof(System.Reflection.MethodAttributes))), MethodBaseReference);
			Type_get_FullName = new MemberRefUser(main.Extensibles, "get_FullName", MethodSig.CreateInstance(main.CorLibTypeSig<string>()), TypeReference);
			Type_get_IsAbstract = new MemberRefUser(main.Extensibles, "get_IsAbstract", MethodSig.CreateInstance(main.CorLibTypeSig<bool>()), TypeReference);
			Type_get_IsSealed = new MemberRefUser(main.Extensibles, "get_IsSealed", MethodSig.CreateInstance(main.CorLibTypeSig<bool>()), TypeReference);

			GetEqualityMethod(TypeReference, TypeSig, out MemberRef typesEqual, out MemberRef typesNotEqual);
			TypesEqual = typesEqual;
			TypesNotEqual = typesNotEqual;

			GetEqualityMethod(MethodBaseReference, MethodBaseSig, out MemberRef methodBasesEqual, out MemberRef methodBasesNotEqual);
			MethodBasesEqual = methodBasesEqual;
			MethodBasesNotEqual = methodBasesNotEqual;

			GetEqualityMethod(MethodInfoReference, MethodInfoSig, out MemberRef methodInfosEqual, out MemberRef methodInfosNotEqual);
			MethodInfosEqual = methodInfosEqual;
			MethodInfosNotEqual = methodInfosNotEqual;

			GetEqualityMethod(ConstructorInfoReference, ConstructorInfoSig, out MemberRef constructorInfosEqual, out MemberRef constructorInfosNotEqual);
			ConstructorInfosEqual = constructorInfosEqual;
			ConstructorInfosNotEqual = constructorInfosNotEqual;

			TypeSig binder = main.Cache.ImportAsTypeSig(typeof(Binder));
			TypeSig paramModArray = main.Cache.ImportAsTypeSig(typeof(ParameterModifier[]));

			GetMethod = new MemberRefUser(main.Extensibles, "GetMethod", MethodSig.CreateInstance(
				// string, BindingFlags, Binder, Type[], ParameterModifier[]
				MethodInfoSig,
				main.CorLibTypeSig<string>(),
				BindingFlagsSig,
				binder,
				TypeArraySig,
				paramModArray
			), TypeReference);

			GetProperty = new MemberRefUser(main.Extensibles, "GetProperty", MethodSig.CreateInstance(
				PropertyInfoSig
			), TypeReference)

			GetConstructor = new MemberRefUser(main.Extensibles, "GetConstructor", MethodSig.CreateInstance(
				// BindingFlags, Binder, Type[], ParameterModifier[]
				ConstructorInfoSig,
				binder,
				TypeArraySig,
				paramModArray
			), TypeReference);

			ExceptionStringCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig<Void>(), main.CorLibTypeSig<string>());
			InvalidOperationExceptionType = main.Cache.Import(typeof(InvalidOperationException));
			InvalidOperationExceptionCtor = new	MemberRefUser(main.Extensibles, ".ctor", ExceptionStringCtorSig, InvalidOperationExceptionType);

			MissingMemberExceptionType = main.Cache.Import(typeof(MissingMemberException));

			ToStringRef = new MemberRefUser(main.Extensibles, "ToString", MethodSig.CreateInstance(main.CorLibTypeSig<Void>()), main.CorLibTypeRef<object>());
			StringArray = main.Cache.ImportAsTypeSig(typeof(string[]));
			ObjectArray = main.Cache.ImportAsTypeSig(typeof(object[]));

			UnityDebugLog = new MemberRefUser(main.Extensibles, "Log", MethodSig.CreateStatic(main.CorLibTypeSig<Void>(), main.CorLibTypeSig<object>()), main.Cache.Import(typeof(UnityEngine.Debug)));

			main.Cache.ImportForReferenceAndSignature(typeof(Hook), out ITypeDefOrRef hookReference, out TypeSig hookSig);
			HookReference = hookReference;
			HookSig = hookSig;
			HookCtor = new MemberRefUser(main.Extensibles, ".ctor", MethodSig.CreateInstance(main.CorLibTypeSig<Void>(), MethodBaseSig, MethodInfoSig), HookReference);

			List<TypeSig> currentTypesStr = new List<TypeSig>(5);
			List<TypeSig> currentTypesObj = new List<TypeSig>(5);
			StringConcatObjects = new MemberRef[5];
			StringConcatStrings = new MemberRef[5];
			for (int i = 0; i < 5; i++) {
				if (i == 4) {
					MemberRefUser objArray = new MemberRefUser(main.Extensibles, "Concat", MethodSig.CreateStatic(main.CorLibTypeSig<string>(), ObjectArray), main.CorLibTypeRef<string>());
					MemberRefUser strArray = new MemberRefUser(main.Extensibles, "Concat", MethodSig.CreateStatic(main.CorLibTypeSig<string>(), StringArray), main.CorLibTypeRef<string>());
					StringConcatObjects[i] = objArray;
					StringConcatStrings[i] = strArray;
				} else {
					currentTypesStr.Add(main.CorLibTypeSig<string>());
					currentTypesObj.Add(main.CorLibTypeSig<object>());
					StringConcatObjects[i] = new MemberRefUser(main.Extensibles, "Concat", MethodSig.CreateStatic(main.CorLibTypeSig<string>(), currentTypesObj.ToArray()), main.CorLibTypeRef<string>());
					StringConcatStrings[i] = new MemberRefUser(main.Extensibles, "Concat", MethodSig.CreateStatic(main.CorLibTypeSig<string>(), currentTypesStr.ToArray()), main.CorLibTypeRef<string>());
				}
			}
		}

	}
}
