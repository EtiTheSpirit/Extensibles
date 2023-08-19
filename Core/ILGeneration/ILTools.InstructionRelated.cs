using dnlib.DotNet.Emit;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Reflection;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.ExtremelySpecific;
using System.Diagnostics;
using HookGenExtender.Core.Utils.Ext;

namespace HookGenExtender.Core.ILGeneration {

	/// <summary>
	/// IL that assists in generating objects and references.
	/// </summary>
	public static partial class ILTools {

		/// <summary>
		/// Emits an instruction via its opcode and an optional operand.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="opcode"></param>
		/// <param name="operand"></param>
		/// <returns></returns>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction Emit(this CilBody body, OpCode opcode, object operand = null) {
			Instruction instruction = new Instruction(opcode, operand);
			body.Instructions.Add(instruction);
			return instruction;
		}

		/// <summary>
		/// Emits an instruction directly.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="instruction"></param>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction Emit(this CilBody body, Instruction instruction) {
			body.Instructions.Add(instruction);
			return instruction;
		}

		/// <summary>
		/// Emits <c>ldarg_0</c> (<see langword="this"/>, in instance methods) in a method, for convenience.
		/// </summary>
		/// <param name="body"></param>
		/// <returns></returns>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction EmitThis(this CilBody body) => body.Emit(OpCodes.Ldarg_0);

		/// <summary>
		/// Calls the provided constructor.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="constructor"></param>
		/// <returns></returns>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction EmitNew(this CilBody body, MemberRef constructor) => body.Emit(OpCodes.Newobj, constructor);

		/// <summary>
		/// Emits <see cref="OpCodes.Ret"/>
		/// </summary>
		/// <param name="body"></param>
		/// <returns></returns>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction EmitRet(this CilBody body) => body.Emit(OpCodes.Ret);

		/// <summary>
		/// Emits <see cref="OpCodes.Ldnull"/>
		/// </summary>
		/// <param name="body"></param>
		/// <returns></returns>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction EmitNull(this CilBody body) => body.Emit(OpCodes.Ldnull);

		/// <summary>
		/// Emits <see cref="OpCodes.Dup"/>
		/// </summary>
		/// <param name="body"></param>
		/// <returns></returns>
		[DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction EmitDup(this CilBody body) => body.Emit(OpCodes.Dup);

		/// <summary>
		/// Creates, but does <strong>NOT</strong> emit, <see cref="OpCodes.Nop"/>. 
		/// This is done with the intent of the nop being used as a destination for branches.
		/// <para/>
		/// This instruction's operand is the <see cref="CilBody"/> that this is called with. This enforces
		/// that you must use <see cref="ExtendedCILBody.OptimizeNopJumps(CilBody)"/> before emitting to the DLL file
		/// otherwise an exception will occur.
		/// </summary>
		/// <returns></returns>
		public static Instruction NewBrDest(this CilBody body) {
			return new Instruction(OpCodes.Nop, body);
		}

		/// <summary>
		/// Emits (<paramref name="amount"/>) ldargs instructions, starting at arg (<paramref name="startingFrom"/>) and ending at arg (<paramref name="startingFrom"/> + <paramref name="amount"/>)
		/// </summary>
		/// <param name="body"></param>
		/// <param name="amount"></param>
		/// <param name="amountIsArrayLength">If true, the amount has <paramref name="startingFrom"/> subtracted from it.</param>
		/// <returns>The first instruction of the generated code, or null if no args were generated.</returns>
		public static Instruction EmitAmountOfArgs(this CilBody body, int amount, int startingFrom = 0, bool amountIsArrayLength = true) {
			Instruction first = null;
			int argIndex = startingFrom;
			if (amountIsArrayLength) amount -= startingFrom;
			for (int i = 0; i < amount; i++) {
				Instruction ldarg = body.EmitLdarg(argIndex);
				if (first == null) first = ldarg;
				argIndex++;
			}
			return first;
		}

		/// <summary>
		/// Emits the code equivalent to <see langword="typeof"/>(<paramref name="type"/>).
		/// </summary>
		/// <param name="body"></param>
		/// <param name="type"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitTypeof(this CilBody body, ExtensiblesGenerator main, ITypeDefOrRef type) {
			// TO FUTURE XAN: In case you come back and wonder, no, TypeSig is not valid here.
			Instruction first = body.Emit(OpCodes.Ldtoken, type);
			body.EmitCall(main.Shared.GetTypeFromHandle);
			return first;
		}

		/// <summary>
		/// Emits the code equivalent to <see langword="methodof"/>(<paramref name="method"/>).<br/>
		/// Note that there is no such thing as "<see langword="methodof"/>", this is made up as a way of 
		/// representing "returns a <see cref="MethodInfo"/> from a method signature", much like how
		/// <see langword="typeof"/> returns a <see cref="Type"/> from a type signature.
		/// </summary>
		/// <returns>The first instruction of the generated code.</returns>
		/// <param name="body"></param>
		/// <param name="method"></param>
		public static Instruction EmitMethodof(this CilBody body, ExtensiblesGenerator main, IMethod method) {
			// TO FUTURE XAN: In case you come back and wonder, no, MethodSig is not valid here.
			Instruction first = body.Emit(OpCodes.Ldtoken, method);
			body.EmitCall(main.Shared.GetMethodFromHandle);
			return first;
		}


		/// <summary>
		/// Emits instructions to throw an <see cref="InvalidOperationException"/> with the provided message.
		/// If the message is null, it will use a string already on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="message">A message, or <see langword="null"/> to use a string that is currently on the stack.</param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitInvalidOpException(this CilBody body, ExtensiblesGenerator main, string message) {
			Instruction first = null;
			if (message != null) first = body.Emit(OpCodes.Ldstr, message);
			Instruction second = body.Emit(OpCodes.Newobj, main.Shared.InvalidOperationExceptionCtor);
			body.Emit(OpCodes.Throw);
			return first ?? second;
		}



		/// <summary>
		/// Emits instructions to throw any exception with the provided message. The exception is imported, but cached.
		/// If the message is null, it will use a string already on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="message">A message, or <see langword="null"/> to use a string that is currently on the stack.</param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitException<T>(this CilBody body, ExtensiblesGenerator main, string message) where T : Exception {
			Instruction first = null;

			main.Shared.DynamicallyImport(typeof(T), out ITypeDefOrRef exceptionRef, out TypeSig exceptionSig);
			MemberRef ctor = main.Shared.DynamicallyReferenceMethod(exceptionRef, ".ctor", () => MethodSig.CreateInstance(main.CorLibTypeSig(), main.CorLibTypeSig<string>()));

			if (message != null) first = body.Emit(OpCodes.Ldstr, message);
			Instruction second = body.Emit(OpCodes.Newobj, ctor);
			body.Emit(OpCodes.Throw);
			return first ?? second;
		}

		/// <summary>
		/// Emits instructions to call <see cref="UnityEngine.Debug.Log"/> with the provided string message.
		/// <para/>
		/// The message can be <see langword="null"/> to use the latest string on the stack instead.
		/// </summary>
		/// <returns>The first instruction of the generated code.</returns>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="message">The message to display, or <see langword="null"/> to use the current string on the stack.</param>
		public static Instruction EmitUnityDbgLog(this CilBody body, ExtensiblesGenerator main, string message) {
			Instruction first = null;
			if (message != null) first = body.Emit(OpCodes.Ldstr, message);
			Instruction second = body.EmitCall(main.Shared.UnityDebugLog);
			return first ?? second;
		}

		/// <summary>
		/// Emits instructions to call <see cref="UnityEngine.Debug.LogWarning"/> with the provided string message.
		/// <para/>
		/// The message can be <see langword="null"/> to use the latest string on the stack instead.
		/// </summary>
		/// <returns>The first instruction of the generated code.</returns>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="message">The message to display, or <see langword="null"/> to use the current string on the stack.</param>
		public static Instruction EmitUnityDbgLogWarning(this CilBody body, ExtensiblesGenerator main, string message) {
			Instruction first = null;
			if (message != null) first = body.Emit(OpCodes.Ldstr, message);
			Instruction second = body.EmitCall(main.Shared.UnityDebugLogWarning);
			return first ?? second;
		}

		/// <summary>
		/// Provide one or more actions that create instructions to load parts of a string. This will follow them with a call to <see cref="string.Concat"/>.
		/// 
		/// All provided instructions should result in strings, but may also result in objects.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="allAreGuaranteedStrings">If true, the system assumes you know <em>for a fact</em> that everything on the stack provided by <paramref name="createParts"/> is unquestionably a string. Setting this to true when this assertion is false will result in invalid IL and will crash the program.</param>
		/// <param name="createParts"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitStringConcat(this CilBody body, ExtensiblesGenerator main, bool allAreGuaranteedStrings, params Action<CilBody, ExtensiblesGenerator>[] createParts) {
			int amount = createParts.Length;
			if (amount == 0) {
				return body.Emit(OpCodes.Ldstr, string.Empty);
			} else if (amount == 1) {
				int index = body.Instructions.Count;
				createParts[0].Invoke(body, main);
				if (body.Instructions.Count > index) {
					// Something actually got emitted.
					return body.Instructions[index];
				} else {
					// Nothing got emitted.
					return body.Emit(OpCodes.Ldstr, string.Empty);
				}
			}
			Instruction first = null;
			if (amount <= 4) {
				// We can use one of C#'s predefined overloads of string.Concat that accepts 1 to 4 arguments.
				foreach (var act in createParts) act.Invoke(body, main); // Emit all the stuff the user declared to push the things onto the stack.
				int index = amount - 1;
				MemberRef[] concatMethodCache = allAreGuaranteedStrings ? main.Shared.StringConcatStrings : main.Shared.StringConcatObjects;
				first = body.EmitCall(concatMethodCache[index]);
			} else {
				// This one gets more complicated, as an array allocation must be performed.
				ITypeDefOrRef arrayType;
				// Pick the proper array type.
				if (allAreGuaranteedStrings) {
					arrayType = main.CorLibTypeRef<string>();
				} else {
					arrayType = main.CorLibTypeRef<object>();
				}
				first = body.EmitLdc_I4(amount);			// Load the length of the array.
				body.Emit(OpCodes.Newarr, arrayType);		// Construct an array of the proper type, push onto stack.
				body.Emit(OpCodes.Dup);						// Copy the array.
				for (int i = 0; i < amount; i++) {
					Action<CilBody, ExtensiblesGenerator> emitInstructions = createParts[i];
					body.EmitLdc_I4(i);						// Load current index...
					emitInstructions.Invoke(body, main);	// Emit instructions provided by user for concat
					body.Emit(OpCodes.Stelem, arrayType);	// Store element (removes the past 3 stack elements, including the Dup above)

					if (i < amount - 1) {
						body.Emit(OpCodes.Dup);				// If it's not the second to last element, duplicate the array on the stack again for ^
					}
				}
				body.EmitCall(allAreGuaranteedStrings ? main.Shared.StringConcatStrings[4] : main.Shared.StringConcatObjects[4]);
			}
			return first;
		}

		/// <summary>
		/// Calls <see cref="object.ToString()"/> on the latest element of the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitTostring(this CilBody body, ExtensiblesGenerator main) {
			return body.EmitCallvirt(main.Shared.ToStringReference);
		}

		/// <summary>
		/// Emits code that loads the provided field from the current type. It will automatically emit the proper opcode(s) based on whether or not the field is static.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="memberRef"></param>
		/// <returns></returns>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitLdThisFldAuto(this CilBody body, FieldDefAndRef field, bool byReference = false) {
			OpCode load;
			Instruction first = null;
			if (field.Definition.IsStatic) {
				load = byReference ? OpCodes.Ldsflda : OpCodes.Ldsfld;
			} else {
				load = byReference ? OpCodes.Ldflda : OpCodes.Ldfld;
				first = body.Emit(OpCodes.Ldarg_0);
			}
			Instruction second = body.Emit(load, field.Reference);
			return first ?? second;
		}

		/// <summary>
		/// Emits code that calls the getter of the provided property. It will automatically emit the proper opcode(s) based on whether or not the property is static and virtual.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="property"></param>
		/// <param name="explicitCall">If true, this MUST be an explicit call (<see cref="OpCodes.Call"/>). This should be true when the goal is to use <see langword="base"/>.Property</param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitGetPropAuto(this CilBody body, PropertyDefAndRef property, bool explicitCall = false) {
			OpCode call;
			Instruction first = null;
			if (property.Getter == null) throw new InvalidOperationException($"The provided property ({property.Definition.FullName}) does not have a getter.");
			if (property.Definition.IsStatic() || explicitCall) {
				call = OpCodes.Call;
			} else {
				call = OpCodes.Callvirt;
				first = body.EmitThis();
			}
			Instruction second = body.Emit(call, property.Getter.Reference);
			return first ?? second;
		}

		/// <summary>
		/// Emits code that calls the setter of the provided property.
		/// <para/>
		/// <strong>Unlike <see cref="EmitGetPropAuto(CilBody, PropertyDefAndRef)"/>, this can NOT automatically emit the full code.</strong>
		/// If the property is an instance property, you must invoke <see cref="EmitThis(CilBody)"/> followed by the code to push the value on the stack.
		/// <para/>
		/// <strong>This only emits the appropriate call type (call vs. callvirt based on whether or not the property is static) and nothing else.</strong>
		/// </summary>
		/// <param name="body"></param>
		/// <param name="property"></param>
		/// <param name="explicitCall">If true, this MUST be an explicit call (<see cref="OpCodes.Call"/>). This should be true when the goal is to use <see langword="base"/>.Property</param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitSetProp(this CilBody body, PropertyDefAndRef property, bool explicitCall = false) {
			OpCode call;
			if (property.Setter == null) throw new InvalidOperationException($"The provided property ({property.Definition.FullName}) does not have a setter.");
			if (property.Definition.IsStatic() || explicitCall) {
				call = OpCodes.Call;
			} else {
				call = OpCodes.Callvirt;
			}
			return body.Emit(call, property.Setter.Reference);
		}

		/// <summary>
		/// Emits either <see cref="OpCodes.Call"/> or <see cref="OpCodes.Callvirt"/> depending on whether or not the method is static.
		/// If the method is not static, <see cref="OpCodes.Callvirt"/> is emitted, unless <paramref name="noVTable"/> is <see langword="true"/> 
		/// from which <see cref="OpCodes.Call"/> is emitted instead.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		/// <param name="noVTable"></param>
		/// <returns></returns>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitCallAuto(this CilBody body, MethodDefAndRef method, bool noVTable = false) {
			bool canCallVirt = !method.Definition.IsStatic && !noVTable;
			if (canCallVirt) return body.Emit(OpCodes.Callvirt, method.Reference);
			return body.Emit(OpCodes.Call, method.Reference);
		}

		/// <summary>
		/// Emits <see cref="OpCodes.Call"/>
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public static Instruction EmitCall(this CilBody body, IMemberRef method) => body.Emit(OpCodes.Call, method);

		/// <summary>
		/// Emits <see cref="OpCodes.Callvirt"/>
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public static Instruction EmitCallvirt(this CilBody body, IMemberRef method) => body.Emit(OpCodes.Callvirt, method);

		/// <summary>
		/// Emits <see cref="OpCodes.Call"/>
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public static Instruction EmitCall(this CilBody body, IMemberDefAndRef method) => body.Emit(OpCodes.Call, method);

		/// <summary>
		/// Emits <see cref="OpCodes.Callvirt"/>
		/// </summary>
		/// <param name="body"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public static Instruction EmitCallvirt(this CilBody body, IMemberDefAndRef method) => body.Emit(OpCodes.Callvirt, method);

		/// <summary>
		/// Emits all the instructions necessary for a call to <see cref="Type.GetMethod(string, BindingFlags, Binder, Type[], ParameterModifier[])"/>.
		/// This assumes the type to call it on is already on the stack.
		/// Leaves behind a <see cref="MethodInfo"/> on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="name">The name to load, or null to use the string currently on the stack.</param>
		/// <param name="bindingFlags"></param>
		/// <param name="types"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitGetMethod(this CilBody body, ExtensiblesGenerator main, string name, BindingFlags bindingFlags, ITypeDefOrRef[] types) {
			Instruction first = null;
			if (name != null) first = body.Emit(OpCodes.Ldstr, name);
			Instruction second = body.EmitLdc_I4((int)bindingFlags);
			body.EmitNull();
			body.EmitArray(main, main.Shared.TypeReference, types.Select<ITypeDefOrRef, Action<CilBody, ExtensiblesGenerator>>(typeRef => {
				return (CilBody body, ExtensiblesGenerator main) => {
					body.EmitTypeof(main, typeRef);
				};
			}).ToArray());
			body.EmitNull();
			body.EmitCallvirt(main.Shared.GetMethod);
			return first ?? second;
		}

		/// <summary>
		/// Emits all the instructions necessary for a call to <see cref="Type.GetProperty(string, BindingFlags)"/>.
		/// This assumes the type to call it on is already on the stack.
		/// Leaves behind a <see cref="PropertyInfo"/> on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="name">The name to load, or null to use the string currently on the stack.</param>
		/// <param name="bindingFlags"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitGetProperty(this CilBody body, ExtensiblesGenerator main, string name, BindingFlags bindingFlags) {
			Instruction first = null;
			if (name != null) first = body.Emit(OpCodes.Ldstr, name);
			Instruction second = body.EmitLdc_I4((int)bindingFlags);
			body.EmitCallvirt(main.Shared.GetProperty);
			return first ?? second;
		}

		/// <summary>
		/// Emits all the instructions necessary for a call to <see cref="Type.GetConstructor(BindingFlags, Binder, Type[], ParameterModifier[])"/>.
		/// This assumes the type to call it on is already on the stack.
		/// Leaves behind a <see cref="ConstructorInfo"/> on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="bindingFlags"></param>
		/// <param name="types"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitGetConstructor(this CilBody body, ExtensiblesGenerator main, BindingFlags bindingFlags, ITypeDefOrRef[] types) {
			Instruction first = body.EmitLdc_I4((int)bindingFlags);
			body.EmitNull();
			body.EmitArray(main, main.Shared.TypeReference, types.Select<ITypeDefOrRef, Action<CilBody, ExtensiblesGenerator>>(typeRef => {
				return (CilBody body, ExtensiblesGenerator main) => {
					body.EmitTypeof(main, typeRef);
				};
			}).ToArray());
			body.EmitNull();
			body.EmitCallvirt(main.Shared.GetConstructor);
			return first;
		}

		/// <summary>
		/// Emits <see cref="object.GetType"/> for the object currently on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <returns></returns>
		public static Instruction EmitGetType(this CilBody body, ExtensiblesGenerator main) {
			return body.EmitCallvirt(main.Shared.GetTypeReference);
		}

		/// <summary>
		/// Emits the instructions necessary to instantiate a new array of the provided <paramref name="arrayType"/> with the provided 
		/// elements generated by the provided instructions.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="arrayType"></param>
		/// <param name="instructionsForObjects"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitArray(this CilBody body, ExtensiblesGenerator main, ITypeDefOrRef arrayType, params Action<CilBody, ExtensiblesGenerator>[] instructionsForObjects) {
			int length = instructionsForObjects.Length;
			Instruction first = body.EmitLdc_I4(length);
			body.Emit(OpCodes.Newarr, arrayType);
			if (length == 0) return first;

			body.EmitDup();
			for (int i = 0; i < length; i++) {
				body.EmitLdc_I4(i);
				Action<CilBody, ExtensiblesGenerator> emitter = instructionsForObjects[i] ?? throw new ArgumentNullException($"instructionsForObjects[{i}] is null.");
				emitter.Invoke(body, main);
				body.Emit(OpCodes.Stelem, arrayType);
				if (i < length - 1) {
					body.EmitDup();
				}
			}
			// Array is left behind on the stack.
			return first;
		}

		/// <summary>
		/// Emits all arguments <c>[0, numArgs)</c> into an array of object[], and leaves the object[] array on the stack.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="main"></param>
		/// <param name="arrayLength"></param>
		/// <param name="startIndex">The index to start writing at.</param>
		/// <param name="firstArgIndex">The argument number to start at.</param>
		/// <param name="assumeArrayAlreadyOnStack">If true, this assumes the array is the current stack element. If false, this will construct a new array and use that instead.</param>
		/// <param name="shouldBoxFunc">This function receives the current argument index and returns whether or not it should be boxed.</param>
		/// <returns>The first instruction of the generated code, or null if no code was generated for the array.</returns>
		public static Instruction EmitArrayOfArgs(this CilBody body, ExtensiblesGenerator main, int arrayLength, int startIndex = 0, int firstArgIndex = 0, bool assumeArrayAlreadyOnStack = false, Func<int, (bool, ITypeDefOrRef)> shouldBoxFunc = null) {
			if (arrayLength < 0) throw new ArgumentOutOfRangeException(nameof(arrayLength), "The length of the array must be greater than or equal to 0.");
			if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index must be greater than or equal to 0.");
			if (firstArgIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex), "First argument index must be greater than or equal to 0.");
			if (firstArgIndex >= arrayLength) throw new ArgumentOutOfRangeException(nameof(firstArgIndex), "The first argument index is too large. It must be less than the number of arguments.");

			Instruction first = null;
			Instruction second = null;
			if (!assumeArrayAlreadyOnStack) {
				first = body.EmitLdc_I4(arrayLength);
				body.Emit(OpCodes.Newarr, main.CorLibTypeRef<object>());
			}
			if (arrayLength == 0) return first;

			if (arrayLength - startIndex > 0) {
				second = body.EmitDup();
			}
			int argIndex = firstArgIndex;
			for (int i = startIndex; i < arrayLength; i++) {
				body.EmitLdc_I4(i);
				body.EmitLdarg(argIndex);
				if (shouldBoxFunc != null) {
					(bool shouldBox, ITypeDefOrRef fromType) = shouldBoxFunc.Invoke(argIndex);
					if (shouldBox) {
						body.Emit(OpCodes.Box, fromType);
					}
				}
				body.Emit(OpCodes.Stelem, main.CorLibTypeRef<object>());
				if (i < arrayLength - 1) {
					body.EmitDup();
				}
				argIndex++;
			}
			// Array is left behind on the stack.
			return first ?? second;
		}

		/// <summary>
		/// Use this to begin a for loop. The <paramref name="continueJump"/> and <paramref name="breakJump"/> instruction should be passed into <see cref="EmitForLoopTail(CilBody, Local, in Instruction, in Instruction, int)"/>.
		/// Your loop code should be placed directly under this. Do not increment <paramref name="countVariable"/> yourself.
		/// Once your code is written, call <see cref="EmitForLoopTail(CilBody, Local, in Instruction, in Instruction, int)"/> to mark the end/repeat of the loop.
		/// <para/>
		/// This line is comparable to this stub: <c>for (<paramref name="countVariable"/> = ...; <paramref name="countVariable"/> &lt; <paramref name="lengthVariable"/>; </c> -- you should set the value of <paramref name="countVariable"/> and <paramref name="lengthVariable"/> before calling this, but do <strong>NOT</strong> need to update them yourself.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="lengthVariable"></param>
		/// <param name="countVariable"></param>
		/// <param name="continueJump">This instruction should be jumped to when the desire is to move to the next iteration. <strong>Do not emit this instruction!</strong></param>
		/// <param name="breakJump">This instruction should be jumped to when the desire is to break out of the loop. <strong>Do not emit this instruction!</strong></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitForLoopHead(this CilBody body, Local countVariable, Local lengthVariable, out Instruction continueJump, out Instruction breakJump) {
			continueJump = body.NewBrDest();
			Instruction first = body.Emit(continueJump);
			breakJump = body.NewBrDest();
			body.EmitLdloc(countVariable);
			body.EmitLdloc(lengthVariable);
			body.Emit(OpCodes.Bge, breakJump);
			// User code would go here, which is after they call this method.
			return first;
		}

		/// <summary>
		///	Use this after the code that should run in a for loop. This will jump back to the head (see <see cref="EmitForLoopHead(CilBody, Local, Local, out Instruction, out Instruction)"/>) if needed.
		///	<para/>
		///	This line is comparable to appending <c>i += incrementBy) {</c> to the stub (see <see cref="EmitForLoopHead(CilBody, Local, Local, out Instruction, out Instruction)"/>), as well as the closing }.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="countVariable"></param>
		/// <param name="continueJump"></param>
		/// <returns>The first instruction of the generated code.</returns>
		public static Instruction EmitForLoopTail(this CilBody body, Local countVariable, in Instruction continueJump, in Instruction breakJump, int incrementBy = 1) {
			// User code would be up here at this line...
			Instruction first = body.EmitLdloc(countVariable);
			body.EmitLdc_I4(incrementBy);
			body.Emit(OpCodes.Add);
			body.EmitStloc(countVariable);
			body.Emit(OpCodes.Br, continueJump);
			body.Emit(breakJump);
			return first;
		}

		/// <summary>
		/// For use in ldarg, which (for some reason) wants this.
		/// </summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Parameter ParameterIndex(int arg) => new Parameter(arg);

		/// <summary>
		/// Emits and returns the version of Ldarg best suited for the input argument index. This also allows inputting the argument index as an int32.
		/// </summary>
		/// <param name="argN"></param>
		/// <returns></returns>
		public static Instruction EmitLdarg(this CilBody body, int argN, bool asReference = false) {
			Instruction result = GetLdarg(argN, asReference);
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Emits and returns the version of Ldc_I4 best suited for the input integer value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction EmitLdc_I4(this CilBody body, int value) {
			Instruction result = GetLdc_I4(value);
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Emits and returns the version of Ldc_I4 best suited for the input integer value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction EmitLdc_I4(this CilBody body, uint value) {
			Instruction result = GetLdc_I4(value);
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Emit a string, or null.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction EmitValue(this CilBody body, string value) {
			if (value is null) return EmitNull(body);
			return Emit(body, OpCodes.Ldstr, value);
		}

		/// <summary>
		/// Emit a boolean value via ldc_i4
		/// </summary>
		/// <param name="body"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction EmitValue(this CilBody body, bool value) => EmitLdc_I4(body, value ? 1 : 0);

		public static Instruction EmitValue(this CilBody body, sbyte value) => EmitLdc_I4(body, value);

		public static Instruction EmitValue(this CilBody body, byte value) => EmitLdc_I4(body, value);

		public static Instruction EmitValue(this CilBody body, short value) => EmitLdc_I4(body, value);

		public static Instruction EmitValue(this CilBody body, ushort value) => EmitLdc_I4(body, value);

		public static Instruction EmitValue(this CilBody body, int value) => EmitLdc_I4(body, value);

		public static Instruction EmitValue(this CilBody body, uint value) => EmitLdc_I4(body, value);

		public static Instruction EmitValue(this CilBody body, float value) => body.Emit(OpCodes.Ldc_R4, value);

		public static Instruction EmitValue(this CilBody body, double value) => body.Emit(OpCodes.Ldc_R8, value);

		public static Instruction EmitValue(this CilBody body, long value) => body.Emit(OpCodes.Ldc_I8, value);

		public static Instruction EmitValue(this CilBody body, ulong value) => body.Emit(OpCodes.Ldc_I8, value);

		/// <summary>
		/// Returns the version of Ldarg best suited for the input argument index. This also allows inputting the argument index as an int32.
		/// </summary>
		/// <param name="argN"></param>
		/// <returns></returns>
		public static Instruction GetLdarg(int argN, bool asReference = false) {
			// If the value is greater than 3 (or its a reference), but less than the byte max value, use _S.
			if ((argN > 3 || asReference) && argN <= byte.MaxValue) return new Instruction(asReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, ParameterIndex(argN));
			if (asReference) return new Instruction(OpCodes.Ldarga, ParameterIndex(argN));
			Instruction result = argN switch {
				0 => OpCodes.Ldarg_0.ToInstruction(),
				1 => OpCodes.Ldarg_1.ToInstruction(),
				2 => OpCodes.Ldarg_2.ToInstruction(),
				3 => OpCodes.Ldarg_3.ToInstruction(),
				_ => new Instruction(OpCodes.Ldarg, ParameterIndex(argN))
			};
			return result;
		}

		/// <summary>
		/// Returns the version of Ldc_I4 best suited for the input integer value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction GetLdc_I4(int value) {
			// If the value is within the sbyte range, and it's not within the range of [-1, 8], use _S.
			if (value >= sbyte.MinValue && value <= sbyte.MaxValue && (value < -1 || value > 8)) return new Instruction(OpCodes.Ldc_I4_S, (sbyte)value);
			Instruction result = value switch {
				-1 => OpCodes.Ldc_I4_M1.ToInstruction(),
				0 => OpCodes.Ldc_I4_0.ToInstruction(),
				1 => OpCodes.Ldc_I4_1.ToInstruction(),
				2 => OpCodes.Ldc_I4_2.ToInstruction(),
				3 => OpCodes.Ldc_I4_3.ToInstruction(),
				4 => OpCodes.Ldc_I4_4.ToInstruction(),
				5 => OpCodes.Ldc_I4_5.ToInstruction(),
				6 => OpCodes.Ldc_I4_6.ToInstruction(),
				7 => OpCodes.Ldc_I4_7.ToInstruction(),
				8 => OpCodes.Ldc_I4_8.ToInstruction(),
				_ => OpCodes.Ldc_I4.ToInstruction(value)
			};
			return result;
		}

		/// <summary>
		/// Returns the version of Ldc_I4 best suited for the input integer value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Instruction GetLdc_I4(uint value) {
			// If the value is within the sbyte range, and it's not within the range of [-1, 8], use _S.
			if (value > 8 && value <= sbyte.MaxValue) return new Instruction(OpCodes.Ldc_I4_S, (sbyte)value);
			Instruction result = value switch {
				0 => OpCodes.Ldc_I4_0.ToInstruction(),
				1 => OpCodes.Ldc_I4_1.ToInstruction(),
				2 => OpCodes.Ldc_I4_2.ToInstruction(),
				3 => OpCodes.Ldc_I4_3.ToInstruction(),
				4 => OpCodes.Ldc_I4_4.ToInstruction(),
				5 => OpCodes.Ldc_I4_5.ToInstruction(),
				6 => OpCodes.Ldc_I4_6.ToInstruction(),
				7 => OpCodes.Ldc_I4_7.ToInstruction(),
				8 => OpCodes.Ldc_I4_8.ToInstruction(),
				_ => OpCodes.Ldc_I4.ToInstruction(value)
			};
			return result;
		}

		/// <summary>
		/// Loads a local onto the stack, choosing the best instruction to use for this task.
		/// </summary>
		/// <param name="local"></param>
		/// <param name="asReference"></param>
		/// <returns></returns>
		public static Instruction EmitLdloc(this CilBody body, Local local, bool asReference = false) {
			if (!body.Variables.Contains(local)) {
				body.Variables.Add(local);
			}
			body.OrderLocals();
			Instruction result;
			if (asReference) {
				OpCode ldloca = local.Index <= byte.MaxValue ? OpCodes.Ldloca_S : OpCodes.Ldloca;
				result = ldloca.ToInstruction(local);
			} else {
				if (local.Index > 3 && local.Index <= byte.MaxValue) {
					result = OpCodes.Ldloc_S.ToInstruction(local);
				} else {
					result = local.Index switch {
						0 => OpCodes.Ldloc_0.ToInstruction(),
						1 => OpCodes.Ldloc_1.ToInstruction(),
						2 => OpCodes.Ldloc_2.ToInstruction(),
						3 => OpCodes.Ldloc_3.ToInstruction(),
						_ => OpCodes.Ldloc.ToInstruction(local)
					};
				}
			}
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Stores a local from the stack, choosing the best instruction to use for this task.
		/// </summary>
		/// <param name="local"></param>
		/// <param name="asReference"></param>
		/// <returns></returns>
		public static Instruction EmitStloc(this CilBody body, Local local) {
			if (!body.Variables.Contains(local)) {
				body.Variables.Add(local);
			}
			body.OrderLocals();
			Instruction result;
			if (local.Index > 3 && local.Index <= byte.MaxValue) {
				result = OpCodes.Stloc_S.ToInstruction(local);
			} else {
				result = local.Index switch {
					0 => OpCodes.Stloc_0.ToInstruction(),
					1 => OpCodes.Stloc_1.ToInstruction(),
					2 => OpCodes.Stloc_2.ToInstruction(),
					3 => OpCodes.Stloc_3.ToInstruction(),
					_ => OpCodes.Stloc.ToInstruction(local)
				};
			}
			body.Instructions.Add(result);
			return result;
		}

		/// <summary>
		/// Emits a stloc followed by a ldloc of the same local.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="local"></param>
		/// <returns></returns>
		public static (Instruction, Instruction) EmitStoreThenLoad(this CilBody body, Local local) {
			Instruction st = body.EmitStloc(local);
			Instruction ld = body.EmitLdloc(local);
			return (st, ld);
		}

		/// <summary>
		/// Appends the provided locals to the method body. This will also apply the appropriate local index.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="locals"></param>
		public static void AppendLocals(this CilBody body, params Local[] locals) {
			int nextID = body.Variables.Count;
			for (int i = 0; i < nextID; i++) {
				Local local = body.Variables[i];
				local.SetIndex(i);
			}
			for (int i = 0; i < locals.Length; i++) {
				Local local = locals[i];
				if (body.Variables.Contains(local)) throw new ArgumentException($"The local {local} is already a part of this method.");
				local.SetIndex(i);
				body.Variables.Add(local);
			}
		}

		/// <summary>
		/// Replaces existing locals of the method body with the provided locals. This will also apply the appropriate local index.
		/// </summary>
		/// <param name="body"></param>
		/// <param name="locals"></param>
		public static void SetLocals(this CilBody body, params Local[] locals) {
			body.Variables.Clear();
			AppendLocals(body, locals);
		}

		/// <summary>
		/// Assigns an appropriate index to all locals.
		/// </summary>
		/// <param name="body"></param>
		public static void OrderLocals(this CilBody body) {
			int localCount = body.Variables.Count;
			for (int i = 0; i < localCount; i++) {
				Local local = body.Variables[i];
				local.SetIndex(i);
			}
		}

		#region Internal Garbage

		internal static void SetIndex(this Local local, int newIndex) {
			if (_localIndexFld == null) {
				_localIndexFld = typeof(Local).GetField("index", BindingFlags.NonPublic | BindingFlags.Instance);
			}
			_localIndexFld.SetValue(local, newIndex);
		}

		private static FieldInfo _localIndexFld = null;

		#endregion
	}
}
