using dnlib.DotNet;
using HookGenExtender.Utilities.ILGeneratorParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Shared {
	public sealed class SharedGeneralSignatures {

		public ReusableSignatureContainer Common { get; }

		/// <summary>
		/// Signature of <see langword="new"/> <see cref="InvalidOperationException(string)"/>
		/// </summary>
		public MethodSig InvalidOperationExceptionCtorSig { get; }

		/// <summary>
		/// Signature of <see cref="object.GetType"/>
		/// </summary>
		public MethodSig GetTypeSig { get; }

		/// <summary>
		/// Reference to <see langword="new"/> <see cref="InvalidOperationException(string)"/>
		/// </summary>
		public MemberRefUser InvalidOperationExceptionCtor { get; }

		/// <summary>
		/// Reference to <see cref="object.GetType"/>
		/// </summary>
		public MemberRefUser GetTypeMtd { get; }

		/// <summary>
		/// Signature of <see cref="ConditionalWeakTable{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
		/// </summary>
		public MethodSig CWTTryGetValueSig { get; }

		/// <summary>
		/// Signature of <see cref="ConditionalWeakTable{TKey, TValue}.Remove(TKey)"/>
		/// </summary>
		public MethodSig CWTRemoveSig { get; }

		/// <summary>
		/// Signature of <see cref="ConditionalWeakTable{TKey, TValue}.Add(TKey, TValue)"/>
		/// </summary>
		public MethodSig CWTAddSig { get; }

		/// <summary>
		/// Signature of <see langword="new"/> <see cref="WeakReference{T}(T)"/>
		/// </summary>
		public MethodSig WeakRefCtorSig { get; }

		/// <summary>
		/// Signature of <see cref="WeakReference{T}.SetTarget(T)"/> 
		/// </summary>
		public MethodSig WeakRefSetTargetSig { get; }

		/// <summary>
		/// Generates a reference to <see cref="ConditionalWeakTable{TKey, TValue}.TryGetValue(TKey, out TValue)"/> for a specific generic instance of <see cref="ConditionalWeakTable{TKey, TValue}"/>.
		/// </summary>
		/// <param name="inCWTType"></param>
		/// <returns></returns>
		public MemberRefUser ReferenceCWTTryGetValue(IMemberRefParent inCWTType) {
			return new MemberRefUser(Common.MirrorGenerator.MirrorModule, "TryGetValue", CWTTryGetValueSig, inCWTType);
		}

		/// <summary>
		/// Generates a reference to <see cref="ConditionalWeakTable{TKey, TValue}.Remove(TKey)"/> for a specific generic instance of <see cref="ConditionalWeakTable{TKey, TValue}"/>.
		/// </summary>
		/// <param name="inCWTType"></param>
		/// <returns></returns>
		public MemberRefUser ReferenceCWTRemove(IMemberRefParent inCWTType) {
			return new MemberRefUser(Common.MirrorGenerator.MirrorModule, "Remove", CWTRemoveSig, inCWTType);
		}

		/// <summary>
		/// Generates a reference to <see cref="ConditionalWeakTable{TKey, TValue}.Add(TKey, TValue)"/> for a specific generic instance of <see cref="ConditionalWeakTable{TKey, TValue}"/>.
		/// </summary>
		/// <param name="inCWTType"></param>
		/// <returns></returns>
		public MemberRefUser ReferenceCWTAdd(IMemberRefParent inCWTType) {
			return new MemberRefUser(Common.MirrorGenerator.MirrorModule, "Add", CWTAddSig, inCWTType);
		}

		/// <summary>
		/// Generates a reference to <see cref="WeakReference{T}.SetTarget(T)"/> for a specific generic instance of <see cref="WeakReference{T}"/>.
		/// </summary>
		/// <param name="inCWTType"></param>
		/// <returns></returns>
		public MemberRefUser ReferenceWeakRefSetTarget(IMemberRefParent inWeakRefType) {
			return new MemberRefUser(Common.MirrorGenerator.MirrorModule, "SetTarget", WeakRefSetTargetSig, inWeakRefType);
		}

		/// <summary>
		/// Generates a reference to <see langword="new"/> <see cref="WeakReference{T}(T)"/> for a specific generic instance of <see cref="WeakReference{T}"/>.
		/// </summary>
		/// <param name="inWeakRefType"></param>
		/// <returns></returns>
		public MemberRefUser ReferenceWeakRefCtor(IMemberRefParent inWeakRefType) {
			return new MemberRefUser(Common.MirrorGenerator.MirrorModule, ".ctor", WeakRefCtorSig, inWeakRefType);
		}

		internal SharedGeneralSignatures(ReusableSignatureContainer common) {
			Common = common;
			MirrorGenerator mirrorGenerator = common.MirrorGenerator;
			InvalidOperationExceptionCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			InvalidOperationExceptionCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", InvalidOperationExceptionCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			GetTypeSig = MethodSig.CreateInstance(CommonMembers.typeTypeSig);
			GetTypeMtd = new MemberRefUser(mirrorGenerator.MirrorModule, "GetType", GetTypeSig, mirrorGenerator.MirrorModule.CorLibTypes.Object.ToTypeDefOrRef());

			WeakRefCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			CWTTryGetValueSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0), new ByRefSig(new GenericVar(1)));
			CWTRemoveSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0));
			CWTAddSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0), new GenericVar(1));
			WeakRefSetTargetSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
		}

	}
}
