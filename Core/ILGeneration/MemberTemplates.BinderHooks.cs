using dnlib.DotNet;
using dnlib.DotNet.Emit;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BindingFlags = System.Reflection.BindingFlags;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		#region Binder Hooked Methods

		/// <summary>
		/// Declares, codes, and registers the method that the binder registers as a BIE hook. This method will call its counterpart in the extensible type.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="hook"></param>
		public static MethodDefAndRef CodeBinderMethodHook(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ProxyAndHookPackage proxyAndHookData) {
			ExtensibleMethodProxyMembers extensibleMethodProxyMembers = proxyAndHookData.MethodProxyMembers;
			BepInExHookRef hook = proxyAndHookData.MethodHookMembers;

			MethodDefAndRef boundMethod = new MethodDefAndRef(
				main.Extensibles,
				hook.originalGameMethod.Name,
				hook.hookBinderMethodSignature,
				coreMembers.type.ExtensibleType.Reference,
				CommonAttributes.SPECIAL_LOCKED_METHOD
			);

			CommonGenerateBodyOfProxyCall(boundMethod, in coreMembers, in binderMembers, in extensibleMethodProxyMembers, in hook);

			return boundMethod;
		}

		/// <summary>
		/// Declares, codes, and registers the method that the binder registers as a BIE hook. This method will call its counterpart in the extensible type.
		/// <para/>
		/// This returns (getter, setter). Either will be null if the property is missing its get method or set method respectively.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="hook"></param>
		public static (MethodDefAndRef, MethodDefAndRef) CodeBinderPropertyHooks(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, ProxyAndHookPackage proxyAndHookData) {
			MethodDefAndRef getter = null;
			MethodDefAndRef setter = null;
			if (proxyAndHookData.PropertyGetterProxyMembers != null) {
				ExtensibleMethodProxyMembers extensibleMethodProxyMembers = proxyAndHookData.PropertyGetterProxyMembers.Value;
				BepInExHookRef hook = proxyAndHookData.PropertyGetterHookMembers.Value;
				getter = new MethodDefAndRef(
					main.Extensibles,
					hook.originalGameMethod.Name,
					hook.hookBinderMethodSignature,
					coreMembers.type.ExtensibleType.Reference,
					CommonAttributes.SPECIAL_LOCKED_METHOD
				);
				CommonGenerateBodyOfProxyCall(getter, in coreMembers, in binderMembers, in extensibleMethodProxyMembers, in hook);
			}
			if (proxyAndHookData.PropertySetterProxyMembers != null) {
				ExtensibleMethodProxyMembers extensibleMethodProxyMembers = proxyAndHookData.PropertySetterProxyMembers.Value;
				BepInExHookRef hook = proxyAndHookData.PropertySetterHookMembers.Value;
				setter = new MethodDefAndRef(
					main.Extensibles,
					hook.originalGameMethod.Name,
					hook.hookBinderMethodSignature,
					coreMembers.type.ExtensibleType.Reference,
					CommonAttributes.SPECIAL_LOCKED_METHOD
				);
				CommonGenerateBodyOfProxyCall(setter, in coreMembers, in binderMembers, in extensibleMethodProxyMembers, in hook);
			}

			return (getter, setter);
		}

		private static void CommonGenerateBodyOfProxyCall(MethodDefAndRef boundMethod, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ExtensibleMethodProxyMembers extensibleMethodProxyMembers, in BepInExHookRef hook) {
			CilBody body = boundMethod.GetOrCreateBody();
			Local extensibleInstance = new Local(CommonGenericArgs.TYPE_ARG_0, "extensibleInstance");
			body.SetLocals(extensibleInstance);

			body.EmitLdarg(1);                                          // self
			body.EmitLdloc(extensibleInstance, true);               // extensibleInstance (var)
			body.EmitCallvirt(binderMembers.tryGetBindingMethod);           // => TryGetBinding(self, out extensibleInstance)
			Instruction callOrig = body.NewBrDest();
			Instruction ret = new Instruction(OpCodes.Ret);
			body.Emit(OpCodes.Brfalse, callOrig); // Skip if there is no binding.

			// From here, there is a binding. Act on it.
			// First, tell the extensible type that a BIE hook is executing:
			body.EmitLdloc(extensibleInstance);
			body.Emit(OpCodes.Stfld, extensibleMethodProxyMembers.origDelegateReference);

			// Load all applicable args and call the extensible's declared override.
			body.EmitLdloc(extensibleInstance);
			// Start from 2: [0] is orig, [1] is self, [2] and beyond is the actual args.
			body.EmitAllArgs(boundMethod.Definition.MethodSig.GetParamCount(), 2);
			body.Emit(OpCodes.Callvirt, extensibleMethodProxyMembers.proxyMethod);

			// Now remove the delegate reference.
			body.EmitNull();
			body.Emit(OpCodes.Stfld, extensibleMethodProxyMembers.origDelegateReference);

			// Jump to the return.
			body.Emit(OpCodes.Br, ret);

			////
			body.Emit(callOrig);
			// Just call orig(self) here.
			// To do this, load *everything*.
			body.EmitAllArgs(boundMethod.Definition.MethodSig.GetParamCount(), 0);
			// And now
			body.EmitCall(hook.origDelegateType.Invoke);

			body.Emit(ret);
			body.FinalizeMethodBody();
		}

		#endregion

		#region Binder CreateHooks Mutators

		/// <summary>
		/// One of the most complicated methods. This one generates the block of code that searches for, verifies, and subscribes every single user-overridden
		/// method and property in the extensible type to BepInEx.
		/// <para/>
		/// For properties, this must be called separately for its getter and its setter.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="binderHookMethod">The method generated in for the binder. This is the method that gets subscribed to the BIE event.</param>
		/// <param name="coreMembers">The common members of all Extensible types.</param>
		/// <param name="binderMembers">The common members of all Binder types.</param>
		/// <param name="proxyMembers">The members specifically designed for this method in the Extensible type.</param>
		/// <param name="hookInfo">Context about the BepInEx hook for this method, or the generated property detour in the case of getters/setters.</param>
		public static void AddMemberBindToCreateHooksMethod(ExtensiblesGenerator main, MethodDefAndRef binderHookMethod, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ExtensibleMethodProxyMembers proxyMembers, in BepInExHookRef hookInfo) {
			const BindingFlags userExtTypeMemberFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			CilBody createBindings = binderMembers.createBindingsMethod.GetOrCreateBody();
			// REMEMBER: Loc0 is reserved (typeof(TExtensible))
			// Use AppendLocals for anything new.

			ITypeDefOrRef[] hookMethodSignatureRefs = binderHookMethod.Definition.MethodSig.Params.Select(param => param.ToTypeDefOrRef()).ToArray();

			#region Alias Methods
			void Alias_EmitIfSetDoesNotContain(string methodName, Instruction gotoNext) {
				createBindings.EmitLdarg(0); // This is a HashSet<string> of members to skip.
				createBindings.Emit(OpCodes.Ldstr, methodName);
				createBindings.Emit(OpCodes.Callvirt, main.Shared.HashSetStringContains);
				createBindings.Emit(OpCodes.Brtrue, gotoNext);
			}

			void Alias_EmitIfTypeContainsProperty(string methodName, Instruction gotoNext) {
				createBindings.Emit(OpCodes.Ldloc_0);                                                               // typeof(TExtensible)
				createBindings.EmitGetMethod(main, methodName, userExtTypeMemberFlags, hookMethodSignatureRefs);    // GetMethod(...) (MethodInfo is now on stack)
				createBindings.EmitNull();
				createBindings.EmitCall(main.Shared.MethodInfosEqual);
				createBindings.Emit(OpCodes.Brfalse, gotoNext);
			}

			void Alias_EmitIfTypeContainsMethod(string methodName, Instruction gotoNext) {
				createBindings.Emit(OpCodes.Ldloc_0);                                                               // typeof(TExtensible)
				createBindings.EmitGetMethod(main, methodName, userExtTypeMemberFlags, hookMethodSignatureRefs);    // GetMethod(...) (MethodInfo is now on stack)
				createBindings.EmitNull();
				createBindings.EmitCall(main.Shared.MethodInfosEqual);
				createBindings.Emit(OpCodes.Brfalse, gotoNext);
			}
			#endregion

			Instruction jumpHereIfSetDNC = createBindings.NewBrDest();
			string method = proxyMembers.proxyMethod.Name;
			Alias_EmitIfSetDoesNotContain(method, jumpHereIfSetDNC);
			if (hookInfo.IsCustomHook) {
				// Property
				Alias_EmitIfTypeContainsProperty(method, jumpHereIfSetDNC);
				createBindings.EmitMethodof(main, (IMethod)hookInfo.originalGameMethod.Reference);          // from
				createBindings.EmitMethodof(main, (IMethod)binderHookMethod.Reference);                     // to
				createBindings.EmitNew(main.Shared.HookCtor);                                               // new Hook(from, to)
				createBindings.Emit(OpCodes.Pop);                                                           // Remove the new hook from the stack.
			} else {
				// Method
				Alias_EmitIfTypeContainsMethod(method, jumpHereIfSetDNC);
			}
			
			createBindings.Emit(jumpHereIfSetDNC);

		}


		#endregion

	}
}
