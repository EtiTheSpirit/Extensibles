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
using UnityEngine;
using BindingFlags = System.Reflection.BindingFlags;

namespace HookGenExtender.Core.ILGeneration {
	public static partial class MemberTemplates {

		#region Binder Hooked Methods

		/// <summary>
		/// Declares, codes, and registers the method that the binder registers as a BIE hook. The generated method will call its counterpart in the extensible type. This is used by step 4.3
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="hook"></param>
		public static MethodDefAndRef CodeBinderMethodHook(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ProxyAndHookPackage proxyAndHookData) {
			ExtensibleMethodProxyMembers extensibleMethodProxyMembers = proxyAndHookData.MethodProxyMembers;
			BepInExHookRef hook = proxyAndHookData.MethodHookMembers;

			MethodDefAndRef boundMethod = new MethodDefAndRef(
				main,
				hook.originalGameMethod.Name,
				hook.hookBinderMethodSignature,
				coreMembers.type.GenericBinder.Reference,
				CommonAttributes.SPECIAL_LOCKED_METHOD
			);
			boundMethod.SetParameterName(0, "orig");
			boundMethod.SetParameterName(1, "self");

			CommonGenerateBodyOfHookMethod(main, boundMethod, in coreMembers, in binderMembers, in extensibleMethodProxyMembers, in hook);

			return boundMethod;
		}

		/// <summary>
		/// Declares, codes, and registers the method that the binder registers as a BIE hook. The generated method will call its counterpart in the extensible type. This is used by step 4.2
		/// <para/>
		/// This returns (getter, setter). Either will be null if the property is missing its get method or set method respectively.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="hook"></param>
		public static (MethodDefAndRef, MethodDefAndRef) CodeBinderPropertyHooks(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ProxyAndHookPackage proxyAndHookData) {
			MethodDefAndRef getter = null;
			MethodDefAndRef setter = null;
			if (proxyAndHookData.PropertyGetterProxyMembers != null) {
				ExtensibleMethodProxyMembers extensibleMethodProxyMembers = proxyAndHookData.PropertyGetterProxyMembers.Value;
				BepInExHookRef hook = proxyAndHookData.PropertyGetterHookMembers.Value;
				getter = new MethodDefAndRef(
					main,
					hook.originalGameMethod.Name,
					hook.hookBinderMethodSignature,
					coreMembers.type.GenericBinder.Reference,
					CommonAttributes.SPECIAL_LOCKED_METHOD
				);
				getter.SetParameterName(0, "orig");
				getter.SetParameterName(1, "self");
				CommonGenerateBodyOfHookMethod(main, getter, in coreMembers, in binderMembers, in extensibleMethodProxyMembers, in hook);
			}
			if (proxyAndHookData.PropertySetterProxyMembers != null) {
				ExtensibleMethodProxyMembers extensibleMethodProxyMembers = proxyAndHookData.PropertySetterProxyMembers.Value;
				BepInExHookRef hook = proxyAndHookData.PropertySetterHookMembers.Value;
				setter = new MethodDefAndRef(
					main,
					hook.originalGameMethod.Name,
					hook.hookBinderMethodSignature,
					coreMembers.type.GenericBinder.Reference,
					CommonAttributes.SPECIAL_LOCKED_METHOD
				);
				setter.SetParameterName(0, "orig");
				setter.SetParameterName(1, "self");
				CommonGenerateBodyOfHookMethod(main, setter, in coreMembers, in binderMembers, in extensibleMethodProxyMembers, in hook);
			}

			return (getter, setter);
		}

		private static void CommonGenerateBodyOfHookMethod(ExtensiblesGenerator main, MethodDefAndRef boundMethod, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ExtensibleMethodProxyMembers extensibleMethodProxyMembers, in BepInExHookRef hook) {
			CilBody body = boundMethod.GetOrCreateBody();
			Local extensibleInstance = new Local(CommonGenericArgs.TYPE_ARG_0, "extensibleInstance");
			body.SetLocals(extensibleInstance);

			int numArgsForOrigSelfCall = boundMethod.Definition.MethodSig.GetParamCount();
			int numArgsForProxyCall = numArgsForOrigSelfCall - 2;

			body.EmitLdarg(1);                                          // self
			body.EmitLdloc(extensibleInstance, true);               // extensibleInstance (var)
			body.EmitCallvirt(binderMembers.tryGetBindingMethod.Reference);           // => TryGetBinding(self, out extensibleInstance)
			Instruction callOrig = body.NewBrDest();
			Instruction ret = new Instruction(OpCodes.Ret);
			body.Emit(OpCodes.Brfalse, callOrig); // Skip if there is no binding.

			// From here, there is a binding. Act on it.
			// First, tell the extensible type that a BIE hook is executing:
			body.EmitLdloc(extensibleInstance);
			body.EmitLdarg(0);
			body.Emit(OpCodes.Stfld, extensibleMethodProxyMembers.origDelegateReference.Reference);

			// Load all applicable args and call the extensible's declared override.
			body.EmitLdloc(extensibleInstance);
			body.EmitAmountOfArgs(numArgsForProxyCall, 0, false);
			body.Emit(OpCodes.Callvirt, extensibleMethodProxyMembers.proxyMethod.Reference);

			// Now remove the delegate reference.
			body.EmitLdloc(extensibleInstance);
			body.EmitNull();
			body.Emit(OpCodes.Stfld, extensibleMethodProxyMembers.origDelegateReference.Reference);

			// Jump to the return.
			body.Emit(OpCodes.Br, ret);

			////
			body.Emit(callOrig);
			// Just call orig(self) here.
			// To do this, load *everything*.
			body.EmitAmountOfArgs(numArgsForOrigSelfCall);
			// And now
			body.EmitCall(hook.origDelegateType.Invoke);

			body.Emit(ret);
			body.FinalizeMethodBody(main);
		}

		#endregion

		#region Binder CreateHooks Mutators

		/// <summary>
		/// <strong>Step 3 of the Extensible Type Pipeline:</strong><br/>
		/// This emits the beginning of the Binder's CreateBindings method.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="binderMembers"></param>
		public static void InitializeCreateBindingsMethod(ExtensiblesGenerator main, in ExtensibleBinderCoreMembers binderMembers) {
			CilBody createBindings = binderMembers.createBindingsMethod.GetOrCreateBody();
			createBindings.Emit(OpCodes.Ldsfld, binderMembers.hasCreatedBindingsField.Reference);
			Instruction needsNewBindings = createBindings.NewBrDest();
			createBindings.Emit(OpCodes.Brfalse, needsNewBindings);
			createBindings.EmitRet();
			createBindings.Emit(needsNewBindings);
		}

		/// <summary>
		/// One of the most complicated methods. This one generates just one part of the block of code that searches for, verifies, 
		/// and subscribes every single user-overridden method and property in the extensible type to BepInEx.
		/// This is used by steps 4.2 and 4.3.
		/// <para/>
		/// For properties, this must be called separately for its getter and its setter.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="binderHookMethod">The method generated for the binder. This is the method that gets subscribed to the BIE event.</param>
		/// <param name="coreMembers">The common members of all Extensible types.</param>
		/// <param name="binderMembers">The common members of all Binder types.</param>
		/// <param name="proxyMembers">The members specifically designed for this method in the Extensible type.</param>
		/// <param name="hookInfo">Context about the BepInEx hook for this method, or the generated property detour in the case of getters/setters.</param>
		/// <param name="propertyNameIfApplicable">If this is for a property, this is the name of the actual property without its fully qualified name.</param>
		/// <param name="isSecondMemberOfProperty">If true, this is the second member of a property. A local stores the reference, so use the local instead.</param>
		public static void AddMemberBindToCreateHooksMethod(ExtensiblesGenerator main, MethodDefAndRef binderHookMethod, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers, in ExtensibleMethodProxyMembers proxyMembers, in BepInExHookRef hookInfo, string propertyNameIfApplicable, bool isSecondMemberOfProperty) {
			const BindingFlags userExtTypeMemberFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			CilBody createBindings = binderMembers.createBindingsMethod.GetOrCreateBody();
			// REMEMBER: Loc0 is reserved (typeof(TExtensible))
			// Use AppendLocals for anything new.

			#region Alias Methods
			void Alias_EmitIfSetDoesNotContain(string methodName, Instruction gotoNext) {
				createBindings.EmitLdarg(0); // This is a HashSet<string> of members to skip.
				createBindings.Emit(OpCodes.Ldstr, methodName);
				createBindings.Emit(OpCodes.Callvirt, main.Shared.HashSetStringContains);
				createBindings.Emit(OpCodes.Brtrue, gotoNext);
			}

			void Alias_EmitCommonIfTypeContainsProperty(Instruction gotoNext) {
				createBindings.EmitNull();
				createBindings.EmitCall(main.Shared.PropertyInfosNotEqual);
				createBindings.Emit(OpCodes.Brfalse, gotoNext);
			}

			void Alias_EmitIfTypeContainsMethod(string methodName, Instruction gotoNext) {
				ITypeDefOrRef[] hookMethodSignatureRefs = binderHookMethod.Definition.MethodSig.Params.Skip(2).Select(param => param.ToTypeDefOrRef()).ToArray();

				createBindings.Emit(OpCodes.Ldloc_0);                                                               // typeof(TExtensible)
				createBindings.EmitGetMethod(main, methodName, userExtTypeMemberFlags, hookMethodSignatureRefs);    // GetMethod(...) (MethodInfo is now on stack)
				createBindings.EmitNull();
				createBindings.EmitCall(main.Shared.MethodInfosNotEqual);
				createBindings.Emit(OpCodes.Brfalse, gotoNext);
			}
			#endregion

			Instruction jumpHereIfSetDNC = createBindings.NewBrDest();
			string method = proxyMembers.proxyMethod.Name;
			Alias_EmitIfSetDoesNotContain(method, jumpHereIfSetDNC);
			if (hookInfo.IsCustomHook) {
				// Property
				if (isSecondMemberOfProperty) {
					createBindings.Emit(OpCodes.Ldloc_1);
					Alias_EmitCommonIfTypeContainsProperty(jumpHereIfSetDNC);
				} else {
					createBindings.Emit(OpCodes.Ldloc_0);                                                   // typeof(TExtensible)
					createBindings.EmitGetProperty(main, propertyNameIfApplicable, userExtTypeMemberFlags); // GetProperty(...) (PropertyInfo is now on stack)
					createBindings.Emit(OpCodes.Stloc_1);
					createBindings.Emit(OpCodes.Ldloc_1);
					Alias_EmitCommonIfTypeContainsProperty(jumpHereIfSetDNC);
				}

				if (!isSecondMemberOfProperty) {
					createBindings.EmitUnityDbgLog(main, $"[Extensible] Found implementation of Property {{{propertyNameIfApplicable}}}. Constructing property hook...");
				}
				createBindings.EmitUnityDbgLog(main, $"[Extensible] Hooking {binderHookMethod.Reference.Name.Substring(0, 3)}ter...");
				createBindings.EmitMethodof(main, (IMethod)hookInfo.originalGameMethod.Reference);          // from
				createBindings.EmitMethodof(main, (IMethod)binderHookMethod.Reference);                     // to
				createBindings.EmitNew(main.Shared.HookCtor);                                               // new Hook(from, to)
				createBindings.Emit(OpCodes.Pop);                                                           // Remove the new hook from the stack.

				createBindings.EmitUnityDbgLog(main, "[Extensible] Done.");

				// Above: I would like to do this differently to avoid calling the same thing on properties.
			} else {
				// Method
				Alias_EmitIfTypeContainsMethod(method, jumpHereIfSetDNC);

				createBindings.EmitUnityDbgLog(main, $"[Extensible] Found implementation of Method {{{binderHookMethod.Reference.Name}}}. Adding event hook...");

				createBindings.EmitNull();                                                                  // null
				createBindings.Emit(OpCodes.Ldftn, binderHookMethod.Reference);                             // IntPtr func
				createBindings.EmitNew((MemberRef)hookInfo.hookDelegateType.Constructor);                   // new hook_Method(null, func)
				createBindings.EmitCall(hookInfo.importedEventAdd.Reference);                               // On.Whatever.Method += ^
				
				createBindings.EmitUnityDbgLog(main, "[Extensible] Done.");
			}

			createBindings.EmitLdarg(0);
			createBindings.Emit(OpCodes.Ldstr, binderHookMethod.Name);
			createBindings.Emit(OpCodes.Callvirt, main.Shared.HashSetStringAdd);
			createBindings.Emit(OpCodes.Pop); // ^ returns a bool, don't want that. Get rid of it.

			createBindings.Emit(jumpHereIfSetDNC);
		}

		/// <summary>
		/// <strong>Step 6 of the Extensible Type Pipeline:</strong><br/>
		/// To be called after all other calls to the CreateBindings methods have been performed.
		/// This appends the last parts of the method (calling the supertype's CreateHooks method) and then closes this method.
		/// </summary>
		/// <param name="main"></param>
		/// <param name="coreMembers"></param>
		/// <param name="binderMembers"></param>
		public static void FinalizeCreateBindingsMethod(ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers, in ExtensibleBinderCoreMembers binderMembers) {
			CilBody createBindings = binderMembers.createBindingsMethod.GetOrCreateBody();
			ExtensibleTypeData type = coreMembers.type;
			if (main.TryGetParent(type, out ExtensibleTypeData parent)) {
				string createBindingsName = binderMembers.createBindingsMethod.Name;
				MethodDef parentCreateBindings = parent.Binder.Underlying.FindMethod(createBindingsName);
				//return new MethodDefAndRef(Generator, Definition, type.Reference, false);
				MethodDefAndRef parentCreate = new MethodDefAndRef(main, parentCreateBindings, parent.GenericBinder.Reference, false);

				createBindings.EmitUnityDbgLog(main, $"[Extensible] Moving up the hierarchy; hooking members of type {{{parent.ImportedGameType.FullName}}}...");
				createBindings.EmitLdarg(0);
				createBindings.EmitCall(parentCreate.Reference);
			}
			createBindings.EmitUnityDbgLog(main, $"[Extensible] Finished hooking members of type {{{type.ImportedGameType.FullName}}}.");

			createBindings.EmitRet();
			createBindings.FinalizeMethodBody(main);
			
		}


		#endregion

	}
}
