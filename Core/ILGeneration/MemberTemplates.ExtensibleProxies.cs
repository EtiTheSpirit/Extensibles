using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using HookGenExtender.Core.DataStorage.ExtremelySpecific.DelegateStuff;
using HookGenExtender.Core.Utils.MemberMutation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		#region Methods (Extensible Class)

		/// <summary>
		/// <strong>Step 4.3 of the Extensible Type Pipeline:</strong><br/>
		/// Writes all required members for, as well as the body of, an extensible method proxy.
		/// This is the member provided in the extensible type that implementors can override to automatically hook.
		/// <para/>
		/// This automatically registers the new members.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="originalGameMethod">The original method in the original type. This is used as a template to copy.</param>
		/// <param name="hook">The BIE hook information for this method, which is used to create the proxy.</param>
		public static ProxyAndHookPackage MakeExtensibleMethodProxy(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in BepInExHookRef hook) {
			return new ProxyAndHookPackage(CommonMakeExtensibleProxy(
				main,
				in coreMembers,
				hook.originalGameMethod,
				hook.origDelegateType
			), hook);
		}

		#endregion

		#region Properties (Extensible Class)

		/// <summary>
		/// <strong>Step 4.2 of the Extensible Type Pipeline:</strong><br/>
		/// Writes all required members for, as well as the bodies of, an extensible property's getter and/or setter proxy.
		/// This is the member provided in the extensible type that implementors can override to automatically hook.
		/// <para/>
		/// This automatically registers the new members.
		/// <para/>
		/// Unlike its method counterpart, this <em>outputs</em> two <see cref="BepInExHookRef"/>s that can be used in the binder. Pay attention to their nullability.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		public static ProxyAndHookPackage MakeExtensiblePropertyProxies(ExtensiblesGenerator main, PropertyDefAndRef originalGameProperty, in ExtensibleCoreMembers coreMembers) {
			// This one takes more effort than the other because I also have to invent the hook type itself.
			ExtensibleMethodProxyMembers? getter = null;
			ExtensibleMethodProxyMembers? setter = null;
			BepInExHookRef? getterHook = null;
			BepInExHookRef? setterHook = null;
			if (originalGameProperty.Getter != null) {
				getter = MakeExtensiblePropertyAccessorProxy(main, originalGameProperty.Getter, in coreMembers, out BepInExHookRef getterHookV);
				getter.Value.proxyMethod.Definition.Attributes |= MethodAttributes.SpecialName | MethodAttributes.HideBySig;
				getterHook = getterHookV;
			}
			if (originalGameProperty.Setter != null) {
				setter = MakeExtensiblePropertyAccessorProxy(main, originalGameProperty.Setter, in coreMembers, out BepInExHookRef setterHookV);
				setter.Value.proxyMethod.Definition.Attributes |= MethodAttributes.SpecialName | MethodAttributes.HideBySig;
				setterHook = setterHookV;
			}

			PropertyDefAndRef proxyProperty = new PropertyDefAndRef(
				main.Extensibles,
				originalGameProperty.Definition.Name,
				new PropertySig(true, main.Cache.Import(originalGameProperty.Definition.PropertySig.RetType)),
				getter?.proxyMethod,
				setter?.proxyMethod
			);
			coreMembers.type.ExtensibleType.AddProperty(proxyProperty);

			return new ProxyAndHookPackage(in getter, in setter, in getterHook, in setterHook, proxyProperty);
		}

		/// <summary>
		/// Common routine to make a property proxy. This is used by step 4.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="accessor"></param>
		/// <param name="coreMembers"></param>
		/// <param name="hook"></param>
		/// <returns></returns>
		private static ExtensibleMethodProxyMembers MakeExtensiblePropertyAccessorProxy(ExtensiblesGenerator main, MethodDefAndRef accessor, in ExtensibleCoreMembers coreMembers, out BepInExHookRef hook) {
			#region Create Delegate Type
			MethodSig importedGameSig = accessor.Definition.MethodSig.CloneAndImport(main);
			MethodSig origDelegateSig = importedGameSig.Clone();
			origDelegateSig.Params.Insert(0, coreMembers.type.ImportedGameTypeSig);
			// Add the game type as arg 0.
			// This corresponds to the "self" parameter seen in BIE hooks.

			// Now make the type. This is auto-registered.
			DelegateTypeDefAndRef origDelegateType = ILTools.CreateDelegateType(main, origDelegateSig, $"orig_{accessor.Name}");

			// Now make the signature for the hook event. Even though we don't have one, this is needed for generated the binder method.
			MethodSig hookDelegateSig = origDelegateSig.Clone();
			hookDelegateSig.Params.Insert(0, origDelegateType.Signature);
			#endregion

			// Register the new delegate type.
			origDelegateType.CachedDelegateType.Underlying.DeclaringType2 = coreMembers.type.ExtensibleType.Underlying;

			// Now create the hook reference.
			hook = new BepInExHookRef(
				coreMembers.type,
				accessor,
				origDelegateType,
				hookDelegateSig,
				importedGameSig
			);
			return CommonMakeExtensibleProxy(
				main,
				in coreMembers,
				accessor,
				origDelegateType
			);
		}

		#endregion

		#region Fields (Extensible Class)

		/// <summary>
		/// <strong>Step 4.1 of the Extensible Type Pipeline:</strong><br/>
		/// Creates a proxy to a field in the extensible type. Proxies are created as ref properties for readable and writable fields.
		/// Readonly fields are created as standard get-only properties.
		/// <para/>
		/// This automatically registers the property.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="original"></param>
		public static PropertyDefAndRef MakeExtensibleFieldProxy(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, ExtensibleTypeData type, FieldDefAndRef original) {
			bool isReadOnly = original.Definition.IsInitOnly;

			TypeSig propertyType = main.Cache.Import(original.Definition.FieldType);
			if (!isReadOnly) propertyType = new ByRefSig(propertyType);

			PropertyDefAndRef prop = new PropertyDefAndRef(
				main,
				original.Definition.Name,
				new PropertySig(true, propertyType),
				type.ExtensibleType.Reference,
				getterAttributes: MethodAttributes.Public,
				setterAttributes: null
			);

			CilBody getter = prop.Getter.GetOrCreateBody();
			getter.EmitThis();
			getter.EmitCall(coreMembers.originalObjectProxy.Getter.Reference);
			getter.Emit(isReadOnly ? OpCodes.Ldfld : OpCodes.Ldflda, original.Reference);
			getter.EmitRet();
			getter.FinalizeMethodBody(main);

			coreMembers.type.ExtensibleType.AddProperty(prop);

			return prop;
		}

		#endregion

		/// <summary>
		/// Code that is common across the methods needed to write method proxies and property proxies, which fundamentally have the same body.
		/// This is used by Step 4.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="originalGameMethod"></param>
		/// <param name="origDelegateType"></param>
		/// <returns></returns>
		private static ExtensibleMethodProxyMembers CommonMakeExtensibleProxy(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, MethodDefAndRef originalGameMethod, IDelegateTypeWrapper origDelegateType) {
			MethodDefAndRef proxyMethod = originalGameMethod.Definition.CloneMethodDeclarationFromGame(main, coreMembers);
			proxyMethod.Definition.Attributes |= MethodAttributes.Virtual;
			proxyMethod.Definition.Attributes &= ~MethodAttributes.Private;
			proxyMethod.Definition.Attributes |= MethodAttributes.Public;

			FieldDefAndRef isCallerInInvocation = new FieldDefAndRef(main, $"<{originalGameMethod.Name}>isCallerInInvocation", new FieldSig(main.CorLibTypeSig<bool>()), coreMembers.type.ExtensibleType.Reference, CommonAttributes.SPECIAL_LOCKED_FIELD);
			FieldDefAndRef origDelegateRef = new FieldDefAndRef(main, $"<{originalGameMethod.Name}>origDelegateRef", new FieldSig(origDelegateType.Signature), coreMembers.type.ExtensibleType.Reference, CommonAttributes.SPECIAL_LOCKED_FIELD);

			// GOAL:
			/*
			orig_Whatever del = this.<Whatever>orig_Whatever;
			bool isManualCall = del == null;
			if (isManualCall && !this.<Whatever>isCallerInInvocation) {
				this.<Whatever>isCallerInInvocation = true;
				var result = Original.Whatever();
				this.<Whatever>isCallerInInvocation = false;
				return result;
			}
			if (isManualCall) {
				throw new InvalidOperationException("Already in delegate, illegal state, yada yada");
			}
			return del(Original);
			*/

			int numGameParams = originalGameMethod.Definition.MethodSig.GetParamCount();
			CilBody proxyBody = proxyMethod.GetOrCreateBody();
			Local isInManualCall = new Local(main.CorLibTypeSig<bool>(), "isManualCall");
			proxyBody.SetLocals(isInManualCall);

			Instruction throwMissingDelegateException = proxyBody.NewBrDest();
			Instruction callOrig_Destination = proxyBody.NewBrDest();

			// if (del == null) {
			proxyBody.EmitThis();
			proxyBody.Emit(OpCodes.Ldfld, origDelegateRef.Definition);
			proxyBody.EmitNull();
			proxyBody.Emit(OpCodes.Ceq);
			proxyBody.EmitStoreThenLoad(isInManualCall);
			proxyBody.Emit(OpCodes.Brfalse, callOrig_Destination);
			// above would go to validateDelegateIntegrity_Destination, but it just does another branch with the same result.
			///////////////////////////////

			// if (!isCallerInInvocation) {
			proxyBody.EmitThis();
			proxyBody.Emit(OpCodes.Ldfld, isCallerInInvocation.Definition);
			proxyBody.Emit(OpCodes.Brtrue, throwMissingDelegateException);
			///////////////////////////////

			proxyBody.EmitThis();
			proxyBody.EmitValue(true);
			proxyBody.Emit(OpCodes.Stfld, isCallerInInvocation.Definition);                // isCallerInInvocation = true

			proxyBody.EmitThis();
			proxyBody.EmitCallvirt(coreMembers.originalObjectProxy.Definition); // this.Original...
			proxyBody.EmitAmountOfArgs(numGameParams, 1, false);			// All args of method
			proxyBody.Emit(OpCodes.Callvirt, originalGameMethod.Reference);		// ... .Method()

			proxyBody.EmitThis();
			proxyBody.EmitValue(false);
			proxyBody.Emit(OpCodes.Stfld, isCallerInInvocation.Definition);				// isCallerInInvocation = false

			proxyBody.EmitRet();
			// }
			// }
			proxyBody.Emit(throwMissingDelegateException);
			proxyBody.EmitInvalidOpException(main, $"Illegal state detected. Something called {coreMembers.type}::{originalGameMethod.Name} in an invalid state (this method is already in the process of being called, and was called again by something other than BepInEx's hook system). Did you attempt to run this method in a multi-threaded environment?");
			// }
			proxyBody.Emit(callOrig_Destination);

			proxyBody.EmitThis();
			proxyBody.Emit(OpCodes.Ldfld, origDelegateRef.Definition);                                  // orig
			proxyBody.EmitThis();
			proxyBody.EmitCallvirt(coreMembers.originalObjectProxy.Definition);   // this.Original (arg 0)
			proxyBody.EmitAmountOfArgs(numGameParams, 1, false);							// All args of method (arg 1, ...)
			proxyBody.EmitCall(origDelegateType.Invoke);									// Call orig(self, ...)
			proxyBody.EmitRet();
			proxyBody.FinalizeMethodBody(main);

			coreMembers.type.ExtensibleType.AddMethod(proxyMethod);
			coreMembers.type.ExtensibleType.AddField(isCallerInInvocation);
			coreMembers.type.ExtensibleType.AddField(origDelegateRef);

			return new ExtensibleMethodProxyMembers(coreMembers.type, proxyMethod, isCallerInInvocation, origDelegateRef, origDelegateType);
		}

	}
}
