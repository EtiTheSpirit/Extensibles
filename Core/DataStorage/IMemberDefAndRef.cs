using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	public interface IMemberDefAndRef {
		/// <summary>
		/// The module this is a part of.
		/// </summary>
		ModuleDef Module { get; }

		/// <summary>
		/// The <see cref="ExtensiblesGenerator"/> where this was created.
		/// </summary>
		ExtensiblesGenerator Generator { get; }

		/// <summary>
		/// The declaration of the defined member.
		/// <para/>
		/// <strong>WARNING: Changes to this object will NOT be reflected back to this object! Do not modify this object!</strong>
		/// </summary>
		IMemberDef Definition { get; }

		/// <summary>
		/// A reference to the defined member.
		/// <para/>
		/// <strong>WARNING: Changes to this object will NOT be reflected back to this object! Do not modify this object!</strong>
		/// </summary>
		IMemberRef Reference { get; }

		/// <summary>
		/// Duplicates this member storage such that its declaring type is the provided type definition.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		IMemberDefAndRef AsMemberOfType(IHasTypeDefOrRef type);

	}

	public interface IMemberDefAndRef<TSelf, TMemberDef> : IMemberDefAndRef where TSelf : IMemberDefAndRef<TSelf, TMemberDef> {


		/// <summary>
		/// The declaration of the defined member.
		/// <para/>
		/// <strong>WARNING: Changes to this object will NOT be reflected back to this object! Do not modify this object!</strong>
		/// </summary>
		new TMemberDef Definition { get; }

		/// <summary>
		/// Duplicates this member storage such that its declaring type is the provided type definition.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		new TSelf AsMemberOfType(IHasTypeDefOrRef type);

	}
}
