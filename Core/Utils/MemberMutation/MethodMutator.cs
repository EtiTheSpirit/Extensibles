using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using HookGenExtender.Core.DataStorage.BulkMemberStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.Utils.MemberMutation {
	public static class MethodMutator {

		/// <summary>
		/// Provided with a method from the game, the extensible type binding for the type declaring that method, and the generator main system, this will
		/// create a new method definition (via <see cref="MethodDefAndRef"/>) of a method with a signature identical to that of its original counterpart.
		/// <para/>
		/// This excludes custom attributes, and does not copy the body of the method.
		/// </summary>
		/// <param name="gameOriginalMethod"></param>
		/// <param name="main"></param>
		/// <returns></returns>
		public static MethodDefAndRef CloneMethodDeclarationFromGame(this MethodDef gameOriginalMethod, ExtensiblesGenerator main, in ExtensibleCoreMembers coreMembers) {
			string name = gameOriginalMethod.Name;
			MethodSig signature = gameOriginalMethod.MethodSig.CloneAndImport(main);
			MethodAttributes attrs = gameOriginalMethod.Attributes;

			return new MethodDefAndRef(main.Extensibles, name, signature, coreMembers.type.ExtensibleType.Reference, attrs);
		}

		/// <summary>
		/// Closely related to <see cref="CloneMethodDeclarationFromGame(MethodDef, ExtensiblesGenerator, in ExtensibleCoreMembers)"/>, this will create an event binding method
		/// for use in the binder. This is the method that actually gets bound to the BepInEx hook. Unlike the original clone technique, this prepends the signature
		/// with the delegate type, and a reference to the self-member.
		/// <para/>
		/// This excludes custom attributes, and does not copy the body of the method.
		/// </summary>
		/// <param name="gameOriginalMethod"></param>
		/// <param name="main"></param>
		/// <param name="binderType"></param>
		/// <param name="origDelegateTypeSig">The <see cref="TypeSig"/> to the <c>orig_*</c> delegate. This must be imported already.</param>
		/// <returns></returns>
		public static MethodDefAndRef CloneMethodDeclarationFromGameAsDelegate(this MethodDef gameOriginalMethod, ExtensiblesGenerator main, in ExtensibleBinderCoreMembers binderType, TypeSig origDelegateTypeSig) {
			string name = gameOriginalMethod.Name;
			MethodSig signature = gameOriginalMethod.MethodSig.CloneAndImport(main);
			signature.Params.Insert(0, origDelegateTypeSig);
			signature.Params.Insert(1, binderType.type.ImportedGameTypeSig);
			MethodAttributes attrs = gameOriginalMethod.Attributes;

			return new MethodDefAndRef(main.Extensibles, name, signature, binderType.type.Binder.Reference, attrs);
		}

		/// <summary>
		/// Clones the provided <paramref name="signature"/> such that the clone has had all of its types imported.
		/// </summary>
		/// <param name="signature"></param>
		/// <returns></returns>
		public static MethodSig CloneAndImport(this MethodSig signature, ExtensiblesGenerator main) {
			// new MethodSig(callingConvention, genParamCount, retType, parameters, paramsAfterSentinel)
			return new MethodSig(
				signature.CallingConvention,
				signature.GenParamCount,
				main.Cache.Import(signature.RetType),
				signature.Params.Select(param => main.Cache.Import(param)).ToArray(),
				signature.ParamsAfterSentinel.Select(param => main.Cache.Import(param)).ToArray()
			);
		}

	}
}
