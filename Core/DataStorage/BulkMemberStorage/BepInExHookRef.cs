﻿using dnlib.DotNet;
using HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff;
using HookGenExtender.Core.ReferenceHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage.BulkMemberStorage {

	/// <summary>
	/// Stores information about, and closely related to, a BepInEx hook. To get this, use <see cref="BepInExTools"/>
	/// </summary>
	public readonly struct BepInExHookRef {
		
		/// <summary>
		/// Whether or not this is a custom hook declared by Extensibles. This returns if <see cref="originalEvent"/> is <see langword="null"/>.
		/// </summary>
		public bool IsCustomHook => originalEvent == null;

		/// <summary>
		/// A reference to the original method in the game that the hook corresponds to.
		/// </summary>
		public readonly MethodDefAndRef originalGameMethod;

		/// <summary>
		/// The type that the extensible counterpart exists in.
		/// </summary>
		public readonly ExtensibleTypeData type;

		/// <summary>
		/// The signature of the original method that the hook points to. This includes all imported types.
		/// </summary>
		public readonly MethodSig importedMethodSignature;

		/// <summary>
		/// The signature of orig_ method that is used by subscribers.
		/// <para/>
		/// This references <see cref="IDelegateTypeWrapper.DelegateSignature"/> of <see cref="origDelegateType"/>.
		/// </summary>
		public readonly MethodSig origDelegateMethodSignature;

		/// <summary>
		/// The delegate type information, which includes its signature and all methods.
		/// </summary>
		public readonly IDelegateTypeWrapper origDelegateType;

		/// <summary>
		/// The signature of hook_ method that is used by the event. This will be <see langword="null"/> on custom types.
		/// For the binder, consider using <see cref="hookBinderMethodSignature"/>.
		/// <para/>
		/// This references <see cref="IDelegateTypeWrapper.DelegateSignature"/> of <see cref="hookDelegateType"/>.
		/// </summary>
		public readonly MethodSig hookDelegateMethodSignature;

		/// <summary>
		/// The method signature for the method in the Binder type that is subscribed to the BIE hook.
		/// </summary>
		public readonly MethodSig hookBinderMethodSignature;

		/// <summary>
		/// The delegate type information for the internal <c>hook_*</c> event type, which is what drives the event that can be subscribed to.
		/// <para/>
		/// <strong>This value may be null if this is a custom hook generated by Extensibles, which has no corresponding event.</strong>
		/// </summary>
		public readonly IDelegateTypeWrapper hookDelegateType;

		/// <summary>
		/// The event for the hook. Subscribing to this event creates a hook.
		/// <para/>
		/// <strong>This value may be null if this is a custom hook generated by Extensibles, which has no corresponding event.</strong>
		/// </summary>
		public readonly EventDef originalEvent;

		/// <summary>
		/// The original declaration of, and the imported reference to, <see cref="originalEvent"/>'s <see langword="add"/> method.
		/// <para/>
		/// <strong>This value may be null if this is a custom hook generated by Extensibles, which has no corresponding event.</strong>
		/// </summary>
		public readonly MethodDefAndRef importedEventAdd;

		/// <summary>
		/// The original declaration of, and the imported reference to, <see cref="originalEvent"/>'s <see langword="remove"/> method.
		/// <para/>
		/// <strong>This value may be null if this is a custom hook generated by Extensibles, which has no corresponding event.</strong>
		/// </summary>
		public readonly MethodDefAndRef importedEventRemove;

		/// <summary>
		/// Create a hook reference for a real BIE hook.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="originalGameMethod"></param>
		/// <param name="origDelegateType"></param>
		/// <param name="hookDelegateType"></param>
		/// <param name="importedMethodSignature"></param>
		/// <param name="originalEvent"></param>
		/// <param name="importedEventAdd"></param>
		/// <param name="importedEventRemove"></param>
		public BepInExHookRef(ExtensibleTypeData type, MethodDefAndRef originalGameMethod, IDelegateTypeWrapper origDelegateType, IDelegateTypeWrapper hookDelegateType, MethodSig importedMethodSignature, EventDef originalEvent, MethodDefAndRef importedEventAdd, MethodDefAndRef importedEventRemove) {
			this.type = type;
			this.originalGameMethod = originalGameMethod;
			this.origDelegateType = origDelegateType;
			this.origDelegateMethodSignature = origDelegateType.DelegateSignature;
			this.hookDelegateType = hookDelegateType;
			this.hookDelegateMethodSignature = hookDelegateType?.DelegateSignature;
			this.importedMethodSignature = importedMethodSignature;
			this.originalEvent = originalEvent;
			this.importedEventAdd = importedEventAdd;
			this.importedEventRemove = importedEventRemove;

			// The only difference is that it's static.
			this.hookBinderMethodSignature = this.hookDelegateMethodSignature.Clone();
			hookBinderMethodSignature.HasThis = false;
		}

		/// <summary>
		/// Create a hook reference for a custom hook.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="originalGameMethod"></param>
		/// <param name="origDelegateType"></param>
		/// <param name="binderHookMethodSignature"></param>
		/// <param name="importedOriginalMethodSignature"></param>
		public BepInExHookRef(ExtensibleTypeData type, MethodDefAndRef originalGameMethod, IDelegateTypeWrapper origDelegateType, MethodSig binderHookMethodSignature, MethodSig importedOriginalMethodSignature) {
			this.type = type;
			this.originalGameMethod = originalGameMethod;
			this.origDelegateType = origDelegateType;
			this.origDelegateMethodSignature = origDelegateType.DelegateSignature;
			this.hookDelegateType = null;
			this.hookDelegateMethodSignature = null;
			this.importedMethodSignature = null;
			this.originalEvent = null;
			this.importedEventAdd = null;
			this.importedEventRemove = null;
			this.hookBinderMethodSignature = binderHookMethodSignature.Clone();
			hookBinderMethodSignature.HasThis = false;
		}
	}
}
