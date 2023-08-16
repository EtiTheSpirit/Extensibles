using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
		/// A predefined generic instance of <see cref="HashSet{T}"/> where <c>T</c> is <see cref="string"/>.
		/// </summary>
		public GenericInstSig HashSetStringInstanceSig { get; }


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
		/// A reference to <see cref="Type.GetConstructors(BindingFlags)"/>
		/// </summary>
		public MemberRef GetConstructors { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetMethod(string, BindingFlags, Binder, CallingConventions, Type[], ParameterModifier[])"/>
		/// </summary>
		public MemberRef GetMethod { get; }

		/// <summary>
		/// A reference to <see cref="Type.GetConstructor(BindingFlags, Binder, Type[], ParameterModifier[])"/>
		/// </summary>
		public MemberRef GetConstructor { get; }

		#endregion

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

		#endregion

		/// <summary>
		/// A reference to <see cref="UnityEngine.Debug.Log(object)"/>
		/// </summary>
		public MemberRef UnityDebugLog { get; }

		public TypeSig StringArray { get; }

		public TypeSig ObjectArray { get; }

		public SharedTypes(ExtensiblesGenerator main) {
			WeakReference = main.Cache.Import(typeof(WeakReference<>));
			WeakReferenceSig = WeakReference.ToTypeSig();
			WeakReferenceCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig(), CommonGenericArgs.TYPE_ARG_0);
			WeakRefTryGetTargetSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.REF_TYPE_ARG_0);

			main.Cache.ImportForReferenceAndSignature(typeof(ConditionalWeakTable<,>), out ITypeDefOrRef cwtRef, out TypeSig cwtSig);
			CWTReference = cwtRef;
			CWTSig = cwtSig;
			CWTCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig());
			CWTAddSig = MethodSig.CreateInstance(main.CorLibTypeSig(), CommonGenericArgs.TYPE_ARG_0, CommonGenericArgs.TYPE_ARG_1);
			CWTRemoveSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.TYPE_ARG_0);
			CWTTryGetValueSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.TYPE_ARG_0, CommonGenericArgs.REF_TYPE_ARG_1);

			main.Cache.ImportForReferenceAndSignature(typeof(HashSet<>), out ITypeDefOrRef hashSetRef, out TypeSig hashSetSig);
			HashSetReference = hashSetRef;
			HashSetSig = hashSetSig;
			HashSetCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig());
			HashSetAddSig = MethodSig.CreateInstance(main.CorLibTypeSig<bool>(), CommonGenericArgs.TYPE_ARG_0);
			HashSetRemoveSig = HashSetAddSig.Clone(); // Remove and Contains both have the same exact signature as Add so just clone them instead of rewriting this.
			HashSetContainsSig = HashSetAddSig.Clone();
			HashSetStringInstanceSig = new GenericInstSig(HashSetSig.ToClassOrValueTypeSig(), main.CorLibTypeSig<string>());

			main.Cache.ImportForReferenceAndSignature(typeof(BindingFlags), out ITypeDefOrRef bindingFlagsRef, out TypeSig bindingFlagsSig);
			BindingFlagsReference = bindingFlagsRef;
			BindingFlagsSig = bindingFlagsSig;

			main.Cache.ImportForReferenceAndSignature(typeof(Type), out ITypeDefOrRef typeReference, out TypeSig typeSignature);
			TypeReference = typeReference;
			TypeSig = typeSignature;

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
			GetConstructors = new MemberRefUser(main.Extensibles, "GetConstructors", MethodSig.CreateStatic(main.Cache.ImportAsTypeSig(typeof(ConstructorInfo[])), BindingFlagsSig), TypeReference);

			ExceptionStringCtorSig = MethodSig.CreateInstance(main.CorLibTypeSig(), main.CorLibTypeSig<string>());
			InvalidOperationExceptionType = main.Cache.Import(typeof(InvalidOperationException));
			InvalidOperationExceptionCtor = new	MemberRefUser(main.Extensibles, ".ctor", ExceptionStringCtorSig, InvalidOperationExceptionType);

			ToStringRef = new MemberRefUser(main.Extensibles, "ToString", MethodSig.CreateInstance(main.CorLibTypeSig()), main.CorLibTypeRef<object>());
			StringArray = main.Cache.ImportAsTypeSig(typeof(string[]));
			ObjectArray = main.Cache.ImportAsTypeSig(typeof(object[]));

			UnityDebugLog = new MemberRefUser(main.Extensibles, "Log", MethodSig.CreateStatic(main.CorLibTypeSig(), main.CorLibTypeSig<object>()), main.Cache.Import(typeof(UnityEngine.Debug)));

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
