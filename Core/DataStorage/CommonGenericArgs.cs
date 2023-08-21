using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.DataStorage {

	/// <summary>
	/// Generic arguments that will or could be commonly used by this program.
	/// </summary>
	public static class CommonGenericArgs {

		/// <summary><c>!0</c> represents the first generic argument of the type that this member or the current calling context is a part of.</summary>
		public static readonly GenericVar TYPE_ARG_0 = new GenericVar(0);
		/// <summary><c>!1</c> represents the second generic argument of the type that this member or the current calling context is a part of.</summary>
		public static readonly GenericVar TYPE_ARG_1 = new GenericVar(1);
		/// <summary><c>!2</c> represents the third generic argument of the type that this member or the current calling context is a part of.</summary>
		public static readonly GenericVar TYPE_ARG_2 = new GenericVar(2);

		/// <summary><c>&amp;!0</c></summary>
		public static readonly ByRefSig BYREF_TYPE_ARG_0 = new ByRefSig(TYPE_ARG_0);
		/// <summary><c>&amp;!1</c></summary>
		public static readonly ByRefSig BYREF_TYPE_ARG_1 = new ByRefSig(TYPE_ARG_1);
		/// <summary><c>&amp;!2</c></summary>
		public static readonly ByRefSig BYREF_TYPE_ARG_2 = new ByRefSig(TYPE_ARG_2);

		/// <summary><c>!!0</c> represents the first generic argument of the method that is being called.</summary>
		public static readonly GenericMVar METHOD_ARG_0 = new GenericMVar(0);
		/// <summary><c>!!1</c> represents the second generic argument of the method that is being called.</summary>
		public static readonly GenericMVar METHOD_ARG_1 = new GenericMVar(1);
		/// <summary><c>!!2</c> represents the third generic argument of the method that is being called.</summary>
		public static readonly GenericMVar METHOD_ARG_2 = new GenericMVar(2);

		public static readonly ITypeDefOrRef TYPE_ARG_0_REF = TYPE_ARG_0.ToTypeDefOrRef();
		public static readonly ITypeDefOrRef TYPE_ARG_1_REF = TYPE_ARG_1.ToTypeDefOrRef();
		public static readonly ITypeDefOrRef TYPE_ARG_2_REF = TYPE_ARG_2.ToTypeDefOrRef();

	}
}
