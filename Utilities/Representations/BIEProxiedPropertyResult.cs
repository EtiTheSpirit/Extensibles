using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Representations {
	public readonly ref struct BIEProxiedPropertyResult {

		/// <summary>
		/// The delegate of the getter hook, or <see langword="null"/> if the property has no getter.
		/// </summary>
		public readonly TypeDefUser getterDelegate;

		/// <summary>
		/// The delegate of the setter hook, or <see langword="null"/> if the property has no setter.
		/// </summary>
		public readonly TypeDefUser setterDelegate;

		/// <summary>
		/// The proxy method that serves as the default implementation of <see langword="get"/>, or <see langword="null"/> if the property has no getter.
		/// </summary>
		public readonly MethodDefUser getProxy;

		/// <summary>
		/// The proxy method that serves as the default implementation of <see langword="set"/>, or <see langword="null"/> if the property has no setter.
		/// </summary>
		public readonly MethodDefUser setProxy;

		/// <summary>
		/// The method that gets used in the custom hook to the original property's getter, or <see langword="null"/> if the property has no getter.
		/// </summary>
		public readonly MethodDefUser getHook;

		/// <summary>
		/// The method that gets used in the custom hook to the original property's setter, or <see langword="null"/> if the property has no setter.
		/// </summary>
		public readonly MethodDefUser setHook;

		/// <summary>
		/// A value that stores whether or not the getter is currently in manual invocation, or <see langword="null"/> if the property has no getter.
		/// </summary>
		public readonly FieldDefUser isGetterInInvocation;

		/// <summary>
		/// A value that stores whether or not the setter is currently in manual invocation, or <see langword="null"/> if the property has no setter.
		/// </summary>
		public readonly FieldDefUser isSetterInInvocation;

		/// <summary>
		/// A value that stores the <c>orig</c> callback of the getter, or <see langword="null"/> if the property has no getter.
		/// </summary>
		public readonly FieldDefUser getterOriginalCallback;

		/// <summary>
		/// A value that stores the <c>orig</c> callback of the setter, or <see langword="null"/> if the property has no setter.
		/// </summary>
		public readonly FieldDefUser setterOriginalCallback;


		public BIEProxiedPropertyResult(TypeDefUser getterDelegate, TypeDefUser setterDelegate, MethodDefUser getProxy, MethodDefUser setProxy, MethodDefUser getHook, MethodDefUser setHook, FieldDefUser isGetterInInvocation, FieldDefUser isSetterInInvocation, FieldDefUser getterOriginalCallback, FieldDefUser setterOriginalCallback) {
			this.getterDelegate = getterDelegate;
			this.setterDelegate = setterDelegate;
			this.getProxy = getProxy;
			this.setProxy = setProxy;
			this.getHook = getHook;
			this.setHook = setHook;
			this.isGetterInInvocation = isGetterInInvocation;
			this.isSetterInInvocation = isSetterInInvocation;
			this.getterOriginalCallback = getterOriginalCallback;
			this.setterOriginalCallback = setterOriginalCallback;
		}

	}
}
