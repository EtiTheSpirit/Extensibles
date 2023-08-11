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


		private static TypeSig typeTypeSig = null;
		private static TypeSig runtimeTypeHandleSig = null;
		private static ITypeDefOrRef typeTypeRef = null;
		private static TypeSig bindingFlagsSig = null;
		private static TypeSig methodInfoSig = null;
		private static TypeSig propertyInfoSig = null;
		private static TypeSig systemReflectionBinderSig = null;
		private static TypeSig typeArraySig = null;
		private static TypeSig paramModifierArraySig = null;
		private static MemberRefUser getType = null;
		private static MemberRefUser getTypeFromHandle = null;
		private static MemberRefUser getMethod = null;
		private static MemberRefUser getProperty = null;


		#endregion

		#region Property Get/Set Mirrors

		#region Legacy

		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a getter for the mirror property that calls the getter of the original.
		/// </summary>
		/// <returns></returns>
		[Obsolete("This technique does not allow for property overrides.")]
		public static void CreateGetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDef orgProp, PropertyDefUser originalRefProperty) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled | PROPERTIES_VIRTUAL;

			MemberRef orgGetter = mirrorGenerator.cache.Import(orgProp.GetMethod);
			MethodDefUser mtd = new MethodDefUser($"get_{mirror.Name}", MethodSig.CreateInstance(mirror.PropertySig.RetType), attrs);
			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			getter.Instructions.Add(OpCodes.Call.ToInstruction(orgGetter));                         // (original prop name)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = getter;
			mtd.IsHideBySig = true;
			mtd.IsSpecialName = true;
			mirror.GetMethod = mtd;
		}


		/// <summary>
		/// Provided with a mirror property and its original counterpart, this will create a setter for the mirror property that calls the setter of the original.
		/// </summary>
		/// <param name="mirror"></param>
		/// <param name="originalSetter"></param>
		/// <param name="originalRefProperty">The property created that uses <see cref="CreateOriginalReferencer(PropertyDefUser, FieldDefUser, TypeDef)"/> as its getter.</param>
		/// <returns></returns>
		[Obsolete("This technique does not allow for property overrides.")]
		public static void CreateSetterToProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirror, PropertyDef orgProp, PropertyDefUser originalRefProperty) {
			MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.CompilerControlled | PROPERTIES_VIRTUAL;

			MemberRef orgSetter = mirrorGenerator.cache.Import(orgProp.SetMethod);
			MethodDefUser mtd = new MethodDefUser($"set_{mirror.Name}", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirror.PropertySig.RetType), attrs);
			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                               // this.
			setter.Instructions.Add(OpCodes.Call.ToInstruction(originalRefProperty.GetMethod));     // Original.
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());                               // value
			setter.Instructions.Add(OpCodes.Call.ToInstruction(orgSetter));                         // set (original prop name here)
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			mtd.Body = setter;
			mtd.IsHideBySig = true;
			mtd.IsSpecialName = true;
			mirror.SetMethod = mtd;
		}

		#endregion

		public static BIEProxiedPropertyResult TryGenerateBIEProxiedProperty(MirrorGenerator mirrorGenerator, PropertyDefUser mirrorProperty, PropertyDef originalPropertyToMirror, PropertyDefUser extensibleOriginalRef, MethodDefUser binderConstructionMethod, TypeDefUser binderType, GenericVar tExtendsExtensible, bool isAllowedInBinder) {
			const MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

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
					)
				);
				getProxy = new MethodDefUser(originalPropertyToMirror.GetMethod.Name, MethodSig.CreateInstance(propType), attrs);
				delegateGetterStorage = CommonMembers.GenerateDelegateHolder(originalPropertyToMirror.GetMethod.Name, getDelegate.ToTypeSig());
				isGetterInInvocation = CommonMembers.GenerateIsCallerInInvocation(mirrorGenerator, originalPropertyToMirror.GetMethod.Name);

				originalPropertyToMirror.GetMethod.SelectParameters(mirrorGenerator, out TypeSig[] parameters, out TypeSig retnParam, out TypeSig thisParam);
				MethodDef invoke = getDelegate.FindMethod("Invoke");
				getProxy.Body = CommonMembers.GenerateProxyMethodBody(mirrorGenerator, originalPropertyToMirror.GetMethod, parameters, originalPropertyToMirror.GetMethod.MakeMemberReference(mirrorGenerator), delegateGetterStorage, isGetterInInvocation, invoke, extensibleOriginalRef);
				mirrorProperty.GetMethod = getProxy;

				if (isAllowedInBinder) {
					CommonMembers.ProgramBinderCallManager(mirrorGenerator, originalDeclaringType, mirrorProperty.GetMethod, binderType, getReceiver, delegateGetterStorage, invoke, tExtendsExtensible);
				} else {
					getReceiver = null;
				}
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
					)
				);

				setProxy = new MethodDefUser(originalPropertyToMirror.SetMethod.Name, MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, propType), attrs);
				delegateSetterStorage = CommonMembers.GenerateDelegateHolder(originalPropertyToMirror.SetMethod.Name, setDelegate.ToTypeSig());
				isSetterInInvocation = CommonMembers.GenerateIsCallerInInvocation(mirrorGenerator, originalPropertyToMirror.SetMethod.Name);

				originalPropertyToMirror.SetMethod.SelectParameters(mirrorGenerator, out TypeSig[] parameters, out TypeSig retnParam, out TypeSig thisParam);
				MethodDef invoke = setDelegate.FindMethod("Invoke");
				setProxy.Body = CommonMembers.GenerateProxyMethodBody(mirrorGenerator, originalPropertyToMirror.SetMethod, parameters, originalPropertyToMirror.SetMethod.MakeMemberReference(mirrorGenerator), delegateSetterStorage, isSetterInInvocation, invoke, extensibleOriginalRef);
				mirrorProperty.SetMethod = setProxy;

				if (isAllowedInBinder) {
					CommonMembers.ProgramBinderCallManager(mirrorGenerator, originalDeclaringType, mirrorProperty.SetMethod, binderType, setReceiver, delegateSetterStorage, invoke, tExtendsExtensible);
				} else {
					setReceiver = null;
				}
			}

			if (isAllowedInBinder) {
				CommonMembers.CreateConditionalBinderCodeBlock(mirrorGenerator, binderConstructionMethod.Body, binderType, getDelegate, setDelegate, getReceiver, setReceiver, mirrorProperty, originalPropertyToMirror);
			}

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
		public static void CreateFieldProxy(MirrorGenerator mirrorGenerator, PropertyDefUser mirrorProp, MemberRef field, PropertyDefUser original) {
			const MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
			// Above: Do not make it virtual, this might be confusing.
			MethodDefUser get = new MethodDefUser($"get_{mirrorProp.Name}", MethodSig.CreateInstance(mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);
			MethodDefUser set = new MethodDefUser($"set_{mirrorProp.Name}", MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.cache.Import(field.FieldSig.Type)), attrs);

			CilBody getter = new CilBody();
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			getter.Instructions.Add(OpCodes.Call.ToInstruction(original.GetMethod));
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(field));
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());
			get.Body = getter;

			CilBody setter = new CilBody();
			setter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			setter.Instructions.Add(OpCodes.Call.ToInstruction(original.GetMethod));
			setter.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			setter.Instructions.Add(OpCodes.Stfld.ToInstruction(field));
			setter.Instructions.Add(OpCodes.Ret.ToInstruction());
			set.Body = setter;

			mirrorProp.GetMethod = get;
			mirrorProp.SetMethod = set;
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
		public static (MethodDefUser, FieldDefUser, FieldDefUser, MethodDefUser) TryGenerateBIEOrigCallAndProxies(MirrorGenerator mirrorGenerator, MethodDef original, PropertyDefUser originalRefProperty, TypeDefUser binderType, GenericVar tExtendsExtensible, TypeRef originalClass, MethodDef hookBinders, bool isAllowedInBinder) {
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

			const MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.Virtual;
			// Some preliminary setup.
			invalidOpExcCtorSig ??= MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			invalidOpExcCtor ??= new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			if (!BepInExExtensibles.TryGetBIEHook(mirrorGenerator, original, out BIEHookRef hookRef)) {
				return (null, null, null, null);
			}

			// Now create the field for the callback. This is the field storing BIE's orig() method and is set on the fly when a hook runs.
			//FieldDefUser bieOrigStoredDelegateCallback = new FieldDefUser($"<orig_{original.Name}>ExtensibleCallback", new FieldSig(hookRef.delegateSig), MirrorGenerator.PRIVATE_FIELD_TYPE);
			FieldDefUser bieOrigStoredDelegateCallback = CommonMembers.GenerateDelegateHolder(original.Name, hookRef.delegateSig);

			// And now create the proxy method itself.
			TypeSig originalReturnType = mirrorGenerator.cache.Import(original.ReturnType);

			#region Generate Extensible Virtual Proxy Method
			// This generates the proxy/clone method as seen in the Extensible class.

			// A field storing the type of invocation is needed:
			FieldDefUser isCallerInInvocation = CommonMembers.GenerateIsCallerInInvocation(mirrorGenerator, original.Name);

			// Now generate the mirror method.
			MemberRef refToOriginal = mirrorGenerator.cache.Import(original);
			MethodDefUser mirror = new MethodDefUser(original.Name, MethodSig.CreateInstance(originalReturnType, hookRef.methodParameters));
			for (int i = 0; i <= hookRef.methodParameters.Length; i++) {
				mirror.SetParameterName(i, original.Parameters[i].Name);
			}
			mirror.Attributes = attrs;
			mirror.Body = CommonMembers.GenerateProxyMethodBody(mirrorGenerator, original, hookRef.methodParameters, refToOriginal, bieOrigStoredDelegateCallback, isCallerInInvocation, hookRef.invoke, originalRefProperty);
			#endregion

			#region Binder Implementation
			// The binder is the system that bridges the gap between proxy and BIE.
			// This system generates the method that gets actually hooked into BIE's On events and redirect the call appropriately.

			#region Generate Static Hook Receiver Methods
			// Generate the methods that get added to the hooks.

			MethodDefUser receiverImpl = null;
			if (isAllowedInBinder) {
				TypeSig originalClassSig = originalClass.ToTypeSig();

				MethodSig receiverImplSig = MethodSig.CreateStatic(originalReturnType, hookRef.invokeParameters);
				receiverImpl = new MethodDefUser(original.Name, receiverImplSig);
				receiverImpl.Attributes |= MethodAttributes.Static | MirrorGenerator.PRIVATE_METHOD_TYPE;

				CommonMembers.ProgramBinderCallManager(mirrorGenerator, originalClassSig, mirror, binderType, receiverImpl, bieOrigStoredDelegateCallback, hookRef.invoke, tExtendsExtensible);
			}

			#endregion

			#region Generate Static Hook Subscription
			// Appends a block of conditional code that checks to see of the desired hook is overridden on the extensible type.
			// This code will append on On.Whatever if it was, and do nothing if it wasn't.
			// This specific section of the code only generates a single if statement for the current method, modifying 
			// the existing contents of the body (see below).

			if (isAllowedInBinder) {
				CilBody cctorBody = hookBinders.Body; // This will have modifications beforehand. *Append* to this.
				CommonMembers.CreateConditionalBinderCodeBlock(mirrorGenerator, cctorBody, binderType, mirror, receiverImpl, hookRef.hook);
			}

			#endregion

			#endregion

			return (mirror, bieOrigStoredDelegateCallback, isCallerInInvocation, receiverImpl);
		}


		#endregion

		#region Basic Members and Initializers

		public static (MethodDefUser, MethodDefUser, MethodDefUser) GenerateBinderBindAndDestroyMethod(MirrorGenerator mirrorGenerator, TypeDefUser mirrorType, GenericInstSig binder, TypeSig originalImported, GenericInstSig instancesSig, FieldDefUser hasHooked, MethodDefUser createHooks) {
			GenericInstSig extensibleWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
			GenericInstSig originalWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, originalImported);

			MethodSig bindSig = MethodSig.CreateStatic(extensibleWeakRef, originalImported);
			MethodDefUser bind = new MethodDefUser("Bind", bindSig, MethodAttributes.Public | MethodAttributes.Static);
			bind.SetParameterName(0, "toObject");

			MethodSig bindExistingSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalImported, new GenericVar(0));
			MethodDefUser bindExisting = new MethodDefUser("BindExisting", bindExistingSig, MirrorGenerator.PRIVATE_METHOD_TYPE | MethodAttributes.Static);

			MethodSig releaseSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, originalImported);
			MethodDefUser destroy = new MethodDefUser("TryReleaseCurrentBinding", releaseSig, MethodAttributes.Public | MethodAttributes.Static);
			destroy.SetParameterName(0, "toObject");

			ITypeDefOrRef instancesGeneric = instancesSig.ToTypeDefOrRef();
			ITypeDefOrRef extensibleTypeWeakReference = extensibleWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef originalTypeWeakRef = originalWeakRef.ToTypeDefOrRef();
			ITypeDefOrRef genericBinderType = binder.ToTypeDefOrRef();
			ITypeDefOrRef extensibleType = new GenericVar(0).ToTypeDefOrRef();

			MemberRefUser hasHookedInstance = new MemberRefUser(mirrorGenerator.MirrorModule, hasHooked.Name, hasHooked.FieldSig, genericBinderType);
			MemberRefUser createHooksInstance = new MemberRefUser(mirrorGenerator.MirrorModule, createHooks.Name, createHooks.MethodSig, genericBinderType);

			MemberRefUser instancesField = new MemberRefUser(mirrorGenerator.MirrorModule, "_instances", new FieldSig(instancesSig), genericBinderType);
			MemberRefUser originalWeakTypeField = new MemberRefUser(mirrorGenerator.MirrorModule, "<Extensible>original", new FieldSig(originalWeakRef), mirrorType);

			MethodSig tryGetValueSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0), new ByRefSig(new GenericVar(1)));
			MemberRefUser tryGetValue = new MemberRefUser(mirrorGenerator.MirrorModule, "TryGetValue", tryGetValueSig, instancesGeneric);

			MethodSig removeSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, new GenericVar(0));
			MemberRefUser remove = new MemberRefUser(mirrorGenerator.MirrorModule, "Remove", removeSig, instancesGeneric);

			MethodSig invalidOpExcCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, mirrorGenerator.MirrorModule.CorLibTypes.String);
			MemberRefUser invalidOpExcCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", invalidOpExcCtorSig, mirrorGenerator.cache.Import(typeof(InvalidOperationException)));

			MethodSig createInstanceSig = MethodSig.CreateStaticGeneric(1, new GenericMVar(0));
			MemberRefUser createInstanceRef = new MemberRefUser(mirrorGenerator.MirrorModule, "CreateInstance", createInstanceSig, mirrorGenerator.cache.Import(typeof(Activator)));
			MethodSpecUser createInstance = new MethodSpecUser(createInstanceRef, new GenericInstMethodSig(new GenericVar(0)));

			MethodSig setTargetSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser setTarget = new MemberRefUser(mirrorGenerator.MirrorModule, "SetTarget", setTargetSig, originalTypeWeakRef);

			MethodSig cwtAddSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0), new GenericVar(1));
			MemberRefUser cwtAdd = new MemberRefUser(mirrorGenerator.MirrorModule, "Add", cwtAddSig, instancesGeneric);

			MethodSig weakRefCtorSig = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser weakRefCtor = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", weakRefCtorSig, extensibleTypeWeakReference);

			#region Bind()
			CilBody body = new CilBody();
			Local instance = new Local(new GenericVar(0), "instance");
			body.Variables.Add(instance);

			Instruction nop = new Instruction(OpCodes.Nop);

			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ceq.ToInstruction());
			Instruction throwMsg = new Instruction(OpCodes.Ldstr, $"Duplicate binding! Only one instance of your current Extensible type can be bound to an instance of type {originalImported.GetName()} at a time. In general, you should explicitly call {destroy.Name} to free the binding immediately rather than waiting on the chance of garbage collection.");
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(throwMsg));

			body.Instructions.Add(OpCodes.Call.ToInstruction(createInstance)); // new T();
			body.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Box.ToInstruction(extensibleType));
			body.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakTypeField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(setTarget));
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtAdd));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefCtor)); // Need to remember this value here.
																			  // Time to call the Bind methods of base types.

			// Create hooks real quick.
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(hasHookedInstance));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Beq_S.ToInstruction(nop));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(createHooksInstance));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(hasHookedInstance));

			body.Instructions.Add(nop);

			TypeDef mirrorBaseType = mirrorType.BaseType.ResolveTypeDef();
			if (mirrorGenerator.IsDeclaredMirrorType(mirrorBaseType)) {
				TypeDef baseBinderType = mirrorBaseType.NestedTypes.First(tDef => tDef.Name == "Binder`1" && tDef is TypeDefUser);

				GenericInstSig genericBaseBinderSig = new GenericInstSig(baseBinderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
				GenericInstSig baseWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
				TypeRef baseParamType = mirrorGenerator.GetOriginal((TypeDefUser)mirrorBaseType);
				MethodSig baseBinderBindSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, baseParamType.ToTypeSig(), new GenericVar(0));
				MemberRefUser baseBinderBind = new MemberRefUser(mirrorGenerator.MirrorModule, "BindExisting", baseBinderBindSig, genericBaseBinderSig.ToTypeDefOrRef());
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction()); // Load the instance of the subclass. This is a valid member for the parent Bind method.
				body.Instructions.Add(OpCodes.Castclass.ToInstruction(baseParamType));
				body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction()); // Load the user-defined instance
				body.Instructions.Add(OpCodes.Call.ToInstruction(baseBinderBind)); // Now call the base class's BindExisting method.
			}

			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(throwMsg);
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			body.Instructions.Add(OpCodes.Throw.ToInstruction());

			bind.Body = body;
			#endregion

			#region BindExisting()
			body = new CilBody();
			body.Variables.Add(instance);

			Instruction ldsInstancesFld = OpCodes.Ldsfld.ToInstruction(instancesField);
			// Create hooks real quick.
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(hasHookedInstance));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Beq_S.ToInstruction(ldsInstancesFld));
			body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
			body.Instructions.Add(OpCodes.Call.ToInstruction(createHooksInstance));
			body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			body.Instructions.Add(OpCodes.Stsfld.ToInstruction(hasHookedInstance));

			body.Instructions.Add(ldsInstancesFld);
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ceq.ToInstruction());
			body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(throwMsg)); // Reuse throwMsg

			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(cwtAdd));

			if (mirrorGenerator.IsDeclaredMirrorType(mirrorBaseType)) {
				TypeDef baseBinderType = mirrorBaseType.NestedTypes.First(tDef => tDef.Name == "Binder`1" && tDef is TypeDefUser);
				GenericInstSig genericBaseBinderSig = new GenericInstSig(baseBinderType.ToTypeSig().ToClassOrValueTypeSig(), new GenericVar(0));
				GenericInstSig baseWeakRef = new GenericInstSig(mirrorGenerator.WeakReferenceTypeSig, new GenericVar(0));
				TypeRef baseParamType = mirrorGenerator.GetOriginal((TypeDefUser)mirrorBaseType);
				MethodSig baseBinderBindSig = MethodSig.CreateStatic(mirrorGenerator.MirrorModule.CorLibTypes.Void, baseParamType.ToTypeSig(), new GenericVar(0));
				MemberRefUser baseBinderBind = new MemberRefUser(mirrorGenerator.MirrorModule, "BindExisting", baseBinderBindSig, genericBaseBinderSig.ToTypeDefOrRef());
				body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction()); // Load the instance of the subclass. This is a valid member for the parent Bind method.
				body.Instructions.Add(OpCodes.Castclass.ToInstruction(baseParamType));
				body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction()); // Load the existing bound type.
				body.Instructions.Add(OpCodes.Call.ToInstruction(baseBinderBind)); // Now call the base class's BindExisting method.
			}

			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			body.Instructions.Add(throwMsg);
			body.Instructions.Add(OpCodes.Newobj.ToInstruction(invalidOpExcCtor));
			body.Instructions.Add(OpCodes.Throw.ToInstruction());
			bindExisting.Body = body;
			#endregion

			#region Release()
			body = new CilBody();
			instance = new Local(new GenericVar(0), "instance");
			//Local result = new Local(mirrorGenerator.MirrorModule.CorLibTypes.Boolean, "result");
			body.Variables.Add(instance);
			//body.Variables.Add(result);
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(instance));
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(tryGetValue));
			body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			Instruction pushFalse = new Instruction(OpCodes.Ldc_I4_0);
			body.Instructions.Add(OpCodes.Beq_S.ToInstruction(pushFalse));

			// Present:
			body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(instancesField));
			body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			body.Instructions.Add(OpCodes.Callvirt.ToInstruction(remove)); // This puts a bool onto the stack.
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			////
			body.Instructions.Add(pushFalse);
			body.Instructions.Add(OpCodes.Ret.ToInstruction());
			destroy.Body = body;
			#endregion
			return (bind, bindExisting, destroy);
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
			// EDIT FROM FUTURE XAN:
			// Yes RET!
			// I moved the hooking to the Bind method via an external static method.
			body.Instructions.Add(OpCodes.Ret.ToInstruction());

			staticCtor.Body = body;
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
			Local local = new Local(originalRefProp.PropertySig.RetType, "storedOriginal", 0);
			getter.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());                                           // this.
			getter.Instructions.Add(OpCodes.Ldfld.ToInstruction(originalWeakRefFld));                           // _original
			getter.Instructions.Add(OpCodes.Ldnull.ToInstruction());                                            // push null
			getter.Instructions.Add(OpCodes.Stloc_0.ToInstruction());                                           // store in local
			getter.Instructions.Add(OpCodes.Ldloca_S.ToInstruction(local));                                     // Load by reference
			getter.Instructions.Add(OpCodes.Call.ToInstruction(tryGetTargetMember));                            // Call TryGetTarget
			getter.Instructions.Add(OpCodes.Pop.ToInstruction());                                               // Discard the return value.
			getter.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());                                           // Load slot 0 (which stores the ref now)
			getter.Instructions.Add(OpCodes.Ret.ToInstruction());                                               // Return the ref.
			getter.Variables.Add(local);

			MethodDefUser mDef = new MethodDefUser($"get_{originalRefProp.Name}", MethodSig.CreateInstance(originalRefProp.PropertySig.RetType), MethodAttributes.Public | MethodAttributes.CompilerControlled);
			mDef.Body = getter;

			originalRefProp.GetMethod = mDef;
		}

		/// <summary>
		/// This creates the constructor of the Extensible class.
		/// </summary>
		/// <param name="mirrorGenerator"></param>
		/// <param name="originalWeakRefFld"></param>
		/// <param name="weakRefGenericInstance"></param>
		/// <returns></returns>
		public static MethodDefUser CreateConstructor(MirrorGenerator mirrorGenerator, FieldDefUser originalWeakRefFld, GenericInstSig weakRefGenericInstance) {
			ITypeDefOrRef weakRefTypeRef = weakRefGenericInstance.ToTypeDefOrRef();

			//MethodSig constructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, originalRefProp.PropertySig.RetType);
			MethodSig mirrorTypeConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void);
			MethodSig weakRefConstructorSignature = MethodSig.CreateInstance(mirrorGenerator.MirrorModule.CorLibTypes.Void, new GenericVar(0));
			MemberRefUser weakRefConstructorRef = new MemberRefUser(mirrorGenerator.MirrorModule, ".ctor", weakRefConstructorSignature, weakRefTypeRef);

			MethodDefUser ctor = new MethodDefUser(".ctor", mirrorTypeConstructorSignature, MethodAttributes.Family | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			CilBody ctorBody = new CilBody();
			ctorBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
			ctorBody.Instructions.Add(OpCodes.Newobj.ToInstruction(weakRefConstructorRef));
			ctorBody.Instructions.Add(OpCodes.Stfld.ToInstruction(originalWeakRefFld));
			ctorBody.Instructions.Add(OpCodes.Ret.ToInstruction());
			ctor.Body = ctorBody;

			return ctor;
		}

		#endregion


	}
}
