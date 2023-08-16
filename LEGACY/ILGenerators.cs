using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;
using System.Security.AccessControl;
using HookGenExtender.Utilities.Representations;
using HookGenExtender.Utilities.ILGeneratorParts;
using MonoMod.RuntimeDetour.HookGen;
using MonoMod.RuntimeDetour;
using HookGenExtender.Utilities.Shared;

namespace HookGenExtender.Utilities {
	public static partial class ILGenerators {

		/// <summary>
		/// The virtual flag for property getters/setters. This might be 0, in which case overridable properties are not supported yet
		/// </summary>
		[Obsolete]
		private const MethodAttributes PROPERTIES_VIRTUAL = MethodAttributes.Virtual;

		#region (Incomplete) Caches

		// TODO: Cache everything!

		internal static MethodSig invalidOpExcCtorSig = null;
		internal static MemberRefUser invalidOpExcCtor = null;



		#endregion

		#region Property Get/Set Mirrors

		public static BIEProxiedPropertyResult TryGenerateBIEProxiedProperty(MirrorGenerator mirrorGenerator, TypeRef originalTypeRef, TypeDefUser inUserType, PropertyDefUser mirrorProperty, PropertyDef originalPropertyToMirror, PropertyDefUser extensibleOriginalRef, MethodDefUser binderConstructionMethod, TypeDefUser binderType, GenericVar tExtendsExtensible) {
			const MethodAttributes proxyAttrs = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

			// The newest feature is property injections.
			// These are not something that BIE natively provides, thus they must be generated during runtime.
			// To achieve this, I will use RuntimeDetour's Hook class.
			// Doing this means:
			// 1: Generating two delegate types (orig_Get... and orig_Set...)
			// 2: Generating the hook method itself that it will redirect to
			// 3: Constructing the Hook instance (from RuntimeDetour).

			// Step 1: Generate delegate types.
			TypeSig originalDeclaringType = mirrorGenerator.cache.Import(originalPropertyToMirror.DeclaringType).ToTypeSig();

			TypeDefUser getDelegate = null;
			TypeDefUser setDelegate = null;
			MethodDefUser setProxy = null;
			MethodDefUser getProxy = null;
			MethodDefUser setReceiver = null;
			MethodDefUser getReceiver = null;
			FieldDefUser delegateGetterStorage = null;
			FieldDefUser isGetterInInvocation = null;
			FieldDefUser delegateSetterStorage = null;
			FieldDefUser isSetterInInvocation = null;

			TypeSig propType = mirrorProperty.PropertySig.RetType;
			if (originalPropertyToMirror.GetMethod != null && !originalPropertyToMirror.GetMethod.IsAbstract) {
				getDelegate = CommonMembers.CreateDelegateType(mirrorGenerator, MethodSig.CreateInstance(propType, originalDeclaringType), $"orig_get_{mirrorProperty.Name}");
				getReceiver = new MethodDefUser(
					originalPropertyToMirror.GetMethod.Name,
					MethodSig.CreateStatic(
						propType,
						getDelegate.ToTypeSig(),
						originalDeclaringType
					),
					MethodAttributes.Private | MethodAttributes.Static
				);
				getProxy = new MethodDefUser(originalPropertyToMirror.GetMethod.Name, MethodSig.CreateInstance(propType), proxyAttrs);
				delegateGetterStorage = CommonMembers.GenerateDelegateHolder(originalPropertyToMirror.GetMethod.Name, getDelegate.ToTypeSig());
				isGetterInInvocation = CommonMembers.GenerateIsCallerInInvocation(mirrorGenerator, originalPropertyToMirror.GetMethod.Name);

				originalPropertyToMirror.GetMethod.SelectParameters(mirrorGenerator, out TypeSig[] parameters, out TypeSig retnParam, out TypeSig thisParam);
				MethodDef invoke = getDelegate.FindMethod("Invoke");
				getProxy.Body = CommonMembers.GenerateProxyMethodBody(mirrorGenerator, originalPropertyToMirror.GetMethod, parameters, originalPropertyToMirror.GetMethod.MakeMemberReference(mirrorGenerator), delegateGetterStorage, isGetterInInvocation, invoke, extensibleOriginalRef);
				mirrorProperty.GetMethod = getProxy;

				CommonMembers.ProgramBinderCallManager(mirrorGenerator, originalDeclaringType, mirrorProperty.GetMethod, binderType, getReceiver, delegateGetterStorage, invoke, tExtendsExtensible);
			}
			if (originalPropertyToMirror.SetMethod != null && !originalPropertyToMirror.SetMethod.IsAbstract) {
				setDelegate = CommonMembers.CreateDelegateType(mirrorGenerator, MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalDeclaringType, propType), $"orig_set_{mirrorProperty.Name}");
				setReceiver = new MethodDefUser(
					originalPropertyToMirror.SetMethod.Name,
					MethodSig.CreateStatic(
						mirrorGenerator.MirrorModule.CorLibTypes.Void,
						setDelegate.ToTypeSig(),
						originalDeclaringType,
						propType
					),
					MethodAttributes.Private | MethodAttributes.Static
				);

				setProxy = new MethodDefUser(originalPropertyToMirror.SetMethod.Name, MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, propType), proxyAttrs);
				delegateSetterStorage = CommonMembers.GenerateDelegateHolder(originalPropertyToMirror.SetMethod.Name, setDelegate.ToTypeSig());
				isSetterInInvocation = CommonMembers.GenerateIsCallerInInvocation(mirrorGenerator, originalPropertyToMirror.SetMethod.Name);

				originalPropertyToMirror.SetMethod.SelectParameters(mirrorGenerator, out TypeSig[] parameters, out TypeSig retnParam, out TypeSig thisParam);
				MethodDef invoke = setDelegate.FindMethod("Invoke");
				setProxy.Body = CommonMembers.GenerateProxyMethodBody(mirrorGenerator, originalPropertyToMirror.SetMethod, parameters, originalPropertyToMirror.SetMethod.MakeMemberReference(mirrorGenerator), delegateSetterStorage, isSetterInInvocation, invoke, extensibleOriginalRef);
				mirrorProperty.SetMethod = setProxy;

				CommonMembers.ProgramBinderCallManager(mirrorGenerator, originalDeclaringType, mirrorProperty.SetMethod, binderType, setReceiver, delegateSetterStorage, invoke, tExtendsExtensible);
			}

			CommonMembers.CreateConditionalBinderCodeBlock(mirrorGenerator, binderConstructionMethod.Body, binderType, originalTypeRef, inUserType, getDelegate, setDelegate, getReceiver, setReceiver, mirrorProperty, originalPropertyToMirror);
			
			return new BIEProxiedPropertyResult(
				getDelegate,
				setDelegate,
				getProxy,
				setProxy,
				getReceiver,
				setReceiver,
				isGetterInInvocation,
				isSetterInInvocation,
				delegateGetterStorage,
				delegateSetterStorage
			);
		}

		#endregion

		#region Field Get/Set Mirror

		/// <summary>
		/// Generates a getter and setter for the property that reads to and writes from the provided <paramref name="field"/>.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="mirrorProp"></param>
		/// <param name="field"></param>
		/// <param name="original"></param>
		public static void CreateFieldProxy(MirrorGenerator mirrorGenerator, PropertyDefUser mirrorProp, FieldDef originalField, MemberRef field, PropertyDefUser original) {
			const MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
			// Above: Do not make it virtual, this might be confusing.

			bool isReadOnly = originalField.Attributes.HasFlag(FieldAttributes.InitOnly);

			// Alright, so here's a fun trick:
			TypeSig fieldType = mirrorGenerator.cache.Import(field.FieldSig.Type);
			ByRefSig fieldTypeByRef = new ByRefSig(fieldType);
			if (isReadOnly) {
				MethodDefUser get = new MethodDefUser($"get_{mirrorProp.Name}", MethodSig.CreateInstance(fieldType), attrs);
				CilBody getter = new CilBody();
				getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				getter.Instructions.Add(OpCodes.Call.ToInstruction(original.GetMethod));
				getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));
				getter.Instructions.Add(OpCodes.Ret.ToInstruction());
				get.Body = getter;
				mirrorProp.GetMethod = get;
			} else {
				MethodDefUser get = new MethodDefUser($"get_{mirrorProp.Name}", MethodSig.CreateInstance(fieldTypeByRef), attrs);
				CilBody getter = new CilBody();
				getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				getter.Instructions.Add(OpCodes.Call.ToInstruction(original.GetMethod));
				getter.Instructions.Add(OpCodes.Ldflda.ToInstruction(field));
				getter.Instructions.Add(OpCodes.Ret.ToInstruction());
				get.Body = getter;
				mirrorProp.PropertySig = new PropertySig(true, fieldTypeByRef);
				mirrorProp.GetMethod = get;
			}

		}

		#endregion

		#region Method Mirrors

		/// <summary>
		/// Generates a delegate pointing to BepInEx's hook orig_MethodName delegates, allowing the mirror method to be used in BIE patches.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="original"></param>
		/// <param name="originalRefProperty"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public static (MethodDefUser, FieldDefUser, FieldDefUser, MethodDefUser) TryGenerateBIEOrigCallAndProxies(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, TypeDefUser binderType, GenericVar tExtendsExtensible, TypeRef originalClass, MethodDef createHooksMtd) {
			/* TO FUTURE XAN / MAINTAINERS:
			 * The way this is implemented is complicated, to put it lightly.
			 * For each and every possible hook, four things are generated in the Extensible class...
			 * 1: The advanced proxy method 
			 *		- (if called in the context of a hook, invoking the base of that method invokes orig(self, ...))
			 *		- (if called outside of a hook, invoking the base of that method invokes the method of the original object and triggers hooks)
			 *		
			 * 2: The context tracker boolean field (is the current invocation in the process of a hook, or is it outside of a hook?)
			 * 3: The current original delegate to call when appropriate.
			 * 4: The method in the Binder<T> of this extensible class that actually subscribes to the BIE On. event.
			 *		4a: The if statement that checks if the method in question is overridden by the user defined <T>, and subscribes iff it is overridden.
			 * 
			 * Because each of these components must exist per method, they are all generated here in this function in that order.
			 */

			const MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig;
			// Some preliminary setup.
			invalidOpExcCtorSig ??= MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			invalidOpExcCtor ??= new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			if (!BepInExExtensibles.TryGetBIEHook(mirrorGenerator, original, out BIEHookRef hookRef)) {
				return (null, null, null, null);
			}

			// And now create the proxy method itself.
			TypeSig originalReturnType = mirrorGenerator.cache.Import(original.ReturnType);

			FieldDefUser bieOrigStoredDelegateCallback = CommonMembers.GenerateDelegateHolder(original.Name, hookRef.delegateSig);
			FieldDefUser isCallerInInvocation = CommonMembers.GenerateIsCallerInInvocation(mirrorGenerator, original.Name);

			// Now generate the mirror method.
			MemberRef refToOriginal = mirrorGenerator.cache.Import(original);
			MethodDefUser mirror = new MethodDefUser(original.Name, MethodSig.CreateInstance(originalReturnType, hookRef.methodParameters));
			for (int i = 0; i <= hookRef.methodParameters.Length; i++) {
				mirror.SetParameterName(i, original.Parameters[i].Name);
			}
			mirror.Attributes = attrs;
			mirror.Body = CommonMembers.GenerateProxyMethodBody(mirrorGenerator, original, hookRef.methodParameters, refToOriginal, bieOrigStoredDelegateCallback, isCallerInInvocation, hookRef.invoke, originalRefProperty);

			TypeSig originalClassSig = originalClass.ToTypeSig();
			MethodSig receiverImplSig = MethodSig.CreateStatic(originalReturnType, hookRef.invokeParameters);
			MethodDefUser receiverImpl = new MethodDefUser(original.Name, receiverImplSig);
			receiverImpl.Attributes |= MethodAttributes.Static | MethodAttributes.Private;

			CommonMembers.ProgramBinderCallManager(mirrorGenerator, originalClassSig, mirror, binderType, receiverImpl, bieOrigStoredDelegateCallback, hookRef.invoke, tExtendsExtensible);

			CilBody createHooksBody = createHooksMtd.Body; // This will have modifications beforehand. *Append* to this.
			CommonMembers.CreateConditionalBinderCodeBlock(mirrorGenerator, originalClass, createHooksBody, binderType, mirror, receiverImpl, hookRef.hook);

			return (mirror, bieOrigStoredDelegateCallback, isCallerInInvocation, receiverImpl);
		}


		#endregion

		#region Basic Members and Initializers

		public static (MethodDefUser[], MethodDefUser, FieldDefUser, MethodDefUser) GenerateBinderBindAndDestroyMethods(MirrorGenerator mirrorGenerator, TypeDef originalDef, TypeDefUser mirrorType, TypeDef binderType, GenericInstSig binderInstanceSig, TypeSig originalImported, GenericInstSig instancesSig, FieldDefUser hasHooked, MethodDefUser createHooks) {
			GenericVar tExtensible = (GenericVar)instancesSig.GenericArguments[1];
			GenericInstSig extensibleWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, tExtensible);
			GenericInstSig originalWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, originalImported);

			CommonMembers.PrepareCachedSystemReflectionStuffs(mirrorGenerator);

			//MethodSig bindExistingSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalImported, new GenericVar(0));
			//MethodDefUser bindExisting = new MethodDefUser("BindExisting", bindExistingSig, MirrorGenerator.PRIVATE_METHOD_TYPE | MethodAttributes.Static);

			MethodSig releaseSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, originalImported);
			// To future xan/maintainers: TryReleaseCurrentBinding is a string that is recurring, if you rename it, change it elsewhere.
			MethodDefUser destroy = new MethodDefUser("TryReleaseCurrentBinding", releaseSig, MethodAttributes.Public | MethodAttributes.Static);
			destroy.SetParameterName(0, "toObject");

			FieldDefUser constructorCache = new FieldDefUser("<Binder>ConstructorCache", new FieldSig(mirrorGenerator.cache.ImportAsTypeSig(typeof(System.Reflection.ConstructorInfo[]))));

			ITypeDefOrRef instancesGeneric = instancesSig.ToTypeDefOrRef();
			ITypeDefOrRef extensibleTypeWeakReference = extensibleWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef originalTypeWeakRef = originalWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef genericBinderType = binderInstanceSig.ToTypeDefOrRef();
			ITypeDefOrRef extensibleType = tExtensible.ToTypeDefOrRef();

			MemberRef hasHookedInstance = hasHooked.MakeMemberReference(mirrorGenerator, genericBinderType, false); //new MemberRefUser(mirrorGenerator.MirrorModule, hasHooked.Name, hasHooked.FieldSig, genericBinderType);
			MemberRef createHooksInstance = createHooks.MakeMemberReference(mirrorGenerator, genericBinderType, false); //new MemberRefUser(mirrorGenerator.MirrorModule, createHooks.Name, createHooks.MethodSig, genericBinderType);
			MemberRef constructorCacheInstance = constructorCache.MakeMemberReference(mirrorGenerator, genericBinderType, false);

			MemberRefUser instancesField = new MemberRefUser(mirrorGenerator.MirrorModule, "_instances", new FieldSig(instancesSig), genericBinderType);
			MemberRefUser originalWeakTypeField = new MemberRefUser(mirrorGenerator.MirrorModule, "<Extensible>original", new FieldSig(originalWeakRef), mirrorType);

			SharedGeneralSignatures signatures = mirrorGenerator.CommonSignatures.General;
			MemberRefUser cwtTryGetValue = signatures.ReferenceCWTTryGetValue(instancesGeneric);
			MemberRefUser cwtRemove = signatures.ReferenceCWTRemove(instancesGeneric);
			MemberRefUser cwtAdd = signatures.ReferenceCWTAdd(instancesGeneric);

			CommonMembers.PrependConstructorValidator(mirrorGenerator, createHooks.Body);

			MethodDefUser[] binds = CommonMembers.CreateBinderMethodBodies(mirrorGenerator, originalDef, binderType, constructorCacheInstance, originalImported, originalWeakTypeField, instancesGeneric, instancesField, hasHookedInstance, createHooksInstance);


			TypeSig outParamSig = new ByRefSig(tExtensible);
			MethodSig staticTryGetValueSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, originalImported, outParamSig);

			// Note: The string below is recurring, if you rename it, search for other occurrences.
			MethodDefUser tryGetBoundInstance = new MethodDefUser("TryGetBoundInstance", staticTryGetValueSig, MethodAttributes.Public | MethodAttributes.Static);
			tryGetBoundInstance.Parameters[1].GetOrCreateParamDef().IsOut = true;
			tryGetBoundInstance.SetParameterName(0, "original");
			tryGetBoundInstance.SetParameterName(1, "boundInstance");

			CilBody tryGetBoundBody = new CilBody();
			tryGetBoundBody.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			tryGetBoundBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			tryGetBoundBody.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			tryGetBoundBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtTryGetValue));
			tryGetBoundBody.Instructions.Add(OpCodes.Ret.ToInstruction());
			tryGetBoundInstance.Body = tryGetBoundBody;

			#region Release()
			CilBody destroyBody = new CilBody();
			Local instance = new Local(new GenericVar(0), "instance");
			//Local result = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "result");
			destroyBody.Variables.Add(instance);
			//body.Variables.Add(result);
			destroyBody.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			destroyBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			destroyBody.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			destroyBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtTryGetValue));
			destroyBody.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			Instruction pushFalse = new Instruction(OpCodes.Ldc_I4_0);
			destroyBody.Instructions.Add(OpCodes.Beq_S.ToInstruction(pushFalse));

			// Present:
			destroyBody.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			destroyBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			destroyBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtRemove)); // This puts a bool onto the stack.
			destroyBody.Instructions.Add(OpCodes.Ret.ToInstruction());

			////
			destroyBody.Instructions.Add(pushFalse);
			destroyBody.Instructions.Add(OpCodes.Ret.ToInstruction());
			destroy.Body = destroyBody;
			#endregion

			return (binds, destroy, constructorCache, tryGetBoundInstance);
		}

		/// <summary>
		/// This is used to begin the generation of the static constructor for a Binder type. It assigns the _instances field, and logs a message indicating that initialization has started.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="mirrorType"></param>
		/// <param name="binderNongeneric"></param>
		/// <param name="binder"></param>
		/// <param name="instancesSig"></param>
		public static void GenerateInstancesConstructor(MirrorGenerator mirrorGenerator, TypeDef mirrorType, TypeDef binderNongeneric, GenericInstSig binder, GenericInstSig instancesSig) {
			ITypeDefOrRef binderType = binder.ToTypeDefOrRef();
			MemberRefUser instancesField = new MemberRefUser(mirrorGenerator.MirrorModule, "_instances", new FieldSig(instancesSig), binderType);

			MethodSig ctor = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser cwtCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", ctor, instancesSig.ToTypeDefOrRef());

			MethodDef staticCtor = binderNongeneric.FindOrCreateStaticConstructor();
			CilBody body = new CilBody();
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(cwtCtor));
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(instancesField));
			// NO RET - more code will be added later.

			staticCtor.Body = body;
		}

		public static CilBody CreateInitializeMethodBody(MirrorGenerator mirrorGenerator, ITypeDefOrRef instanceType, MemberRef createHooks, MemberRef hasCreatedHooks) {
			CilBody body = new CilBody();

			CommonMembers.PrepareCachedSystemReflectionStuffs(mirrorGenerator);
			ITypeDefOrRef hashSet = mirrorGenerator.MirrorModule.Import(typeof(HashSet<>));
			GenericInstSig stringSet = new GenericInstSig(hashSet.ToTypeSig().ToClassOrValueTypeSig(), mirrorGenerator.MirrorModule.CorLibTypes.String);
			MethodSig hashSetCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MemberRefUser hashSetCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", hashSetCtorSig, stringSet.ToTypeDefOrRef());

			Instruction loadToken = OpCodes.Ldtoken.ToInstruction(instanceType);
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(hasCreatedHooks));
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(loadToken));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			body.Instructions.Add(loadToken);
			body.Instructions.Add(OpCodes.Call.ToInstruction(CommonMembers.getTypeFromHandle));
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(hashSetCtor));
			body.Instructions.Add(OpCodes.Call.ToInstruction(createHooks));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(hasCreatedHooks));
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			return body;
		}

		/// <summary>
		/// For use when creating the "Original" property of mirror types, this generates the IL of the getter to get the reference from the weak field.
		/// </summary>
		/// <param name="originalRefProp"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <returns></returns>
		public static void CreateOriginalReferencer(MirrorGenerator mirrorGenerator, PropertyDefUser originalRefProp, FieldDefUser originalWeakRefFld, GenericInstSig weakRef) {
			ModuleDef module = weakRef.Module;
			ITypeDefOrRef weakRefTypeRef = weakRef.ToTypeDefOrRef();

			MethodSig tryGetTargetSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new ByRefSig(new GenericVar(0)));
			MemberRefUser tryGetTargetMember = new MemberRefUser(module, "TryGetTarget", tryGetTargetSig, weakRefTypeRef);

			CilBody getter = new CilBody();
			Local storedOriginal = new Local(originalRefProp.PropertySig.RetType, "storedOriginal", 0);
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc_0.ToInstruction());                                           // store in local
			getter.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(storedOriginal));							// Load by reference
			getter.Instructions.Add(OpCodes.Call.ToInstruction(tryGetTargetMember));                            // Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.
			getter.Variables.Add(storedOriginal);

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public | MethodAttributes.CompilerControlled);
			mDef.Body = getter;

			originalRefProp.GetMethod = mDef;
		}

		/// <summary>
		/// Generates the body of the op_Implicit cast to the original type.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="original"></param>
		/// <param name="cast"></param>
		public static void CreateImplicitCastToOriginal(MirrorGenerator mirrorGenerator, TypeDefUser replacement, PropertyDefUser original, MethodDefUser cast) {
			CilBody caster = new CilBody();
			caster.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			caster.Instructions.Add(OpCodes.Callvirt.ToInstruction(original.GetMethod.MakeMemberReference(mirrorGenerator, replacement, false)));
			caster.Instructions.Add(OpCodes.Ret.ToInstruction());
			cast.Body = caster;
		}

		/// <summary>
		/// This creates the constructor of the Extensible class.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <param name="weakRefGenericInstance"></param>
		/// <returns></returns>
		public static MethodDefUser CreateConstructor(MirrorGenerator mirrorGenerator, TypeDef originalType, TypeSig importedOriginalType, FieldDefUser originalWeakRefFld, GenericInstSig weakRefGenericInstance) {
			ITypeDefOrRef weakRefTypeRef = weakRefGenericInstance.ToTypeDefOrRef();

			//MethodSig constructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalRefProp.PropertySig.RetType);
			MethodSig mirrorTypeConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, importedOriginalType);
			MethodSig weakRefConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser weakRefConstructorRef = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", weakRefConstructorSignature, weakRefTypeRef);

			MethodDefUser ctor = new MethodDefUser(".ctor", mirrorTypeConstructorSignature, MethodAttributes.Family | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			CilBody ctorBody = new CilBody();

			// If there is a superclass to this, we need to call its constructor as well.
			if (originalType.BaseType is TypeDef super && !super.ToTypeSig().IsCorLibType) {
				if (mirrorGenerator.mirrorLookup.TryGetValue(super, out (TypeDefUser, TypeDefUser, TypeRef) data)) {
					MethodSig superConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, data.Item3.ToTypeSig());
					MemberRefUser @base = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", superConstructorSignature, data.Item1);
					ctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
					ctorBody.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
					ctorBody.Instructions.Add(OpCodes.Call.ToInstruction(@base)); // call base ctor here.
				}
			}

			ctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefConstructorRef));
			ctorBody.Instructions.Add(OpCodes.Stfld.ToInstruction(originalWeakRefFld));
			ctorBody.Instructions.Add(OpCodes.Ret.ToInstruction());
			ctor.Body = ctorBody;

			return ctor;
		}

		#endregion


	}
}
